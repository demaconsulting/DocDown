using System.Text;

namespace DocDown.Core;

/// <summary>
///     Identifies a document's format from its file-name extension, falling back to a leading
///     content signature, and reports both the format and the evidence behind it.
/// </summary>
/// <remarks>
///     <para>
///         Detection follows a fixed precedence: the file-name extension is the primary signal, and
///         a leading magic-byte signature is consulted only when the name carries no extension or
///         an extension this library does not recognize. The extension is trusted because the
///         caller already possesses the document and named it; sniffing the bytes changes only
///         which parser runs, not whether hostile bytes are parsed, and a wrongly-named file is
///         rejected honestly by the decoder that receives it. Reporting the
///         <see cref="DetectionBasis"/> and a confidence score alongside the format keeps the
///         record of <em>how</em> the format was determined available to <c>manifest.json</c>,
///         downstream selection, and human reviewers.
///     </para>
///     <para>
///         The sniffer is non-destructive: it requires a seekable stream and restores the stream's
///         original position before returning, so callers can hand the same stream to an extractor
///         afterwards. Reading advances then restores the stream position as a side effect. It is
///         a static class because detection is now a pure function of a stream and a name — no
///         collaborator and no state remain — so it is inherently safe for concurrent use provided
///         each call is given its own stream. No archive, container, or nested-document format is
///         opened here — detection never parses the document.
///     </para>
/// </remarks>
public static class FormatSniffer
{
    /// <summary>
    ///     The maximum number of leading bytes inspected for a content signature.
    /// </summary>
    /// <remarks>
    ///     512 bytes is ample for every magic number and for a leading HTML doctype while bounding
    ///     the read cost; named as a constant so the bound is stated once.
    /// </remarks>
    private const int SignatureBytes = 512;

    /// <summary>
    ///     The file-extension-to-format table, the primary detection signal.
    /// </summary>
    /// <remarks>
    ///     Declared once as an ordered table keyed by lowercase extension. The table is the
    ///     vocabulary of formats DocDown knows how to name; recognizing an extension here does not
    ///     imply an extractor is registered for it — that is <c>ExtractorSelector</c>'s concern, and
    ///     it reports the absence honestly.
    /// </remarks>
    private static readonly (string Extension, DocumentFormat Format)[] ExtensionMap =
    [
        (".pdf", DocumentFormat.Pdf),
        (".docx", DocumentFormat.Docx),
        (".doc", DocumentFormat.Doc),
        (".xlsx", DocumentFormat.Xlsx),
        (".xls", DocumentFormat.Xls),
        (".pptx", DocumentFormat.Pptx),
        (".ppt", DocumentFormat.Ppt),
        (".vsdx", DocumentFormat.Vsdx),
        (".vsdm", DocumentFormat.Vsdm),
        (".vsd", DocumentFormat.Vsd),
        (".html", DocumentFormat.Html),
        (".htm", DocumentFormat.Html),
        (".txt", DocumentFormat.Text),
        (".text", DocumentFormat.Text),
        (".log", DocumentFormat.Text)
    ];

    /// <summary>
    ///     Detects the format of the supplied content, trusting the file-name extension first.
    /// </summary>
    /// <param name="content">
    ///     A readable, seekable stream over the document bytes. Must not be null and must support
    ///     seeking; its position is restored before the method returns.
    /// </param>
    /// <param name="fileName">
    ///     The document's file name, the primary detection signal, or <see langword="null"/> when
    ///     no name is available.
    /// </param>
    /// <returns>
    ///     A <see cref="FormatDetection"/> carrying the identified format, the evidence basis, and a
    ///     confidence score.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is not readable or not seekable.</exception>
    /// <remarks>
    ///     A recognized extension short-circuits the method before any byte is read, so the common
    ///     case costs no I/O at all. Only when the name yields nothing are up to
    ///     <see cref="SignatureBytes"/> leading bytes read and tested for a <c>%PDF-</c> header or
    ///     an HTML doctype. Seekability is required so the stream position can be restored, keeping
    ///     detection non-destructive for a subsequent extractor. The method never throws for
    ///     malformed content — unrecognized input returns an <see cref="DocumentFormat.Unknown"/>
    ///     detection.
    /// </remarks>
    public static FormatDetection Detect(Stream content, string? fileName)
    {
        // A stream is mandatory; without one there is nothing to inspect
        ArgumentNullException.ThrowIfNull(content);

        // Seekability lets us both inspect and restore, which is what makes detection non-destructive
        if (!content.CanRead || !content.CanSeek)
        {
            throw new ArgumentException("The content stream must be readable and seekable.", nameof(content));
        }

        // Primary signal: the caller named the file, so believe the name it gave us
        var byExtension = DetectByExtension(fileName);
        if (!byExtension.Format.IsUnknown)
        {
            return byExtension;
        }

        // Remember where the caller left the stream so we can restore it no matter which path we take
        var originalPosition = content.Position;
        try
        {
            // Read the leading bytes once; every content-signature test works from this buffer
            content.Position = 0;
            var buffer = new byte[SignatureBytes];
            var read = ReadUpTo(content, buffer, SignatureBytes);

            // Fallback evidence: a PDF header is an unambiguous content signature
            if (StartsWith(buffer, read, PdfSignature))
            {
                return new FormatDetection(DocumentFormat.Pdf, DetectionBasis.ContentSignature, 1.0);
            }

            // Slightly weaker but still content-based: a leading HTML doctype or root element
            if (LooksLikeHtml(buffer, read))
            {
                return new FormatDetection(DocumentFormat.Html, DetectionBasis.ContentSignature, 0.9);
            }

            // Neither the name nor the leading bytes identified the format; say so honestly
            return byExtension;
        }
        finally
        {
            // Always restore the caller's position so detection leaves the stream as it was found
            content.Position = originalPosition;
        }
    }

