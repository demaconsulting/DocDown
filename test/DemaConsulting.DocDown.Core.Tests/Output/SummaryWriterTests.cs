using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="SummaryWriter"/>, proving the new fixed section order, the
///     reduced failure and note model, the authored-metadata inlining rules, the content outline,
///     the aggregate image summary, and deterministic output.
/// </summary>
public class SummaryWriterTests
{
    /// <summary>A fixed timestamp used to make the rendered summary byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves every mandatory section for a produced run is present even when there are no notes.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_CleanRun_ContainsEveryMandatorySection()
    {
        // Arrange: a clean successful text run with no notes
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Produced, SuccessExtractor(), null);

        // Assert: the fixed produced-run sections all appear and the notes section states its emptiness
        Assert.Contains("DocDown Extraction Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Source document", summary, StringComparison.Ordinal);
        Assert.Contains("Detected format", summary, StringComparison.Ordinal);
        Assert.Contains("Status          : PRODUCED  - the standard output layout was written.", summary, StringComparison.Ordinal);
        Assert.Contains("Backend", summary, StringComparison.Ordinal);
        Assert.Contains("Environment", summary, StringComparison.Ordinal);
        Assert.Contains("Document metadata", summary, StringComparison.Ordinal);
        Assert.Contains("Layout", summary, StringComparison.Ordinal);
        Assert.Contains("What WAS extracted", summary, StringComparison.Ordinal);
        Assert.Contains("Could not read", summary, StringComparison.Ordinal);
        Assert.Contains("Nothing was left incomplete.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("What was NOT extracted", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Completeness", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the absolute scratch-folder path appears in the leading header block.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_HeaderBlock_ContainsAbsoluteScratchPath()
    {
        // Arrange: a prepared scratch folder rendered into a summary
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);

        // Act: finalize the summary
        var summary = await FinalizeSummaryAsync(temp, folder, sink, ExtractionOutcome.Produced, SuccessExtractor(), null);

        // Assert: the summary states the exact absolute scratch path
        Assert.Contains(folder.AbsolutePath, summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the summary names the selected backend.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_SuccessfulRun_NamesSelectedBackend()
    {
        // Arrange / Act: a successful run whose selected backend is the text stub
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(temp, WriteText, ExtractionOutcome.Produced, SuccessExtractor(), null);

        // Assert: the backend block names the selected backend
        Assert.Contains("Selected  : text - Text (stub)", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the environment block reports both the base runtime lines and contributed facts.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithEnvironmentFacts_ReportsRuntimeAndFacts()
    {
        // Arrange: a run whose environment carries a distinctive contributed fact
        using var temp = new TempScratch();
        var environment = new ExtractionEnvironment(
            "TestOS 1.0",
            "X64",
            "test-runtime 8.0",
            "test-rid",
            [new EnvironmentFact("TestBackend", "renderer", "libtxt 4.2 present", true)]);

        // Act: render the summary
        var summary = await RenderSummaryAsync(
            temp,
            WriteText,
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null,
            environment);

        // Assert: the base runtime lines and the contributed fact are reported
        Assert.Contains("Operating system   : TestOS 1.0, X64", summary, StringComparison.Ordinal);
        Assert.Contains("Runtime            : test-runtime 8.0", summary, StringComparison.Ordinal);
        Assert.Contains("renderer", summary, StringComparison.Ordinal);
        Assert.Contains("libtxt 4.2 present", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves notes are listed in the <c>Could not read</c> section for a produced run.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_ProducedRun_ListsNotesUnderCouldNotRead()
    {
        // Arrange: a produced run that records a note
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(
            temp,
            async sink =>
            {
                await sink.WriteContentAsync("# Document\n\nText present.\n", CancellationToken.None);
                sink.ReportNote(new ExtractionNote("One embedded image could not be decoded and was skipped."));
            },
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: the note is rendered as a bullet in the fixed notes section
        Assert.Contains("Could not read", summary, StringComparison.Ordinal);
        Assert.Contains("  - One embedded image could not be decoded and was skipped.", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an unreadable run reproduces the failure explanation verbatim and omits the selected backend.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnreadableRun_ReproducesFailureExplanationVerbatim()
    {
        // Arrange: an unreadable run with a distinctive multi-line explanation
        using var temp = new TempScratch();
        const string explanation =
            "The source document could not be read.\nDetected format: unknown (application/octet-stream) - detected by file extension";
        var failure = new ExtractionFailure("The source document could not be read.", explanation);

        // Act: render the unreadable summary
        var summary = await RenderSummaryAsync(temp, _ => ValueTask.CompletedTask, ExtractionOutcome.Unreadable, null, failure);

        // Assert: the explanation reaches the reader exactly as authored and the backend block explains the null selection
        Assert.Contains(explanation, summary, StringComparison.Ordinal);
        Assert.Contains("Status          : UNREADABLE  - no output could be produced; see \"Failure\"", summary, StringComparison.Ordinal);
        Assert.Contains("No backend was selected. See \"Failure\" for the reason.", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves two renders of the same content are byte-identical.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalSummary()
    {
        // Arrange: a prepared folder, sink, and fixed report rendered once, then snapshotted
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);
        var report = BuildReport(temp, Options(), ExtractionOutcome.Produced, SuccessExtractor(), null, DefaultEnvironment());
        var content = await ContentWriter.WriteAsync(sink, "Document", Ct);

        // Act: render twice into the same folder, snapshotting the first render
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);
        var firstSnapshot = Path.Combine(temp.Path, "first-summary.txt");
        File.Copy(Path.Combine(folder.AbsolutePath, "summary.txt"), firstSnapshot);
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);

        // Assert: the deterministic output is byte-identical
        ContractAssert.FileEquals(firstSnapshot, Path.Combine(folder.AbsolutePath, "summary.txt"));
    }

    /// <summary>
    ///     Proves PDF-shaped metadata prefers the human author over the producing application.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_PdfShapedMetadata_InlinesHumanAuthorNotApplication()
    {
        // Arrange: PDF-shaped metadata with both an author and a creator
        using var temp = new TempScratch();
        var metadata = new DocumentMetadata(
            [
                new DocumentMetadataField("author", "The Document Author", MetadataProvenance.PdfDocumentInformation),
                new DocumentMetadataField("creator", "Sample Word Processor", MetadataProvenance.PdfDocumentInformation),
                new DocumentMetadataField("modified", "2026-05-04T00:00:00Z", MetadataProvenance.PdfDocumentInformation)
            ],
            []);

        // Act: render the summary
        var summary = await RenderSummaryAsync(
            temp,
            sink => WriteTextWithMetadata(sink, metadata),
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: the human author is inlined and the producing application is not
        Assert.Contains("Author          : The Document Author", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample Word Processor", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves OPC-shaped metadata falls back to <c>creator</c> as the author.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_OpcShapedMetadata_FallsBackToCreatorAsAuthor()
    {
        // Arrange: OPC-shaped metadata with only a creator field
        using var temp = new TempScratch();
        var metadata = new DocumentMetadata(
            [
                new DocumentMetadataField("creator", "The Document Author", MetadataProvenance.OpcCoreProperties),
                new DocumentMetadataField("modified", "2026-05-04T00:00:00Z", MetadataProvenance.OpcCoreProperties)
            ],
            []);

        // Act: render the summary
        var summary = await RenderSummaryAsync(
            temp,
            sink => WriteTextWithMetadata(sink, metadata),
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: creator is used as the author
        Assert.Contains("Author          : The Document Author", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the summary opens with a gist and outlines the content's structural features.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithContentFeatures_OpensWithGistAndOutlinesContent()
    {
        // Arrange / Act: a run reporting document extent and extracted content structure
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(
            temp,
            WriteTextWithFeatures,
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: the gist precedes the header block and the outline names the features
        var gistIndex = summary.IndexOf("A 3-page plain-text document; extracted", StringComparison.Ordinal);
        var scratchIndex = summary.IndexOf("Scratch folder  : ", StringComparison.Ordinal);
        Assert.InRange(gistIndex, 0, scratchIndex);
        Assert.Contains("Contains 4 headings, 1 table, and 53 comments.", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a looked-for zero feature is outlined while an undeclared zero stays absent.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_LookedForZeroFeature_OutlinesTheZero()
    {
        // Arrange / Act: a run with one looked-for zero and one undeclared zero
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(
            temp,
            WriteTextWithLookedForZero,
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: the declared zero appears naturally and the undeclared zero does not
        Assert.Contains(
            "Contains 50 slides, 44 slide titles, 0 sets of speaker notes, and 42 inline images.",
            summary,
            StringComparison.Ordinal);
        Assert.DoesNotContain("charts", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the gist is omitted when the detected format has no plain-English name.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnknownFormat_OmitsGistRatherThanGuessing()
    {
        // Arrange: a run whose detected format is unknown
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);
        var report = new ExtractionReport(
            ExtractionOutcome.Produced,
            DocumentSource.FromFile(temp.CreateFile("source.bin", "hello")),
            new FormatDetection(DocumentFormat.Unknown, DetectionBasis.Extension, 0.1),
            SuccessExtractor(),
            DefaultEnvironment(),
            Options(),
            FixedTimestamp,
            null,
            "DemaConsulting.DocDown.TestSupport");
        var content = await ContentWriter.WriteAsync(sink, "Document", Ct);

        // Act: render the summary
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);

        // Assert: the banner is followed directly by the header block
        Assert.Contains("==========================\n\nScratch folder  : ", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves extracted images are summarized in aggregate and template-only images are explained.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_WithImages_SummarizesInAggregateAndExplainsTemplateImages()
    {
        // Arrange / Act: two placed images and one template-only image
        using var temp = new TempScratch();
        var summary = await RenderSummaryAsync(
            temp,
            WriteMixedImages,
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null);

        // Assert: the image set is summarized in aggregate and no per-image line survives
        Assert.Contains("3 embedded images, 9 bytes total, spanning pages 1-2.", summary, StringComparison.Ordinal);
        Assert.Contains("One of them carries no page number because a layout, master, or stencil", summary, StringComparison.Ordinal);
        Assert.Contains("See manifest.json for the per-image inventory.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("images/0001-placed-one.png", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves available-but-unused candidate facts are collapsed into a counted line while
    ///     unavailability facts remain visible.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_WriteAsync_UnusedAvailableCandidates_CollapsesThemIntoOneCountedLine()
    {
        // Arrange: one backend fact, one unavailable candidate, and two available-but-unused candidates
        using var temp = new TempScratch();
        var environment = new ExtractionEnvironment(
            "TestOS 1.0",
            "X64",
            "test-runtime 8.0",
            "test-rid",
            [
                new EnvironmentFact("TestBackend", "renderer", "libtxt 4.2 present", true),
                new EnvironmentFact("Absent Backend", "backend.absent", "no native library", false, EnvironmentFactOrigin.CandidateAvailability),
                new EnvironmentFact("Unused One", "backend.unused-one", "available", true, EnvironmentFactOrigin.CandidateAvailability),
                new EnvironmentFact("Unused Two", "backend.unused-two", "available", true, EnvironmentFactOrigin.CandidateAvailability)
            ]);

        // Act: render the summary
        var summary = await RenderSummaryAsync(
            temp,
            WriteText,
            ExtractionOutcome.Produced,
            SuccessExtractor(),
            null,
            environment);

        // Assert: the backend fact and the unavailability remain, while the available candidates are counted
        Assert.Contains("libtxt 4.2 present", summary, StringComparison.Ordinal);
        Assert.Contains("no native library", summary, StringComparison.Ordinal);
        Assert.Contains("2 other registered backends were available but did not run; run 'docdown --list-backends' to see them.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("backend.unused-one", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Renders a summary for a write action, outcome, selected extractor, and optional failure.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="selected">The selected extractor, or <see langword="null"/>.</param>
    /// <param name="failure">The failure to record, or <see langword="null"/>.</param>
    /// <param name="environment">The environment to record, or <see langword="null"/> for the default.</param>
    /// <returns>The rendered summary text.</returns>
    private static async Task<string> RenderSummaryAsync(
        TempScratch temp,
        Func<IExtractionSink, ValueTask> write,
        ExtractionOutcome outcome,
        ExtractorDescriptor? selected,
        ExtractionFailure? failure,
        ExtractionEnvironment? environment = null)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await write(sink);
        return await FinalizeSummaryAsync(temp, folder, sink, outcome, selected, failure, environment);
    }

    /// <summary>
    ///     Finalizes content and renders the summary for a prepared folder and sink.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink holding the written content.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="selected">The selected extractor, or <see langword="null"/>.</param>
    /// <param name="failure">The failure to record, or <see langword="null"/>.</param>
    /// <param name="environment">The environment to record, or <see langword="null"/> for the default.</param>
    /// <returns>The rendered summary text.</returns>
    private static async Task<string> FinalizeSummaryAsync(
        TempScratch temp,
        ScratchFolder folder,
        ExtractionSink sink,
        ExtractionOutcome outcome,
        ExtractorDescriptor? selected,
        ExtractionFailure? failure,
        ExtractionEnvironment? environment = null)
    {
        var report = BuildReport(temp, Options(), outcome, selected, failure, environment ?? DefaultEnvironment());
        var content = outcome == ExtractionOutcome.Unreadable
            ? null
            : await ContentWriter.WriteAsync(sink, "Document", Ct);
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);
        return await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);
    }

    /// <summary>
    ///     Builds a consistent extraction report for the summary writer.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <param name="outcome">The recorded outcome.</param>
    /// <param name="selected">The recorded selected extractor, or <see langword="null"/>.</param>
    /// <param name="failure">The recorded failure, or <see langword="null"/>.</param>
    /// <param name="environment">The recorded environment.</param>
    /// <returns>The assembled report.</returns>
    private static ExtractionReport BuildReport(
        TempScratch temp,
        ExtractionOptions options,
        ExtractionOutcome outcome,
        ExtractorDescriptor? selected,
        ExtractionFailure? failure,
        ExtractionEnvironment environment)
    {
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(
            outcome,
            source,
            detection,
            selected,
            environment,
            options,
            FixedTimestamp,
            failure,
            selected is null ? null : "DemaConsulting.DocDown.TestSupport");
    }

    /// <summary>
    ///     Creates the selected extractor used by the successful summary scenarios.
    /// </summary>
    /// <returns>The selected extractor descriptor.</returns>
    private static ExtractorDescriptor SuccessExtractor() =>
        new("text", "Text (stub)", [DocumentFormat.Text], 0);

    /// <summary>
    ///     Creates the default environment used by most summary scenarios.
    /// </summary>
    /// <returns>An environment with no contributed facts.</returns>
    private static ExtractionEnvironment DefaultEnvironment() =>
        new("TestOS", "X64", "test-runtime", "test-rid", []);

    /// <summary>
    ///     Creates the fixed options used across the summary scenarios.
    /// </summary>
    /// <returns>Options stamped with the fixed timestamp.</returns>
    private static ExtractionOptions Options() => new();

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nSummary writer content.\n", CancellationToken.None);

    /// <summary>
    ///     Writes a small text document through the sink and reports the supplied metadata.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="metadata">The document metadata the summary should inline.</param>
    /// <returns>A task that completes when the write and report finish.</returns>
    private static async ValueTask WriteTextWithMetadata(IExtractionSink sink, DocumentMetadata metadata)
    {
        await sink.WriteContentAsync("# Document\n\nSummary writer content.\n", CancellationToken.None);
        sink.ReportDocumentMetadata(metadata);
    }

    /// <summary>
    ///     Writes text, reports a document extent, and reports the structural features of the content.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes and reports finish.</returns>
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
    ///     Writes text and reports a deck-shaped outline with a looked-for zero.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WriteTextWithLookedForZero(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Deck\n\nOutline.\n", CancellationToken.None);
        sink.ReportContentFeature(new ContentFeature("slides", 50));
        sink.ReportContentFeature(new ContentFeature("slide titles", 44, "slide title"));
        sink.ReportContentFeature(new ContentFeature("sets of speaker notes", 0, "set of speaker notes", LookedFor: true));
        sink.ReportContentFeature(new ContentFeature("charts", 0));
        sink.ReportContentFeature(new ContentFeature("inline images", 42, "inline image"));
    }

    /// <summary>
    ///     Writes text and three small images, one of which is template-only.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WriteMixedImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith images.\n", CancellationToken.None);

        using (var one = new MemoryStream([1, 2, 3], writable: false))
        {
            await sink.AddImageAsync(one, new ImageHint("placed-one", "image/png", SourcePages: [1]), CancellationToken.None);
        }

        using (var two = new MemoryStream([4, 5, 6], writable: false))
        {
            await sink.AddImageAsync(two, new ImageHint("placed-two", "image/png", SourcePages: [2]), CancellationToken.None);
        }

        using (var three = new MemoryStream([7, 8, 9], writable: false))
        {
            await sink.AddImageAsync(
                three,
                new ImageHint("template-only", "image/png", ReferencedByTemplate: true),
                CancellationToken.None);
        }
    }
}
