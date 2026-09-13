using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Subsystem-integration tests for the Output subsystem, exercising <see cref="ScratchFolder"/>,
///     <see cref="ExtractionSink"/>, <see cref="ContentWriter"/>, <see cref="ManifestWriter"/>,
///     <see cref="SummaryWriter"/>, and <see cref="ContractVerifier"/> together at the subsystem
///     boundary.
/// </summary>
/// <remarks>
///     These tests drive the real write pipeline directly — preparing a scratch folder, writing
///     through the sink, then finalizing content, manifest, and summary — and reconcile the result
///     against disk. Each is named for the subsystem requirement it evidences: artifact layout,
///     summary and manifest content, content document, image and page resources, completeness
///     ledger, gap explanation, path safety, determinism, and contract verification.
/// </remarks>
public class OutputTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a successful write creates the always-present summary and manifest (ArtifactLayout).
    /// </summary>
    [Fact]
    public async Task Output_ArtifactLayout_SuccessfulWrite_CreatesSummaryAndManifest()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing only text
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: the two mandatory files exist and the manifest resolves every ledger slot
        Assert.True(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.True(File.Exists(Path.Combine(scratch, "manifest.json")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the summary carries its mandatory sections (SummaryContent).
    /// </summary>
    [Fact]
    public async Task Output_SummaryContent_Written_ContainsMandatorySections()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and read the summary
        await RunPipelineAsync(temp, scratch, WriteText);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the fixed mandatory sections are present
        Assert.Contains("DocDown Extraction Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Scratch folder", summary, StringComparison.Ordinal);
        Assert.Contains("Backend", summary, StringComparison.Ordinal);
        Assert.Contains("Completeness", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the manifest parses and declares its schema version (ManifestContent).
    /// </summary>
    [Fact]
    public async Task Output_ManifestContent_Written_ParsesWithSchemaVersion()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and parse the manifest
        await RunPipelineAsync(temp, scratch, WriteText);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var root = document.RootElement;

        // Assert: the manifest parses with the expected schema version and mandatory blocks
        Assert.Equal("1.2", root.GetProperty("schemaVersion").GetString());
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("artifacts", out _));
    }

    /// <summary>
    ///     Proves written text produces the content document (ContentDocument).
    /// </summary>
    [Fact]
    public async Task Output_ContentDocument_TextWritten_ProducesContentMd()
    {
        // Arrange: a scratch folder and a text write with distinctive content
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing text
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: content.md exists and carries the written text
        var contentPath = Path.Combine(scratch, "content.md");
        Assert.True(File.Exists(contentPath));
        Assert.Contains("Hello from the output subsystem", await File.ReadAllTextAsync(contentPath, Ct), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves identical image bytes are stored once and reference-counted (ImageResources).
    /// </summary>
    [Fact]
    public async Task Output_ImageResources_DuplicateBytes_AreDeduplicated()
    {
        // Arrange: a scratch folder and a write that adds the same image bytes twice
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing two identical images plus text
        await RunPipelineAsync(temp, scratch, WriteDuplicateImages);

        // Assert: only one image file exists on disk and the manifest records two references to it
        Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = document.RootElement.GetProperty("images");
        Assert.Equal(1, images.GetArrayLength());
        Assert.Equal(2, images[0].GetProperty("references").GetInt32());
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a rendered page is named by its document page number (PageResources).
    /// </summary>
    [Fact]
    public async Task Output_PageResources_PageWritten_IsNamedByPageNumber()
    {
        // Arrange: a scratch folder and a write that renders page three
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline rendering a page
        await RunPipelineAsync(temp, scratch, WritePage);

        // Assert: the page file is named by its 1-based document page number
        Assert.True(File.Exists(Path.Combine(scratch, "pages", "page0003.png")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var pages = document.RootElement.GetProperty("pages");
        Assert.Equal(3, pages[0].GetProperty("pageNumber").GetInt32());
    }

    /// <summary>
    ///     Proves reconciliation marks written content as present in the ledger (CompletenessLedger).
    /// </summary>
    [Fact]
    public async Task Output_CompletenessLedger_TextWritten_MarksContentPresent()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and capture the reconciliation
        var (_, reconciliation) = await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: the always-present and content ledger entries are marked present with no gaps
        Assert.Equal(ArtifactStatus.Present, reconciliation.Ledger.Summary.Status);
        Assert.Equal(ArtifactStatus.Present, reconciliation.Ledger.Content.Status);
        Assert.True(reconciliation.IsComplete);
    }

    /// <summary>
    ///     Proves an unexplained missing artifact is caught and explained by a synthesized gap (GapExplanation).
    /// </summary>
    [Fact]
    public async Task Output_GapExplanation_MissingContent_SynthesizesExplainedGap()
    {
        // Arrange: a scratch folder and a write that produces no content at all
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline claiming images were found but writing none, leaving them unexplained
        var (_, reconciliation) = await RunPipelineAsync(temp, scratch, WriteSilentImages);

        // Assert: reconciliation is incomplete and every gap carries a non-empty reason
        Assert.False(reconciliation.IsComplete);
        Assert.NotEmpty(reconciliation.Gaps);
        Assert.All(reconciliation.Gaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap.Reason)));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a malicious image name is contained under the images folder (PathSafety).
    /// </summary>
    [Fact]
    public async Task Output_PathSafety_MaliciousImageName_IsContained()
    {
        // Arrange: a prepared scratch folder and sink, plus a hostile preferred image name
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, new ExtractionOptions());
        using var bytes = new MemoryStream([1, 2, 3, 4]);

        // Act: add an image whose preferred name attempts a directory traversal
        var relativePath = await sink.AddImageAsync(bytes, new ImageHint("../../etc/passwd", "image/png"), Ct);

        // Assert: the allocated path stays under images/ with no traversal, and the file lands inside the scratch folder
        Assert.StartsWith("images/", relativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("..", relativePath, StringComparison.Ordinal);
        var onDisk = Path.Combine(folder.AbsolutePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(onDisk));
    }

    /// <summary>
    ///     Proves two runs with a fixed timestamp produce byte-identical output (Determinism).
    /// </summary>
    [Fact]
    public async Task Output_Determinism_FixedTimestamp_ProducesByteIdenticalOutput()
    {
        // Arrange: a stable scratch folder written twice with the same fixed timestamp
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the pipeline twice, snapshotting the first run's files
        await RunPipelineAsync(temp, scratch, WriteText);
        var firstSummary = Path.Combine(temp.Path, "first-summary.txt");
        var firstManifest = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(scratch, "summary.txt"), firstSummary);
        File.Copy(Path.Combine(scratch, "manifest.json"), firstManifest);
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: both the summary and the manifest are byte-identical between runs
        ContractAssert.FileEquals(firstSummary, Path.Combine(scratch, "summary.txt"));
        ContractAssert.FileEquals(firstManifest, Path.Combine(scratch, "manifest.json"));
    }

    /// <summary>
    ///     Proves an honest extraction passes the contract verifier with no violations (ContractVerification).
    /// </summary>
    [Fact]
    public async Task Output_ContractVerification_HonestExtraction_ReportsNoViolations()
    {
        // Arrange: a scratch folder written honestly with text and an image
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and verify the folder against its manifest
        await RunPipelineAsync(temp, scratch, WriteTextAndImage);
        var violations = ContractVerifier.Verify(scratch);

        // Assert: an honest extraction yields no violations
        Assert.Empty(violations);
    }

    /// <summary>
    ///     Proves the contract verifier detects a file on disk that the manifest does not list (ContractVerification).
    /// </summary>
    [Fact]
    public async Task Output_ContractVerification_UnlistedFile_IsDetected()
    {
        // Arrange: an honest extraction into which an extra, unlisted image is smuggled
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");
        await RunPipelineAsync(temp, scratch, WriteTextAndImage);
        await File.WriteAllTextAsync(Path.Combine(scratch, "images", "rogue.png"), "not listed", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the unlisted file is reported
        Assert.Contains(violations, violation => violation.Code == "DD0717");
    }

    /// <summary>
    ///     Proves two images with different provenance are each recorded honestly (ImageProvenance).
    /// </summary>
    [Fact]
    public async Task Output_ImageProvenance_MixedTransforms_ManifestRecordsEachHonestly()
    {
        // Arrange: a scratch folder and a write that adds one passthrough and one re-encoded image
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline through the real sink, manifest, and summary writers
        await RunPipelineAsync(temp, scratch, WriteMixedTransformImages);

        // Assert: the two images are labeled distinctly, in allocation order, and the folder verifies
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = document.RootElement.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal("passthrough", images[0].GetProperty("transform").GetString());
        Assert.Equal("decodedToPng", images[1].GetProperty("transform").GetString());
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Runs the output pipeline for a write action and returns the scratch folder and reconciliation.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="scratchPath">The requested scratch folder path.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <returns>The absolute scratch folder and the reconciliation result.</returns>
    /// <remarks>Drives the real writers so the tests exercise the Output subsystem at its boundary.</remarks>
    private static async Task<(string Scratch, ReconciliationResult Reconciliation)> RunPipelineAsync(
        TempScratch temp, string scratchPath, Func<IExtractionSink, ValueTask> write)
    {
        var options = new ExtractionOptions();
        var folder = ScratchFolder.Prepare(scratchPath, options.ScratchFolder);
        var sink = new ExtractionSink(folder, options);

        // Let the scenario write through the sink, then finalize content, manifest, metadata, and summary
        await write(sink);
        var content = await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);
        var report = BuildReport(temp, options);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await ManifestWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        await MetadataWriter.WriteAsync(folder, sink, Ct);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        return (folder.AbsolutePath, reconciliation);
    }

    /// <summary>
    ///     Builds a consistent extraction report for driving the output writers.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <returns>A report whose selected backend is consistent across summary and manifest.</returns>
    /// <remarks>Uses a fixed timestamp so determinism assertions are meaningful; the source is a real file to avoid an undisposed stream.</remarks>
    private static ExtractionReport BuildReport(TempScratch temp, ExtractionOptions options)
    {
        var descriptor = new ExtractorDescriptor(
            "text", "Text (stub)", [DocumentFormat.Text], ExtractorCapabilities.Text, 0);
        var trace = new[]
        {
            new CandidateVerdict("text", "Text (stub)", 0, CandidateOutcome.Selected, "selected for the output test")
        };
        var selection = new SelectionResult(
            descriptor, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.Text, trace, null);
        var environment = new ExtractionEnvironment("test-os", "x64", "test-runtime", "test-rid", []);
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(
            ExtractionOutcome.Succeeded, source, "0000", detection, selection, environment, options, FixedTimestamp, null);
    }

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    /// <remarks>The canonical simple write used by most output scenarios.</remarks>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nHello from the output subsystem.\n", CancellationToken.None);

    /// <summary>
    ///     Writes text and two identical images to exercise deduplication.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Both images share the same bytes so the sink must store them once.</remarks>
    private static async ValueTask WriteDuplicateImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith images.\n", CancellationToken.None);
        var bytes = new byte[] { 9, 8, 7, 6, 5 };
        using var first = new MemoryStream(bytes);
        await sink.AddImageAsync(first, new ImageHint("logo", "image/png"), CancellationToken.None);
        using var second = new MemoryStream(bytes);
        await sink.AddImageAsync(second, new ImageHint("logo", "image/png"), CancellationToken.None);
    }

    /// <summary>
    ///     Writes text and renders document page three.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Renders a specific page number so the naming can be asserted.</remarks>
    private static async ValueTask WritePage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith a rendered page.\n", CancellationToken.None);
        using var png = new MemoryStream([1, 2, 3, 4, 5, 6]);
        await sink.AddPageAsync(3, png, CancellationToken.None);
    }

    /// <summary>
    ///     Writes text and a single image for an honest, verifiable extraction.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Produces both a content document and an image resource for contract verification.</remarks>
    private static async ValueTask WriteTextAndImage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nHonest content.\n", CancellationToken.None);
        using var png = new MemoryStream([10, 20, 30, 40]);
        await sink.AddImageAsync(png, new ImageHint("figure", "image/png"), CancellationToken.None);
    }

    /// <summary>
    ///     Writes text and claims images were found without writing or explaining them.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Leaves an unexplained image absence that reconciliation must catch with a synthesized gap.</remarks>
    private static async ValueTask WriteSilentImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nText present, images silently missing.\n", CancellationToken.None);
        sink.ReportFound(GapKind.Images, 2);
    }

    /// <summary>
    ///     Writes text and two images with different reported provenance.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>
    ///     The two images carry distinct bytes so deduplication does not collapse them, letting the
    ///     subsystem prove it records each image's own provenance rather than one blanket claim.
    /// </remarks>
    private static async ValueTask WriteMixedTransformImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith images of differing provenance.\n", CancellationToken.None);
        using var verbatim = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);
        await sink.AddImageAsync(
            verbatim, new ImageHint("photo", "image/jpeg", Transform: ImageTransform.Passthrough), CancellationToken.None);
        using var reEncoded = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);
        await sink.AddImageAsync(
            reEncoded, new ImageHint("chart", "image/png", Transform: ImageTransform.DecodedToPng), CancellationToken.None);
    }
}