    /// <summary>
    ///     The PDF content signature (<c>%PDF-</c>) in ASCII bytes.
    /// </summary>
    /// <remarks>Held as a static array so the signature bytes are declared once for reuse.</remarks>
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    /// <summary>
    ///     Determines the format from a file-name extension, the primary detection basis.
    /// </summary>
    /// <param name="fileName">The file name to inspect, or <see langword="null"/>.</param>
    /// <returns>
    ///     A matching known format with basis <see cref="DetectionBasis.Extension"/> and confidence
    ///     <c>0.9</c>, or an <see cref="DocumentFormat.Unknown"/> detection with confidence
    ///     <c>0.0</c> when the name has no extension or an unrecognized one.
    /// </returns>
    /// <remarks>
    ///     Extensions are matched case-insensitively via an invariant-lowercase comparison so
    ///     <c>.PDF</c> and <c>.pdf</c> are equivalent. Confidence is high but deliberately short of
    ///     <c>1.0</c>: the name is trusted, but only the decoder can prove the bytes agree with it.
    ///     Pure and side-effect free.
    /// </remarks>
    private static FormatDetection DetectByExtension(string? fileName)
    {
        // With no name there is no extension evidence; report unknown with zero confidence
        if (!string.IsNullOrEmpty(fileName))
        {
            // Compare against the table using an invariant lowercase extension for stable results
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            foreach (var (candidate, format) in ExtensionMap)
            {
                if (string.Equals(extension, candidate, StringComparison.Ordinal))
                {
                    return new FormatDetection(format, DetectionBasis.Extension, 0.9);
                }
            }
        }

        return new FormatDetection(DocumentFormat.Unknown, DetectionBasis.Extension, 0.0);
    }

    /// <summary>
    ///     Reads up to <paramref name="count"/> bytes into <paramref name="buffer"/>, tolerating
    ///     short reads.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes actually read, which may be less than <paramref name="count"/> at end of stream.</returns>
    /// <remarks>
    ///     A single <see cref="Stream.Read(byte[],int,int)"/> may return fewer bytes than requested,
    ///     so this loops until the buffer is filled or the stream ends, guaranteeing a well-defined
    ///     prefix length for the signature tests.
    /// </remarks>
    private static int ReadUpTo(Stream stream, byte[] buffer, int count)
    {
        // Accumulate across partial reads so a chunked stream still yields the full available prefix
        var total = 0;
        while (total < count)
        {
            var n = stream.Read(buffer, total, count - total);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }

    /// <summary>
    ///     Tests whether the first <paramref name="length"/> bytes of <paramref name="buffer"/>
    ///     begin with the given signature.
    /// </summary>
    /// <param name="buffer">The buffer holding the leading bytes.</param>
    /// <param name="length">The number of valid bytes in <paramref name="buffer"/>.</param>
    /// <param name="signature">The signature bytes to compare.</param>
    /// <returns><see langword="true"/> when the prefix matches; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Compares only within the valid length so a stream shorter than the signature never
    ///     produces a false match on uninitialized buffer bytes. Pure and side-effect free.
    /// </remarks>
    private static bool StartsWith(byte[] buffer, int length, byte[] signature)
    {
        // A prefix cannot match if fewer valid bytes were read than the signature length
        if (length < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether the leading bytes look like HTML after skipping any BOM and whitespace.
    /// </summary>
    /// <param name="buffer">The buffer holding the leading bytes.</param>
    /// <param name="length">The number of valid bytes in <paramref name="buffer"/>.</param>
    /// <returns>
    ///     <see langword="true"/> when the content begins with <c>&lt;!DOCTYPE html</c> or
    ///     <c>&lt;html</c> (case-insensitive); otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     Handles UTF-8 and UTF-16 (LE/BE) byte-order marks so an HTML document saved in any of
    ///     those encodings is still recognized, then compares case-insensitively because the HTML
    ///     doctype and root tag are case-insensitive. Pure and side-effect free.
    /// </remarks>
    private static bool LooksLikeHtml(byte[] buffer, int length)
    {
        // Choose the text encoding from any leading BOM so UTF-16 content decodes correctly
        var (encoding, offset) = DetectEncoding(buffer, length);

        // Nothing meaningful to test once the BOM is skipped
        if (offset >= length)
        {
            return false;
        }

        // Decode the readable prefix and trim leading whitespace before matching the HTML markers
        var text = encoding.GetString(buffer, offset, length - offset).TrimStart();
        return text.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Detects a leading byte-order mark and returns the matching encoding and the offset past it.
    /// </summary>
    /// <param name="buffer">The buffer holding the leading bytes.</param>
    /// <param name="length">The number of valid bytes in <paramref name="buffer"/>.</param>
    /// <returns>
    ///     A tuple of the encoding to decode with and the byte offset at which real content begins
    ///     (past any BOM).
    /// </returns>
    /// <remarks>
    ///     Defaults to UTF-8 with no offset when no BOM is present, which also correctly handles
    ///     plain ASCII. Recognizing UTF-16 BOMs is necessary so wide-encoded HTML is not missed.
    ///     Pure and side-effect free.
    /// </remarks>
    private static (Encoding Encoding, int Offset) DetectEncoding(byte[] buffer, int length)
    {
        // UTF-8 BOM: EF BB BF
        if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return (Encoding.UTF8, 3);
        }

        // UTF-16 little-endian BOM: FF FE
        if (length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            return (Encoding.Unicode, 2);
        }

        // UTF-16 big-endian BOM: FE FF
        if (length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        // No BOM: treat as UTF-8, which subsumes ASCII for the markers we test
        return (Encoding.UTF8, 0);
    }
}
