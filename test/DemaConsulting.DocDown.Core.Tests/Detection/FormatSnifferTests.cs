using System.Text;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Detection;

/// <summary>
///     Unit tests for <see cref="FormatSniffer"/>, exercising its extension-primary detection
///     precedence, the content-signature fallback, and its non-destructive, position-restoring
///     contract in isolation.
/// </summary>
/// <remarks>
///     These tests bind only to <see cref="FormatSniffer"/>, which has no collaborators. Each is
///     named for the unit requirement it evidences: extension-primary identification,
///     content-signature fallback, unknown-format reporting, basis-and-confidence reporting, and
///     stream-position restoration.
/// </remarks>
public class FormatSnifferTests
{
    /// <summary>
    ///     Proves a leading PDF signature is identified when no file name is available (ContentSignature).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_PdfSignatureWithoutFileName_IdentifiesPdfByContentSignature()
    {
        // Arrange: PDF magic bytes with a null file name so only content evidence can apply
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-2.0\nnot really a pdf"));

        // Act: sniff with no file name
        var detection = FormatSniffer.Detect(stream, null);

        // Assert: the fallback content signature identifies PDF at full confidence
        Assert.Equal(DocumentFormat.Pdf, detection.Format);
        Assert.Equal(DetectionBasis.ContentSignature, detection.Basis);
    }

    /// <summary>
    ///     Proves a five-byte <c>%PDF-</c> header with an unrecognized extension is identified (ContentSignature).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_PdfSignatureWithUnknownExtension_FallsBackToContentSignature()
    {
        // Arrange: exactly the five signature bytes behind an extension the table does not know
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-"));

        // Act: sniff, so the unrecognized extension yields nothing and the signature is consulted
        var detection = FormatSniffer.Detect(stream, "mystery.dat");

        // Assert: the fallback recognized the PDF header even though the name did not
        Assert.Equal(DocumentFormat.Pdf, detection.Format);
        Assert.Equal(DetectionBasis.ContentSignature, detection.Basis);
    }

    /// <summary>
    ///     Proves a bare <c>&lt;html&gt;</c> root element (no doctype) is identified from content (ContentSignature).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_HtmlRootElement_IdentifiesHtmlByContentSignature()
    {
        // Arrange: content that opens with a root html element after leading whitespace, no doctype
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("   <html lang=\"en\"><body>hi</body></html>"));

        // Act: sniff with an unrecognized .bin name so only the content fallback applies
        var detection = FormatSniffer.Detect(stream, "page.bin");

