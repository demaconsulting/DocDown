using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="SummaryWriter"/>, proving every mandatory section is always
///     present, the absolute scratch path leads the header, the backend is named, the environment
///     is reported, gaps are enumerated, a failure explanation is reproduced verbatim, and the text
///     is deterministic.
/// </summary>
/// <remarks>
///     These tests drive a real <see cref="ExtractionSink"/> and the reconciliation of
///     <see cref="ManifestWriter"/> (documented dependencies of the summary) over a prepared
///     <see cref="ScratchFolder"/>, then render with <see cref="SummaryWriter.WriteAsync"/>. Each is
///     named for the unit requirement it evidences: mandatory sections, absolute scratch path,
///     backend named, environment reported, gaps enumerated, failure explained, and deterministic
///     text.
/// </remarks>
public class SummaryWriterTests
{
    /// <summary>A fixed timestamp used to make the rendered summary byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves every mandatory section is present even when there is nothing to report (MandatorySections).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_CleanRun_ContainsEveryMandatorySection()
    {
        // Arrange: a clean successful text run with no gaps
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the fixed sections all appear, and empty sections state their emptiness in words
        Assert.Contains("DocDown Extraction Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Source document", summary, StringComparison.Ordinal);
        Assert.Contains("Detected format", summary, StringComparison.Ordinal);
        Assert.Contains("Backend", summary, StringComparison.Ordinal);
        Assert.Contains("Environment", summary, StringComparison.Ordinal);
        Assert.Contains("Layout", summary, StringComparison.Ordinal);
        Assert.Contains("What WAS extracted", summary, StringComparison.Ordinal);
        Assert.Contains("What was NOT extracted", summary, StringComparison.Ordinal);
        Assert.Contains("No gaps: everything requested was extracted.", summary, StringComparison.Ordinal);
        Assert.Contains("Completeness", summary, StringComparison.Ordinal);
        Assert.Contains("Diagnostics", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the absolute scratch-folder path appears in the leading header block (AbsoluteScratchPath).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_HeaderBlock_ContainsAbsoluteScratchPath()
    {
        // Arrange: a prepared scratch folder rendered into a summary
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);
        var summary = await FinalizeSummaryAsync(temp, folder, sink, ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the summary states the exact absolute scratch path
        Assert.Contains(folder.AbsolutePath, summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the summary names the selected backend (BackendNamed).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_SuccessfulRun_NamesSelectedBackend()
    {
        // Arrange: a successful run whose selection chose the text backend
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the backend block names the selected backend the manifest also records
        Assert.Contains("text", summary, StringComparison.Ordinal);
        Assert.Contains("Text (stub)", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the environment block reports the base runtime and contributed facts (EnvironmentReported).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithEnvironmentFacts_ReportsRuntimeAndFacts()
    {
        // Arrange: a run whose environment carries a distinctive contributed fact
        using var temp = new TempScratch();
        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [new EnvironmentFact("TestBackend", "renderer", "libtxt 4.2 present", true)]);
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Succeeded, SuccessSelection(), null, environment);

        // Assert: the base runtime lines and the contributed fact are reported
        Assert.Contains("Operating system", summary, StringComparison.Ordinal);
        Assert.Contains("TestOS 1.0", summary, StringComparison.Ordinal);
        Assert.Contains("renderer", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves each gap is enumerated with its identifier, target, and reason (GapsEnumerated).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnexplainedAbsence_EnumeratesSynthesizedGap()
    {
        // Arrange: a run that claims images were found but writes none, forcing a synthesized gap
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(temp, WriteSilentImages, ExtractionOutcome.Degraded, SuccessSelection(), null);

        // Assert: the gap appears with its dense identifier, its target, and a non-empty reason
        Assert.Contains("[GAP-1]", summary, StringComparison.Ordinal);
        Assert.Contains("images/", summary, StringComparison.Ordinal);
        Assert.Contains("Reason :", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a failure's explanation is reproduced verbatim in the summary (FailureExplained).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_FailedRun_ReproducesFailureExplanationVerbatim()
    {
        // Arrange: a failed run whose failure carries a distinctive multi-line explanation
        using var temp = new TempScratch();
        const string explanation =
            "The source document could not be read.\nDetected format: text (text/plain)\n\nRemedy: verify the file exists.";
        var failure = new ExtractionFailure(
            ExtractionFailureKind.SourceUnreadable, "DD0502", "Could not read the source.", explanation, [], "verify the file exists.");
        var selection = new SelectionResult(
            null, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.None, [], failure);

        // Act: render the failed summary
        var summary = await RenderSummaryAsync(temp, _ => ValueTask.CompletedTask, ExtractionOutcome.Failed, selection, failure);

        // Assert: the carefully composed explanation reaches the reader exactly as authored
        Assert.Contains(explanation, summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves two renders of the same content are byte-identical (DeterministicText).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalSummary()
    {
        // Arrange: a prepared folder and a fixed report rendered once, then snapshotted
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);
        var report = BuildReport(temp, Options(), ExtractionOutcome.Succeeded, SuccessSelection(), null, DefaultEnvironment());
        var content = await ContentWriter.WriteAsync(sink, Options().ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);

        // Act: render twice into the same folder, snapshotting the first render
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        var firstSnapshot = Path.Combine(temp.Path, "first-summary.txt");
        File.Copy(Path.Combine(folder.AbsolutePath, "summary.txt"), firstSnapshot);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);

        // Assert: the deterministic (\n endings, no BOM, invariant culture) output is byte-identical
        ContractAssert.FileEquals(firstSnapshot, Path.Combine(folder.AbsolutePath, "summary.txt"));
    }

    /// <summary>
    ///     Proves the inlined author is the human author, not the producing application, for
    ///     PDF-shaped metadata that carries both an <c>author</c> and a distinct <c>creator</c>.
    /// </summary>
    /// <remarks>
    ///     The PDF backend emits <c>author</c> (the person) and <c>creator</c> (the producing
    ///     application). The summary must prefer <c>author</c> so it never names the application as
    ///     the author — the defect this test guards against.
    /// </remarks>
    [Fact]
    public async Task SummaryWriter_WriteAsync_PdfShapedMetadata_InlinesHumanAuthorNotApplication()
    {
        // Arrange: PDF-shaped metadata where author is the person and creator is the producing application
        using var temp = new TempScratch();
        var metadata = new DocumentMetadata(
            [
                new DocumentMetadataField("author", "The Document Author", MetadataProvenance.PdfDocumentInformation),
                new DocumentMetadataField("creator", "Sample Word Processor", MetadataProvenance.PdfDocumentInformation),
                new DocumentMetadataField("modified", "2026-05-04T00:00:00Z", MetadataProvenance.PdfDocumentInformation)
            ],
            []);

        // Act: render a summary whose sink reported the PDF-shaped metadata
        var summary = await RenderSummaryAsync(
            temp, sink => WriteTextWithMetadata(sink, metadata), ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the human author is inlined and the producing application never appears
        Assert.Contains("Author          : The Document Author", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample Word Processor", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves OPC-shaped metadata (no <c>author</c> field) falls back to <c>creator</c>, which is
    ///     the author for OPC backends, so their summary output is unchanged.
    /// </summary>
    /// <remarks>
    ///     Word, Excel, PowerPoint, and Visio emit no <c>author</c> field and a <c>creator</c> that
    ///     <em>is</em> the author. This regression guard proves the author-preferred lookup keeps
    ///     their output byte-identical.
    /// </remarks>
    [Fact]
    public async Task SummaryWriter_WriteAsync_OpcShapedMetadata_FallsBackToCreatorAsAuthor()
    {
        // Arrange: OPC-shaped metadata with only a creator field, which is the author for OPC backends
        using var temp = new TempScratch();
        var metadata = new DocumentMetadata(
            [
                new DocumentMetadataField("creator", "The Document Author", MetadataProvenance.OpcCoreProperties),
                new DocumentMetadataField("modified", "2026-05-04T00:00:00Z", MetadataProvenance.OpcCoreProperties)
            ],
            []);

        // Act: render a summary whose sink reported the OPC-shaped metadata
        var summary = await RenderSummaryAsync(
            temp, sink => WriteTextWithMetadata(sink, metadata), ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the creator is inlined as the author, preserving OPC output
        Assert.Contains("Author          : The Document Author", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the summary opens with a one-line plain-English gist composed only from established
    ///     facts, and names the content's structural features rather than only its size.
    /// </summary>
    /// <remarks>
    ///     The gist and the content outline are the two changes that answer a paste-in reader's first
    ///     question — what is this, and what is in it — which a filename and a character count do not.
    /// </remarks>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithContentFeatures_OpensWithGistAndOutlinesContent()
    {
        // Arrange: a run reporting document extent and the structure its content carries
        using var temp = new TempScratch();

        // Act: render the summary
        var summary = await RenderSummaryAsync(temp, WriteTextWithFeatures, ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the gist precedes the header block and the outline names the features, with the
        // singular form used for a count of one so "1 comments" can never reach a reader
        var gistIndex = summary.IndexOf("A 3-page plain-text document; extracted", StringComparison.Ordinal);
        var scratchIndex = summary.IndexOf("Scratch folder  : ", StringComparison.Ordinal);
        Assert.InRange(gistIndex, 0, scratchIndex);
        Assert.Contains("Contains 4 headings, 1 table, and 53 comments.", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the gist is omitted entirely when the detected format has no plain-English name,
    ///     rather than describing an unrecognized stream as some guessed kind of document.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnknownFormat_OmitsGistRatherThanGuessing()
    {
        // Arrange: a run whose detected format is unrecognized
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);

        var source = DocumentSource.FromFile(temp.CreateFile("source.bin", "hello"));
        var detection = new FormatDetection(DocumentFormat.Unknown, DetectionBasis.Extension, 0.1);
        var report = new ExtractionReport(
            ExtractionOutcome.Succeeded, source, "0000", detection, SuccessSelection(),
            DefaultEnvironment(), Options(), FixedTimestamp, null);

        // Act: render the summary
        var content = await ContentWriter.WriteAsync(sink, Options().ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);

        // Assert: the banner is followed straight by the header block, with no invented description
        Assert.Contains("==========================\n\nScratch folder  : ", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the image inventory is described in aggregate with a pointer to the manifest,
    ///     rather than one line per image, and that unplaced images are explained.
    /// </summary>
    /// <remarks>
    ///     A per-image inventory is the wrong default for a paste-in file; the manifest already
    ///     carries every per-image fact. An image that a layout or master references is explained
    ///     rather than left with an unexplained blank where its page number would be.
    /// </remarks>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithImages_SummarizesInAggregateAndExplainsTemplateImages()
    {
        // Arrange: two placed images and one referenced only by a template
        using var temp = new TempScratch();

        // Act: render the summary
        var summary = await RenderSummaryAsync(temp, WriteMixedImages, ExtractionOutcome.Succeeded, SuccessSelection(), null);

        // Assert: the set is described in aggregate, the template image is explained, and no
        // per-image line survives
        Assert.Contains("3 embedded images, 9 bytes total, spanning pages 1-2.", summary, StringComparison.Ordinal);
        Assert.Contains("One of them carries no page number because a layout, master, or stencil", summary, StringComparison.Ordinal); Assert.Contains("See manifest.json for the per-image inventory.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("images/0001-placed-one.png ", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the environment block keeps every backend-contributed fact and every unavailability,
    ///     but collapses the available-but-unused candidates into one counted line.
    /// </summary>
    /// <remarks>
    ///     Listing eight near-identical "available" lines for backends that never ran spends a
    ///     paste-in reader's budget on context; the manifest carries them unconditionally. An
    ///     unavailability is kept because it can explain a gap.
    /// </remarks>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnusedAvailableCandidates_CollapsesThemIntoOneCountedLine()
    {
        // Arrange: one backend fact, one unavailable candidate, and two available-but-unused candidates
        using var temp = new TempScratch();
        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [
                new EnvironmentFact("TestBackend", "renderer", "libtxt 4.2 present", true),
                new EnvironmentFact("Absent Backend", "backend.absent", "no native library", false, EnvironmentFactOrigin.CandidateAvailability),
                new EnvironmentFact("Unused One", "backend.unused-one", "available", true, EnvironmentFactOrigin.CandidateAvailability),
                new EnvironmentFact("Unused Two", "backend.unused-two", "available", true, EnvironmentFactOrigin.CandidateAvailability)
            ]);

        // Act: render the summary
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Succeeded, SuccessSelection(), null, environment);

        // Assert: the backend fact and the unavailability stay; the two available ones are counted
        Assert.Contains("libtxt 4.2 present", summary, StringComparison.Ordinal);
        Assert.Contains("no native library", summary, StringComparison.Ordinal);
        Assert.Contains("2 other registered backends were available but did not run; see manifest.json.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("backend.unused-one", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Renders a summary for a write action, outcome, selection, and optional failure.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="selection">The selection result to record.</param>
    /// <param name="failure">The failure to record, or <see langword="null"/> for a non-failed run.</param>
    /// <param name="environment">The environment to record, or <see langword="null"/> for the default.</param>
    /// <returns>The rendered summary text.</returns>
    /// <remarks>Drives the real writer chain so the summary reflects an authentic run.</remarks>
    private static async Task<string> RenderSummaryAsync(
        TempScratch temp, Func<IExtractionSink, ValueTask> write, ExtractionOutcome outcome,
        SelectionResult selection, ExtractionFailure? failure, ExtractionEnvironment? environment = null)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await write(sink);
        return await FinalizeSummaryAsync(temp, folder, sink, outcome, selection, failure, environment);
    }

    /// <summary>
    ///     Finalizes content, reconciles, and renders the summary for a prepared folder and sink.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink holding the written content.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="selection">The selection result to record.</param>
    /// <param name="failure">The failure to record, or <see langword="null"/>.</param>
    /// <param name="environment">The environment to record, or <see langword="null"/> for the default.</param>
    /// <returns>The rendered summary text.</returns>
    /// <remarks>A failed run passes no content, so content.md is honestly absent.</remarks>
    private static async Task<string> FinalizeSummaryAsync(
        TempScratch temp, ScratchFolder folder, ExtractionSink sink, ExtractionOutcome outcome,
        SelectionResult selection, ExtractionFailure? failure, ExtractionEnvironment? environment = null)
    {
        var report = BuildReport(temp, Options(), outcome, selection, failure, environment ?? DefaultEnvironment());
        var content = outcome == ExtractionOutcome.Failed
            ? null
            : await ContentWriter.WriteAsync(sink, Options().ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        return await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);
    }

    /// <summary>
    ///     Builds a consistent extraction report for the summary writer.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <param name="outcome">The recorded outcome.</param>
    /// <param name="selection">The recorded selection.</param>
    /// <param name="failure">The recorded failure, or <see langword="null"/>.</param>
    /// <param name="environment">The recorded environment.</param>
    /// <returns>The assembled report.</returns>
    /// <remarks>Uses a fixed timestamp and a real source file so the render is deterministic and disposable.</remarks>
    private static ExtractionReport BuildReport(
        TempScratch temp, ExtractionOptions options, ExtractionOutcome outcome,
        SelectionResult selection, ExtractionFailure? failure, ExtractionEnvironment environment)
    {
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(outcome, source, "0000", detection, selection, environment, options, FixedTimestamp, failure);
    }

    /// <summary>
    ///     Creates a successful selection naming the text backend.
    /// </summary>
    /// <returns>A selection result whose selected descriptor is the text backend.</returns>
    /// <remarks>Keeps the summary and manifest naming the same backend for cross-check consistency.</remarks>
    private static SelectionResult SuccessSelection()
    {
        var descriptor = new ExtractorDescriptor("text", "Text (stub)", [DocumentFormat.Text], ExtractorCapabilities.Text, 0);
        var trace = new[] { new CandidateVerdict("text", "Text (stub)", 0, CandidateOutcome.Selected, "selected for the test") };
        return new SelectionResult(descriptor, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.Text, trace, null);
    }

    /// <summary>
    ///     Creates the default environment used by most summary scenarios.
    /// </summary>
    /// <returns>An environment with no contributed facts.</returns>
    /// <remarks>Kept minimal so tests that care about facts supply their own environment.</remarks>
    private static ExtractionEnvironment DefaultEnvironment() =>
        new("TestOS", "X64", "test-runtime", "test-rid", []);

    /// <summary>
    ///     Creates the fixed options used across the summary scenarios.
    /// </summary>
    /// <returns>Options stamped with the fixed timestamp.</returns>
    /// <remarks>The fixed timestamp makes the deterministic-text assertion meaningful.</remarks>
    private static ExtractionOptions Options() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    /// <remarks>The canonical clean write used by the mandatory-sections and backend scenarios.</remarks>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nSummary writer content.\n", CancellationToken.None);

    /// <summary>
    ///     Writes a small text document through the sink and reports the supplied self-reported metadata.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="metadata">The document metadata the summary should inline.</param>
    /// <returns>A task that completes when the write and report finish.</returns>
    /// <remarks>Lets a summary scenario exercise the author/modified inlining over reported metadata.</remarks>
    private static async ValueTask WriteTextWithMetadata(IExtractionSink sink, DocumentMetadata metadata)
    {
        await sink.WriteContentAsync("# Document\n\nSummary writer content.\n", CancellationToken.None);
        sink.ReportDocumentMetadata(metadata);
    }

    /// <summary>
    ///     Writes text and claims images were found without writing or explaining them.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Leaves an unexplained image absence that reconciliation must catch with a synthesized gap.</remarks>
    private static async ValueTask WriteSilentImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nText present, images missing.\n", CancellationToken.None);
        sink.ReportFound(GapKind.Images, 2);
    }

    /// <summary>
    ///     Writes text, reports a document extent, and reports the structural features of the content.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes and reports finish.</returns>
    /// <remarks>
    ///     Includes a feature with a count of one so the rendered outline's number agreement is
    ///     exercised, and a zero-count feature so the sink's zero-dropping rule is exercised too.
    /// </remarks>
    private static async ValueTask WriteTextWithFeatures(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nSummary writer content.\n", CancellationToken.None);
        sink.ReportDocumentInfo(new DocumentInfo("Feature Report", PageCount: 3));
        sink.ReportContentFeature(new ContentFeature("headings", 4));
        sink.ReportContentFeature(new ContentFeature("tables", 1));
        sink.ReportContentFeature(new ContentFeature("inline images", 0));
        sink.ReportContentFeature(new ContentFeature("comments", 53));
    }

    /// <summary>
    ///     Writes text and three images: two attributed to pages and one referenced only by a template.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Exercises the aggregate image description and the template-sourced explanation together.</remarks>
    private static async ValueTask WriteMixedImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nText with three images.\n", CancellationToken.None);
        await sink.AddImageAsync(new MemoryStream([1, 2, 3]),
            new ImageHint("placed-one", "image/png", 100, 100, 1), CancellationToken.None);
        await sink.AddImageAsync(new MemoryStream([4, 5, 6]),
            new ImageHint("placed-two", "image/png", 100, 100, 2), CancellationToken.None);
        await sink.AddImageAsync(new MemoryStream([7, 8, 9]),
            new ImageHint("from-template", "image/png", ReferencedByTemplate: true), CancellationToken.None);
        sink.ReportFound(GapKind.Images, 3);
    }
}
