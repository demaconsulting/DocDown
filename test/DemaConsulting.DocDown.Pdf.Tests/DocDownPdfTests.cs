using System.IO.Compression;
using System.Text.Json;
using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     System-level integration tests for the DocDown.Pdf extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against PDFs generated at test time.
/// </summary>
/// <remarks>
///     <para>
///         Every scenario runs the real engine over a real PDF and ends by confirming the contract
///         verifier finds no violations, so a reported gap always matches what is on disk. That
///         reconciliation is the highest-value assertion available: it makes a dishonest extraction
///         a test failure rather than a review finding.
///     </para>
///     <para>
///         Degradation is treated as the common case, not the exception. A PDF extractor that ships
///         no renderer degrades on every page-rendering request; a scanned document has no text to
///         find; a JPEG 2000 image cannot be decoded here. Most rows below therefore assert an
///         honest, explained shortfall rather than a clean success — which is what the library
///         promises.
///     </para>
///     <para>
///         Text-extraction quality is heuristic, so these tests assert properties of the output
///         (expected substrings, relative order, the presence of links and markers) rather than
///         golden markdown. Byte-exact comparison is reserved for the determinism scenario, where
///         both sides come from the same code.
///     </para>
/// </remarks>
public class DocDownPdfTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a plain text PDF produces the complete contract layout with no gaps.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_SimpleTextPdf_ProducesContractLayout()
    {
        // Arrange: an engine with the PDF backend and a generated single-page text document
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", PdfFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract into a fresh scratch folder
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run is clean, the full layout exists, and the document's text is present
        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Gaps);
        Assert.Equal("pdf", result.SelectedExtractor?.Id);
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("quick brown fox", content, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.True(File.Exists(Path.Combine(scratch, "manifest.json")));
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a multi-page PDF reports its page count and carries every page's text in order.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_MultiPagePdf_ReportsPageCountAndAllText()
    {
        // Arrange: a generated three-page document whose pages carry distinguishable markers
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "multi.pdf", PdfFixtures.MultiPage(3));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the whole document
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the manifest reports three pages and the content carries all three in reading order
        using var manifest = await ReadManifestAsync(scratch);
        Assert.Equal(3, manifest.RootElement.GetProperty("document").GetProperty("pageCount").GetInt32());
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        var first = content.IndexOf("Page 1 marker", StringComparison.Ordinal);
        var second = content.IndexOf("Page 2 marker", StringComparison.Ordinal);
        var third = content.IndexOf("Page 3 marker", StringComparison.Ordinal);
        Assert.True(first >= 0 && second > first && third > second, "page text is missing or out of reading order");
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a DCTDecode image round-trips byte-identically and is labeled a passthrough.
    /// </summary>
    /// <remarks>
    ///     This is the assertion that makes the passthrough provenance claim falsifiable rather than
    ///     decorative: the written file is compared against the exact JPEG the fixture embedded, not
    ///     against anything the extractor produced. If the extractor ever silently re-encoded a JPEG
    ///     while still calling it a passthrough, this test would fail on the bytes rather than passing
    ///     on the label.
    /// </remarks>
    [Fact]
    public async Task DocDownPdf_Extract_DctImage_RoundTripsByteIdenticalAsPassthrough()
    {
        // Arrange: a generated document embedding a known JPEG as a DCTDecode image
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "jpeg.pdf", PdfFixtures.WithEmbeddedJpeg());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract with the default preserve-encoding image policy
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: exactly one image was written, as .jpg, and its bytes are the embedded source bytes
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        var written = Assert.Single(images);
        Assert.EndsWith(".jpg", written, StringComparison.Ordinal);
        Assert.Equal(PdfFixtures.SourceJpegBytes(), await File.ReadAllBytesAsync(written, Ct));

        // Assert: the manifest states the true media type and the true provenance
        using var manifest = await ReadManifestAsync(scratch);
        var entry = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("image/jpeg", entry.GetProperty("mediaType").GetString());
        Assert.Equal("passthrough", entry.GetProperty("transform").GetString());

        // Assert: the content document links the image by the path the sink allocated
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains(entry.GetProperty("path").GetString()!, content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a Flate-compressed image is labeled as decoded to PNG, not as a passthrough.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_FlateImage_IsLabeledDecodedToPng()
    {
        // Arrange: a generated document whose image is stored as compressed samples, not as a file
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "png.pdf", PdfFixtures.WithEmbeddedPng());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract with the default preserve-encoding image policy
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the image is written as PNG and labeled as the re-encoding it genuinely is
        var written = Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        Assert.EndsWith(".png", written, StringComparison.Ordinal);
        using var manifest = await ReadManifestAsync(scratch);
        var entry = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("image/png", entry.GetProperty("mediaType").GetString());
        Assert.Equal("decodedToPng", entry.GetProperty("transform").GetString());
        Assert.NotEqual("passthrough", entry.GetProperty("transform").GetString());
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves an image this extractor cannot decode becomes a counted gap naming the encoding.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_UndecodableImage_ReportsCountedGapNamingEncoding()
    {
        // Arrange: a document holding one decodable JPEG and one JBIG2 image
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "mixed.pdf", PdfFixtures.WithUndecodableImage());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the decodable image was written and the undecodable one was counted, not dropped
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        var gap = Assert.Single(result.Gaps, candidate =>
            candidate.Target == "images/" && candidate.Reason.Contains("JBIG2Decode", StringComparison.Ordinal));
        Assert.Equal(GapScope.PartiallyExtracted, gap.Scope);
        Assert.Equal(1, gap.AffectedCount);
        Assert.Contains("1 of 2", gap.Reason, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDF0001");

        // Assert: the ledger records one of two, and the human-readable summary says so too
        using var manifest = await ReadManifestAsync(scratch);
        var images = manifest.RootElement.GetProperty("artifacts").GetProperty("images");
        Assert.Equal("partial", images.GetProperty("status").GetString());
        Assert.Equal(1, images.GetProperty("obtained").GetInt32());
        Assert.Equal(2, images.GetProperty("found").GetInt32());
        var summary = Normalize(await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct));
        Assert.Contains("1 of 2", summary, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a JPEG 2000 image is written as .jp2, labeled a passthrough, and caveated in the summary.
    /// </summary>
    /// <remarks>
    ///     This package takes no JPEG 2000 decoder, so the only two honest options are to write the
    ///     codestream as the file it is or to write nothing. A file a JPEG 2000-capable consumer can
    ///     open is worth more than an absence — but only together with the warning that many viewers
    ///     and image libraries cannot read it, which is why the caveat reaching <c>summary.txt</c> is
    ///     asserted here rather than assumed from the gap list. The assertion is made on whitespace-
    ///     normalized text because the summary wraps its prose.
    /// </remarks>
    [Fact]
    public async Task DocDownPdf_Extract_Jpeg2000Image_WritesJp2PassthroughWithReadabilityCaveat()
    {
        // Arrange: a document holding one decodable JPEG and one JPEG 2000 image
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "jpx.pdf", PdfFixtures.WithJpeg2000Image());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: both images are on disk, the JPEG 2000 one under an extension describing its bytes
        var images = Directory.GetFiles(Path.Combine(scratch, "images")).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(2, images.Count);
        var jp2 = Assert.Single(images, path => path.EndsWith(".jp2", StringComparison.Ordinal));
        Assert.Equal(PdfFixtures.SourceJpeg2000Bytes(), await File.ReadAllBytesAsync(jp2, Ct));

        // Assert: the manifest calls it JPEG 2000 and states the passthrough it genuinely was
        using var manifest = await ReadManifestAsync(scratch);
        var entry = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray().ToList(),
            candidate => candidate.GetProperty("path").GetString()!.EndsWith(".jp2", StringComparison.Ordinal));
        Assert.Equal("image/jp2", entry.GetProperty("mediaType").GetString());
        Assert.Equal("passthrough", entry.GetProperty("transform").GetString());

        // Assert: the ledger counts it as extracted rather than lost, so the denominator stays honest
        var ledger = manifest.RootElement.GetProperty("artifacts").GetProperty("images");
        Assert.Equal(2, ledger.GetProperty("obtained").GetInt32());
        Assert.Equal(2, ledger.GetProperty("found").GetInt32());

        // Assert: and the summary carries the readability caveat in words a reader will see
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        var summary = Normalize(await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct));
        Assert.Contains("Many image viewers and image libraries cannot read JPEG 2000 files.",
            summary, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a PNG-output request that cannot be honored is explained rather than hidden.
    /// </summary>
    /// <remarks>
    ///     Core's naming rule already guarantees the file on disk is not mislabeled — the extension
    ///     follows the bytes written. What this scenario adds is the explanation: a caller who asked
    ///     for PNG and received JPEG is told which images were affected and why, so the unhonored
    ///     option is visible rather than silently absorbed.
    /// </remarks>
    [Fact]
    public async Task DocDownPdf_Extract_ForcePngWithJpegImage_ExplainsUnhonoredMode()
    {
        // Arrange: a JPEG-bearing document extracted with PNG output demanded
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "jpeg.pdf", PdfFixtures.WithEmbeddedJpeg());
        var scratch = Path.Combine(temp.Path, "out");
        var options = FixedOptions();
        options.ImageOutput = ImageOutputMode.ForcePng;

        // Act: extract with an output mode that cannot be honored
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the file on disk still describes its own bytes truthfully
        var written = Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        Assert.EndsWith(".jpg", written, StringComparison.Ordinal);
        using var manifest = await ReadManifestAsync(scratch);
        var entry = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("image/jpeg", entry.GetProperty("mediaType").GetString());

        // Assert: and the run explains, rather than hides, that PNG could not be produced
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDF0002");
        var gap = Assert.Single(result.Gaps, candidate =>
            candidate.Target == "images/" && candidate.Reason.Contains("PNG output was requested", StringComparison.Ordinal));
        Assert.Equal(1, gap.AffectedCount);
        var summary = Normalize(await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct));
        Assert.Contains("PNG output was requested", summary, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves an image over a caller's size limit is reported as its own skip gap.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_ImageOverSizeLimit_ReportsSeparateSkipGap()
    {
        // Arrange: a JPEG-bearing document extracted with an impossibly small byte budget
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "jpeg.pdf", PdfFixtures.WithEmbeddedJpeg());
        var scratch = Path.Combine(temp.Path, "out");
        var options = FixedOptions();
        options.MaxImageBytes = 8;

        // Act: extract with the size limit in force
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: nothing was written, and the gap names a size limit rather than a decode failure
        Assert.False(Directory.Exists(Path.Combine(scratch, "images"))
            && Directory.GetFiles(Path.Combine(scratch, "images")).Length > 0);
        var gap = Assert.Single(result.Gaps, candidate =>
            candidate.Target == "images/" && candidate.Reason.Contains("size or dimension limit", StringComparison.Ordinal));
        Assert.Equal(GapScope.Unavailable, gap.Scope);
        Assert.DoesNotContain("cannot decode", gap.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "PDF0001");
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves disabling embedded images suppresses them with an explained gap and no dangling links.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_ImagesDisabled_SuppressesImagesWithGap()
    {
        // Arrange: a JPEG-bearing document extracted with embedded images turned off
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "jpeg.pdf", PdfFixtures.WithEmbeddedJpeg());
        var scratch = Path.Combine(temp.Path, "out");
        var options = FixedOptions();
        options.IncludeEmbeddedImages = false;

        // Act: extract with images suppressed
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: nothing was written, the suppression is recorded, and no link points at nothing
        Assert.False(Directory.Exists(Path.Combine(scratch, "images"))
            && Directory.GetFiles(Path.Combine(scratch, "images")).Length > 0);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0201");
        Assert.Contains(result.Gaps, gap => gap.Target == "images/" && gap.Scope == GapScope.NotAttempted);
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.DoesNotContain("](images/", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a scanned PDF with no text layer degrades with an honest gap rather than an empty document.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_ScannedPdfWithNoTextLayer_DegradesWithHonestGap()
    {
        // Arrange: a document whose only page is a full-page image and carries no glyphs
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "scanned.pdf", PdfFixtures.NoTextLayer());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the scanned document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run degrades rather than failing, and says plainly why there is no text
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDF0003");
        var gap = Assert.Single(result.Gaps, candidate =>
            candidate.Target == "content.md" && candidate.Kind == GapKind.Text);
        Assert.Contains("text layer", gap.Reason, StringComparison.Ordinal);

        // Assert: the page's embedded image is still extracted, so the run is degraded, not useless
        Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a page-rendering request degrades with DD0301 and names the class of package that provides it.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_PagesRequested_DegradesWithDD0301AndNamesPackageClass()
    {
        // Arrange: a plain text document extracted with rendered pages demanded
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", PdfFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");
        var options = FixedOptions();
        options.RenderPages = true;

        // Act: extract with a capability this package does not provide
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the engine records the unmet capability and no page was rendered
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0301");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0702");
        Assert.Empty(result.PagePaths);

        // Assert: the summary states where the capability lives, declaratively. The summary wraps
        // its prose across lines, so the text is compared with whitespace normalized — a
        // line-oriented match would pass or fail on where the wrap happened to fall.
        var summary = Normalize(await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct));
        Assert.Contains("does not rasterize pages", summary, StringComparison.Ordinal);
        Assert.Contains("page-rendering extractor package", summary, StringComparison.Ordinal);

        // Assert: and never instructs the reader to perform an installation that cannot succeed
        Assert.DoesNotContain("dotnet add package", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("install ", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not yet available", summary, StringComparison.OrdinalIgnoreCase);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves an encrypted PDF fails structurally without an exception escaping to the caller.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_EncryptedPdf_FailsStructurallyAndWritesFullLayout()
    {
        // Arrange: a structurally valid PDF whose content is behind a security handler
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "protected.pdf", PdfFixtures.Encrypted());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the protected document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the failure is structured and coded, and the full layout is still written
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.ExtractorFailed, result.Failure.Kind);
        Assert.Contains("encrypted", result.Failure.Explanation, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a malformed PDF fails structurally without an exception escaping to the caller.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_MalformedPdf_FailsStructurallyAndWritesFullLayout()
    {
        // Arrange: a truncated document that looks like a PDF but has no cross-reference table
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "broken.pdf", PdfFixtures.Malformed());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the malformed document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the failure is structured, and the layout and its explaining gaps are still written
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal(ExtractionFailureKind.ExtractorFailed, result.Failure?.Kind);
        Assert.Contains(result.Gaps, gap => gap.Target == "content.md");
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a PDF with no pages completes with explicit gaps rather than failing or throwing.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_ZeroPagePdf_CompletesWithExplicitGaps()
    {
        // Arrange: a valid document that declares no pages at all
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "empty.pdf", PdfFixtures.ZeroPage());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract the empty document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: no failure, and the absence of content is stated rather than left to be inferred
        Assert.NotEqual(ExtractionOutcome.Failed, result.Outcome);
        Assert.Null(result.Failure);
        Assert.Contains(result.Gaps, gap =>
            gap.Target == "content.md" && gap.Reason.Contains("no pages", StringComparison.Ordinal));
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a requested page range restricts extraction to that range.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_PageRangeRequested_RestrictsToRange()
    {
        // Arrange: a three-page document extracted with only the middle page requested
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "multi.pdf", PdfFixtures.MultiPage(3));
        var scratch = Path.Combine(temp.Path, "out");
        var options = FixedOptions();
        options.Pages = new PageRange(2, 2);

        // Act: extract the requested range
        await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: only the requested page's text is present, and the others are absent
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Page 2 marker", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 1 marker", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 3 marker", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves two runs over the same document at a fixed timestamp produce byte-identical artifacts.
    /// </summary>
    [Fact]
    public async Task DocDownPdf_Extract_SameDocumentTwice_ProducesByteIdenticalArtifacts()
    {
        // Arrange: a document with both text and images, and a stable scratch folder written twice
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "mixed.pdf", PdfFixtures.WithJpeg2000Image());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run twice with the same fixed timestamp, snapshotting the first run's artifacts
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        var snapshot = Path.Combine(temp.Path, "snapshot");
        CopyTree(scratch, snapshot);
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: every artifact, including the gap text and the extracted images, is byte-identical
        var expected = Directory.GetFiles(snapshot, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(expected);
        foreach (var file in expected)
        {
            ContractAssert.FileEquals(file, Path.Combine(scratch, Path.GetRelativePath(snapshot, file)));
        }
    }

    /// <summary>
    ///     Proves every extraction scenario reconciles cleanly against the filesystem.
    /// </summary>
    /// <param name="scenario">The fixture key identifying the document under test.</param>
    [Theory]
    [InlineData("simple")]
    [InlineData("multiPage")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("undecodable")]
    [InlineData("jpeg2000")]
    [InlineData("scanned")]
    [InlineData("zeroPage")]
    [InlineData("malformed")]
    [InlineData("encrypted")]
    public async Task DocDownPdf_Extract_AnyOutcome_ReportedGapsMatchFilesystem(string scenario)
    {
        // Arrange: the named fixture and a fresh scratch folder
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, scenario + ".pdf", FixtureFor(scenario));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract whatever this scenario produces, clean or degraded or failed
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: whatever the outcome, the record and the disk tell the same story
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves no PdfPig type appears anywhere on this package's public API surface.
    /// </summary>
    /// <remarks>
    ///     PdfPig is pre-1.0 and changes its public API on minor versions. Confining it behind this
    ///     package's own surface is what keeps that churn from reaching consumers, and a
    ///     hand-maintained rule would decay; this test makes the containment a build failure instead.
    /// </remarks>
    [Fact]
    public void DocDownPdf_PublicApi_AllPublicMembers_ExposeNoPdfPigTypes()
    {
        // Arrange: every type this package exports
        var assembly = typeof(PdfDocumentExtractor).Assembly;
        var leaks = new List<string>();

        // Act: walk every public signature and collect any type that comes from PdfPig
        foreach (var type in assembly.GetExportedTypes())
        {
            foreach (var member in type.GetMembers())
            {
                foreach (var referenced in SignatureTypes(member))
                {
                    if (IsPdfPigType(referenced))
                    {
                        leaks.Add($"{type.FullName}.{member.Name} -> {referenced.FullName}");
                    }
                }
            }
        }

        // Assert: the public surface mentions no PdfPig type at all
        Assert.True(leaks.Count == 0, "PdfPig types reached the public API surface:\n  " + string.Join("\n  ", leaks));
    }

    /// <summary>
    ///     Proves the package's build output carries no native assets or runtime-specific folders.
    /// </summary>
    /// <remarks>
    ///     Inspects the package project's own output rather than the test host's. The test host
    ///     legitimately ships a <c>runtimes/</c> folder from its own test-platform dependencies, so
    ///     asserting against it would test the wrong assembly and fail for the wrong reason.
    /// </remarks>
    [Fact]
    public void DocDownPdf_Package_BuildOutput_ContainsNoNativeAssets()
    {
        // Arrange: locate the package project's own build output for this runtime
        var output = PackageOutputFolder();

        // Assert: no runtime-identifier folder exists, which is what makes the package RID-agnostic
        Assert.False(Directory.Exists(Path.Combine(output, "runtimes")), $"a runtimes/ folder was produced in '{output}'");

        // Assert: no unmanaged binaries of any platform are present
        foreach (var extension in new[] { "*.so", "*.dylib" })
        {
            Assert.Empty(Directory.GetFiles(output, extension, SearchOption.AllDirectories));
        }

        // Assert: every library the package builds alongside itself is a managed assembly
        foreach (var library in Directory.GetFiles(output, "*.dll", SearchOption.AllDirectories))
        {
            Assert.True(IsManagedAssembly(library), $"'{Path.GetFileName(library)}' is not a managed assembly");
        }

        // Assert: the project declares no runtime identifier, so no RID-specific asset can appear later
        var project = File.ReadAllText(Path.Combine(RepositoryRoot(), "src",
            "DemaConsulting.DocDown.Pdf", "DemaConsulting.DocDown.Pdf.csproj"));
        Assert.DoesNotContain("<RuntimeIdentifier", project, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the produced NuGet package carries no native assets and no runtime-specific folder.
    /// </summary>
    /// <remarks>
    ///     Inspects the real <c>.nupkg</c> rather than the build output: it packs the package project
    ///     to a temporary folder and opens the resulting archive. This is the assertion that a
    ///     consumer restoring the package gets a fully managed, runtime-identifier-agnostic library —
    ///     the property that keeps DocDown.Pdf deployable anywhere the runtime is, which the separate
    ///     rendering package (that does carry native assets) exists to preserve.
    /// </remarks>
    [Fact]
    public void DocDownPdf_Package_Nupkg_ContainsNoNativeAssets()
    {
        // Arrange: pack the package project to a temporary folder and locate the produced .nupkg
        using var temp = new TempScratch();
        var nupkg = NuGetPackHelper.PackAndLocateNupkg(PdfPackageProject(), "DemaConsulting.DocDown.Pdf", temp.Path);

        using var archive = ZipFile.OpenRead(nupkg);
        var entries = archive.Entries.Select(entry => entry.FullName).ToList();

        // Assert: no runtimes/ tree, which is what would carry per-RID native assets
        Assert.DoesNotContain(entries, name => name.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase));

        // Assert: no unmanaged binaries of any platform anywhere in the package
        foreach (var suffix in new[] { ".so", ".dylib" })
        {
            Assert.DoesNotContain(entries, name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        // Assert: every managed library the package ships under lib/ is a real managed assembly
        var extractDir = Path.Combine(temp.Path, "extract");
        Directory.CreateDirectory(extractDir);
        foreach (var entry in archive.Entries.Where(candidate =>
                     candidate.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                     && candidate.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var target = Path.Combine(extractDir, Path.GetFileName(entry.FullName));
            entry.ExtractToFile(target, overwrite: true);
            Assert.True(IsManagedAssembly(target), $"'{entry.FullName}' is not a managed assembly");
        }
    }

    /// <summary>
    ///     Proves concurrent packs of the same project do not collide on the shared intermediate nuspec.
    /// </summary>
    /// <remarks>
    ///     Reproduces, deterministically, the contention the suite hits when this packaging test runs
    ///     once per target framework in separate processes: without isolation, two packs of one
    ///     project race on <c>obj/Release/&lt;id&gt;.nuspec</c> and one fails with "the process cannot
    ///     access the file … it is being used by another process." Each pack goes through
    ///     <see cref="NuGetPackHelper"/>, which gives every invocation a unique nuspec directory — the
    ///     load-bearing fix — so all packs succeed and each yields its own package. A regression guard
    ///     rather than a retry: it fails loudly if the isolation is ever removed.
    /// </remarks>
    [Fact]
    public async Task DocDownPdf_Package_ConcurrentPacksOfSameProject_DoNotCollide()
    {
        // Arrange: pack the same project several times at once, each into its own output folder
        using var temp = new TempScratch();
        var project = PdfPackageProject();
        const int concurrency = 3;

        // Act: run the packs concurrently through the shared helper and collect the package paths
        var tasks = Enumerable.Range(0, concurrency)
            .Select(index => Task.Run(() => NuGetPackHelper.PackAndLocateNupkg(
                project, "DemaConsulting.DocDown.Pdf", Path.Combine(temp.Path, $"pack-{index}")), Ct))
            .ToArray();
        var packages = await Task.WhenAll(tasks);

        // Assert: every pack succeeded and produced its own distinct package file
        Assert.All(packages, package => Assert.True(File.Exists(package), $"expected '{package}' to exist"));
        Assert.Equal(concurrency, packages.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    ///     Resolves the absolute path of the DocDown.Pdf package project.
    /// </summary>
    /// <returns>The absolute path to <c>DemaConsulting.DocDown.Pdf.csproj</c>.</returns>
    /// <remarks>Anchored on the repository root so the packaging tests locate the project independently of the CWD. Pure.</remarks>
    private static string PdfPackageProject() =>
        Path.Combine(RepositoryRoot(), "src", "DemaConsulting.DocDown.Pdf", "DemaConsulting.DocDown.Pdf.csproj");

    /// <summary>
    ///     Locates the package project's build output folder for the running target framework.
    /// </summary>
    /// <returns>The absolute path of the package's own output folder.</returns>
    /// <remarks>
    ///     Derived from the repository root and the test host's own target-framework folder name, so
    ///     the assertion follows whichever framework the test is currently running under rather than
    ///     hard-coding one. Read-only I/O.
    /// </remarks>
    private static string PackageOutputFolder()
    {
        var testOutput = Path.GetDirectoryName(typeof(DocDownPdfTests).Assembly.Location)!;
        var framework = new DirectoryInfo(testOutput).Name;
        return Path.Combine(RepositoryRoot(), "src", "DemaConsulting.DocDown.Pdf", "bin", "Release", framework);
    }

    /// <summary>
    ///     Walks up from the test output to the repository root.
    /// </summary>
    /// <returns>The absolute path of the folder holding the solution file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no ancestor holds the solution file.</exception>
    /// <remarks>Anchored on the solution file so the walk cannot stop at a coincidentally named folder. Read-only I/O.</remarks>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(DocDownPdfTests).Assembly.Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocDown.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test output folder.");
    }

    /// <summary>
    ///     Collapses all whitespace in a text into single spaces.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text with every whitespace run reduced to one space.</returns>
    /// <remarks>
    ///     The summary wraps its prose to a fixed width, so a phrase it contains may be split across
    ///     two lines. Normalizing before matching asserts on what the summary says rather than on
    ///     where the wrap happened to fall — a line-oriented match would otherwise report a false
    ///     absence. Pure.
    /// </remarks>
    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    ///     Proves the extractor contributes runnable self-validation cases that describe its own behavior.
    /// </summary>
    [Fact]
    public void DocDownPdf_SelfValidation_RegisteredEngine_ReportsPdfCases()
    {
        // Arrange: an engine with the PDF backend registered
        var engine = BuildEngine();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        // Act: enumerate and run every case this backend contributes
        var cases = engine.GetSelfTestCases().Where(candidate => candidate.Category == "pdf").ToList();
        var results = cases.Select(candidate => candidate.Run(context)).ToList();

        // Assert: the parse round trip passes here, and the rendering case reports a reasoned skip
        Assert.Equal(2, cases.Count);
        Assert.Contains(results, result => result.Status == SelfTestStatus.Passed);
        var skipped = Assert.Single(results, result => result.Status == SelfTestStatus.Skipped);
        Assert.Contains("renderedPages", skipped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(results, result => result.Status == SelfTestStatus.Failed);
    }

    /// <summary>
    ///     Builds an engine with only the PDF backend registered.
    /// </summary>
    /// <returns>A configured engine.</returns>
    /// <remarks>Registers through the public extension so every test also exercises the seam a host uses.</remarks>
    private static DocDownEngine BuildEngine() => new DocDownBuilder().AddPdf().Build();

    /// <summary>
    ///     Creates options carrying the fixed timestamp for reproducible output.
    /// </summary>
    /// <returns>An options instance with a fixed timestamp.</returns>
    /// <remarks>Injecting the timestamp is what makes the determinism scenario a real comparison.</remarks>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Materializes a generated fixture as a file on disk.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The file name, whose extension drives format detection.</param>
    /// <param name="bytes">The generated document bytes.</param>
    /// <returns>The absolute path of the written file.</returns>
    /// <remarks>
    ///     The name always ends in <c>.pdf</c> because detection trusts the file extension; writing
    ///     the fixture out rather than passing a stream also exercises the path a host actually uses.
    /// </remarks>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    ///     Resolves a scenario key to its generated fixture bytes.
    /// </summary>
    /// <param name="scenario">The scenario key.</param>
    /// <returns>The fixture bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when the scenario key is unknown.</exception>
    /// <remarks>Centralizes the reconciliation theory's matrix so the test stays declarative.</remarks>
    private static byte[] FixtureFor(string scenario) => scenario switch
    {
        "simple" => PdfFixtures.SimpleText(),
        "multiPage" => PdfFixtures.MultiPage(3),
        "jpeg" => PdfFixtures.WithEmbeddedJpeg(),
        "png" => PdfFixtures.WithEmbeddedPng(),
        "undecodable" => PdfFixtures.WithUndecodableImage(),
        "jpeg2000" => PdfFixtures.WithJpeg2000Image(),
        "scanned" => PdfFixtures.NoTextLayer(),
        "zeroPage" => PdfFixtures.ZeroPage(),
        "malformed" => PdfFixtures.Malformed(),
        "encrypted" => PdfFixtures.Encrypted(),
        _ => throw new ArgumentException($"Unknown fixture scenario '{scenario}'.", nameof(scenario))
    };

    /// <summary>
    ///     Reads and parses the manifest from a completed extraction.
    /// </summary>
    /// <param name="scratch">The scratch folder to read from.</param>
    /// <returns>The parsed manifest; the caller disposes it.</returns>
    /// <remarks>Parsed as a document rather than deserialized so tests can assert on raw field spellings.</remarks>
    private static async Task<JsonDocument> ReadManifestAsync(string scratch) =>
        JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));

    /// <summary>
    ///     Copies a directory tree so a later run can be compared against it.
    /// </summary>
    /// <param name="source">The tree to copy.</param>
    /// <param name="destination">The folder to copy into.</param>
    /// <remarks>Used by the determinism scenario, which must compare every artifact, not just two.</remarks>
    private static void CopyTree(string source, string destination)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>
    ///     Enumerates every type that appears in a member's signature.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The parameter, return, property, field, and generic argument types involved.</returns>
    /// <remarks>
    ///     Generic arguments are included because a leak hidden inside a collection type would be as
    ///     real as a bare parameter. Pure.
    /// </remarks>
    private static IEnumerable<Type> SignatureTypes(System.Reflection.MemberInfo member)
    {
        var types = new List<Type>();
        switch (member)
        {
            case System.Reflection.MethodBase method:
                types.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                if (method is System.Reflection.MethodInfo info)
                {
                    types.Add(info.ReturnType);
                }

                break;
            case System.Reflection.PropertyInfo property:
                types.Add(property.PropertyType);
                break;
            case System.Reflection.FieldInfo field:
                types.Add(field.FieldType);
                break;
            default:
                break;
        }

        return types.SelectMany(Expand);
    }

    /// <summary>
    ///     Expands a type into itself plus its generic arguments and element type.
    /// </summary>
    /// <param name="type">The type to expand.</param>
    /// <returns>The type and every type nested inside it.</returns>
    /// <remarks>Pure.</remarks>
    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            yield return argument;
        }

        if (type.HasElementType && type.GetElementType() is { } element)
        {
            yield return element;
        }
    }

    /// <summary>
    ///     Determines whether a type comes from PdfPig.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type belongs to a PdfPig assembly or namespace.</returns>
    /// <remarks>
    ///     Tests both the assembly and the namespace because PdfPig ships several assemblies whose
    ///     names share the <c>UglyToad</c> root. Pure.
    /// </remarks>
    private static bool IsPdfPigType(Type type) =>
        (type.Assembly.GetName().Name?.StartsWith("UglyToad", StringComparison.Ordinal) ?? false)
        || (type.Namespace?.StartsWith("UglyToad", StringComparison.Ordinal) ?? false);

    /// <summary>
    ///     Determines whether a file on disk is a managed assembly.
    /// </summary>
    /// <param name="path">The library path to test.</param>
    /// <returns><see langword="true"/> when the file carries a managed assembly manifest.</returns>
    /// <remarks>
    ///     Reading the assembly name is the definitive test: a native library has no managed manifest
    ///     and raises <see cref="BadImageFormatException"/>. Read-only I/O.
    /// </remarks>
    private static bool IsManagedAssembly(string path)
    {
        try
        {
            System.Reflection.AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