        // Assert: the html root element is recognized at the documented 0.9 confidence
        Assert.Equal(DocumentFormat.Html, detection.Format);
        Assert.Equal(DetectionBasis.ContentSignature, detection.Basis);
    }

    /// <summary>
    ///     Proves the file-name extension is trusted even when the bytes say otherwise (ExtensionPrimary).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_ExtensionContradictsContent_TrustsExtension()
    {
        // Arrange: PDF magic bytes deliberately behind a .txt file name
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nstill named as text"));

        // Act: sniff, so the primary extension signal competes with a strong content signature
        var detection = FormatSniffer.Detect(stream, "readme.txt");

        // Assert: the name wins; the decoder, not the sniffer, is what proves the bytes disagree
        Assert.Equal(DocumentFormat.Text, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves an unsignatured document is identified from its extension (ExtensionPrimary).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_VsdxExtensionWithoutSignature_IdentifiesVsdxByExtension()
    {
        // Arrange: plain bytes carrying no signature but a Visio file name
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain content, no magic number"));

        // Act: sniff using the primary extension signal
        var detection = FormatSniffer.Detect(stream, "diagram.vsdx");

        // Assert: the extension identifies Visio at the documented 0.9 confidence
        Assert.Equal(DocumentFormat.Vsdx, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves a macro-enabled Visio drawing is identified from its extension as a drawing.
    /// </summary>
    /// <remarks>
    ///     <c>.vsdm</c> is the macro-enabled sibling of <c>.vsdx</c>; DocDown detects it as a drawing
    ///     (never a stencil or template) so the Visio backend reads its pages, shapes, and connectors
    ///     exactly as it does a <c>.vsdx</c>.
    /// </remarks>
    [Fact]
    public void FormatSniffer_Detect_VsdmExtensionWithoutSignature_IdentifiesVsdmByExtension()
    {
        // Arrange: plain bytes carrying no signature but a macro-enabled Visio file name
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain content, no magic number"));

        // Act: sniff using the primary extension signal
        var detection = FormatSniffer.Detect(stream, "diagram.vsdm");

        // Assert: the extension identifies the macro-enabled Visio drawing at the documented 0.9 confidence
        Assert.Equal(DocumentFormat.Vsdm, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves each legacy binary Office extension is identified from its name, with the correct
    ///     format id and media type at the extension-primary basis and confidence (ExtensionPrimary).
    /// </summary>
    /// <param name="fileName">The document file name carrying a mixed-case legacy extension.</param>
    /// <param name="expectedId">The stable lowercase format id the extension must resolve to.</param>
    /// <param name="expectedMediaType">The IANA media type the resolved format must carry.</param>
    /// <remarks>
    ///     The legacy binaries are detectable but not extractable; detection trusting the name is
    ///     what lets the engine report a format-specific failure instead of an unrecognized one.
    ///     The mixed-case names also prove the invariant-lowercase extension comparison.
    /// </remarks>
    [Theory]
    [InlineData("report.DOC", "doc", "application/msword")]
    [InlineData("budget.Xls", "xls", "application/vnd.ms-excel")]
    [InlineData("deck.pPt", "ppt", "application/vnd.ms-powerpoint")]
    [InlineData("diagram.VSD", "vsd", "application/vnd.visio")]
    public void FormatSniffer_Detect_LegacyBinaryExtension_IdentifiesByExtension(
        string fileName, string expectedId, string expectedMediaType)
    {
        // Arrange: plain bytes carrying no signature, so only the file name can identify the format
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain content, no magic number"));

        // Act: sniff using the primary extension signal with mixed-case names
        var detection = FormatSniffer.Detect(stream, fileName);

        // Assert: the legacy format is named by extension with the verified id and media type
        Assert.Equal(expectedId, detection.Format.Id);
        Assert.Equal(expectedMediaType, detection.Format.MediaType);
        Assert.False(detection.Format.IsUnknown);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves the extension match is case-insensitive via an invariant lowercase comparison (ExtensionPrimary).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_UpperCaseLogExtension_IdentifiesTextCaseInsensitively()
    {
        // Arrange: plain bytes with an upper-case .LOG extension
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("2026-01-01 log line"));

        // Act: sniff using the primary extension signal with mixed case
        var detection = FormatSniffer.Detect(stream, "SERVER.LOG");

        // Assert: the case-insensitive extension match identifies text
        Assert.Equal(DocumentFormat.Text, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves unrecognized content with no file name is reported as unknown with zero confidence (UnknownFormat).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_UnrecognizedContentNoFileName_ReportsUnknown()
    {
        // Arrange: bytes with no signature and no file name to identify from
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05]);

        // Act: sniff content that matches nothing
        var detection = FormatSniffer.Detect(stream, null);

        // Assert: the format is unknown, by extension basis, at zero confidence
        Assert.True(detection.Format.IsUnknown);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves an empty stream is reported as unknown rather than throwing (UnknownFormat).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_EmptyStreamUnknownExtension_ReportsUnknown()
    {
        // Arrange: an empty stream with an unrecognized extension
        using var stream = new MemoryStream();

        // Act: sniff the empty content
        var detection = FormatSniffer.Detect(stream, "mystery.dat");

        // Assert: no known extension and no signature means unknown
        Assert.True(detection.Format.IsUnknown);
    }

    /// <summary>
    ///     Proves the reported basis and confidence match the extension-primary contract (BasisAndConfidence).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_ExtensionMatch_ReportsExtensionBasisAndHighConfidence()
    {
        // Arrange: a document identified only by its .htm extension
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not markup, just prose"));

        // Act: sniff using the primary extension signal
        var detection = FormatSniffer.Detect(stream, "index.htm");

        // Assert: the trusted extension basis carries exactly the 0.9 confidence contract
        Assert.Equal(DocumentFormat.Html, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves an extension match leaves the caller's stream position untouched (StreamPositionRestored).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_ExtensionMatchFromOffset_LeavesStreamPositionUntouched()
    {
        // Arrange: content with a recognized name and the stream positioned partway through
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("just plain text content here"));
        stream.Position = 7;

        // Act: sniff, which resolves from the name alone and never reads a byte
        FormatSniffer.Detect(stream, "notes.txt");

        // Assert: the caller's position is exactly where it was left
        Assert.Equal(7, stream.Position);
    }

    /// <summary>
    ///     Proves the stream position is restored after the content-signature fallback reads bytes (StreamPositionRestored).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_ContentFallbackFromOffset_RestoresStreamPosition()
    {
        // Arrange: PDF bytes with an unrecognized name, positioned partway through
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nbody bytes follow here"));
        stream.Position = 5;

        // Act: sniff, which must rewind to read the signature and then restore the entry position
        var detection = FormatSniffer.Detect(stream, "unnamed.dat");

        // Assert: the fallback identified PDF and the caller's position was restored
        Assert.Equal(DocumentFormat.Pdf, detection.Format);
        Assert.Equal(5, stream.Position);
    }

    /// <summary>
    ///     Proves a null content stream is rejected with an argument-null exception (error path).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_NullContent_ThrowsArgumentNullException()
    {
        // Act + Assert: a missing stream is a caller error, checked before any other work
        Assert.Throws<ArgumentNullException>(() => FormatSniffer.Detect(null!, "document.pdf"));
    }

    /// <summary>
    ///     Proves a non-seekable stream is rejected so detection stays non-destructive (error path).
    /// </summary>
    [Fact]
    public void FormatSniffer_Detect_NonSeekableStream_ThrowsArgumentException()
    {
        // Arrange: a read-only, non-seekable stream wrapper over some bytes
        using var inner = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7"));
        using var nonSeekable = new NonSeekableStream(inner);

        // Act + Assert: seekability is mandatory so the position can be restored
        Assert.Throws<ArgumentException>(() => FormatSniffer.Detect(nonSeekable, "document.pdf"));
    }

    /// <summary>
    ///     A minimal read-only stream that reports itself as non-seekable for error-path testing.
    /// </summary>
    /// <remarks>
    ///     Wraps a seekable inner stream but returns <see langword="false"/> from
    ///     <see cref="CanSeek"/> so the sniffer's seekability guard can be exercised.
    /// </remarks>
    private sealed class NonSeekableStream : Stream
    {
        /// <summary>The underlying seekable stream the reads are forwarded to.</summary>
        /// <remarks>Owned by the caller; this wrapper only forwards read operations.</remarks>
        private readonly Stream _inner;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NonSeekableStream"/> class.
        /// </summary>
        /// <param name="inner">The inner stream to forward reads to.</param>
        /// <remarks>Kept minimal: only the members the sniffer touches are meaningfully implemented.</remarks>
        public NonSeekableStream(Stream inner) => _inner = inner;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush() => _inner.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
