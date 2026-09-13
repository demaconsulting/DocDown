using System.Text;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Golden-file tests that render real <c>summary.txt</c> output for representative synthetic
///     scenarios and compare it byte-for-byte against committed summary goldens.
/// </summary>
public class SummaryWriterGoldenTests
{
    /// <summary>A fixed timestamp so the rendered header is byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The placeholder substituted for the absolute scratch-folder path during normalization.</summary>
    private const string ScratchPlaceholder = "Scratch folder  : <scratch-folder>";

    /// <summary>The placeholder substituted for the temporary source-document line during normalization.</summary>
    private const string SourcePlaceholder = "Source document : <source-document>";

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the rendered summary for a successful run with rendered pages and no images matches
    ///     its committed golden.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_RenderedPagesNoImages_MatchesCommittedGolden()
    {
        // Arrange: a produced run with content and two rendered pages
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true };
        var environment = PdfEnvironment(includeRenderer: true);

        // Act: render the summary
        var summary = await RenderAsync(
            temp,
            options,
            new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension, 0.9),
            PdfExtractor(),
            ExtractionOutcome.Produced,
            null,
            environment,
            async sink =>
            {
                await sink.WriteContentAsync("# Document\n\nText plus two rendered pages, no embedded images.\n", Ct);
                sink.ReportDocumentInfo(new DocumentInfo("Rendered Report", PageCount: 2));
                using var first = new MemoryStream([1, 2, 3], writable: false);
                using var second = new MemoryStream([4, 5, 6], writable: false);
                await sink.AddPageAsync(1, first, Ct);
                await sink.AddPageAsync(2, second, Ct);
            });

        // Assert: the normalized summary matches its committed golden
        await AssertMatchesGoldenAsync("summary-rendered-pages-no-images.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary for a successful run with embedded images matches its golden.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_EmbeddedImages_MatchesCommittedGolden()
    {
        // Arrange: a produced run with two embedded images
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };
        var environment = PdfEnvironment(includeRenderer: false);

        // Act: render the summary
        var summary = await RenderAsync(
            temp,
            options,
            new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension, 0.9),
            PdfExtractor(),
            ExtractionOutcome.Produced,
            null,
            environment,
            async sink =>
            {
                await sink.WriteContentAsync("# Document\n\nText with two embedded images.\n", Ct);
                sink.ReportDocumentInfo(new DocumentInfo("Illustrated Report", PageCount: 2));
                using var logo = new MemoryStream([10, 11, 12], writable: false);
                using var chart = new MemoryStream([20, 21, 22], writable: false);
                await sink.AddImageAsync(
                    logo,
                    new ImageHint("logo", "image/png", 640, 480, 1),
                    Ct);
                await sink.AddImageAsync(
                    chart,
                    new ImageHint("chart", "image/png", 800, 600, 2),
                    Ct);
            });

        // Assert: the normalized summary matches its committed golden
        await AssertMatchesGoldenAsync("summary-embedded-images.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary for a produced run with an extraction note matches its golden.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_RenderingNotAvailableNote_MatchesCommittedGolden()
    {
        // Arrange: a produced run where page rendering was requested but unavailable
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true };
        var environment = PdfEnvironment(includeRenderer: false);

        // Act: render the summary
        var summary = await RenderAsync(
            temp,
            options,
            new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension, 0.9),
            PdfExtractor(),
            ExtractionOutcome.Produced,
            null,
            environment,
            async sink =>
            {
                await sink.WriteContentAsync("# Document\n\nText extracted; pages were requested but not rendered.\n", Ct);
                sink.ReportDocumentInfo(new DocumentInfo("Unrendered Report", PageCount: 3));
                sink.ReportNote(new ExtractionNote(
                    "Page rendering was requested, but no page renderer is available for the 'pdf' format in this environment; pages were not rendered."));
            });

        // Assert: the normalized summary matches its committed golden
        await AssertMatchesGoldenAsync("summary-rendering-not-available-note.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary for an unreadable docx selection failure matches its golden.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_UnreadableDocxNoBackend_MatchesCommittedGolden()
    {
        // Arrange: a detected docx document with no registered Word backend
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };
        var detection = new FormatDetection(DocumentFormat.Docx, DetectionBasis.Extension, 0.9);
        _ = ExtractorSelector.Select(detection, options, Array.Empty<ExtractorCandidate>(), out var failure);

        // Act: render the unreadable summary
        var summary = await RenderAsync(
            temp,
            options,
            detection,
            null,
            ExtractionOutcome.Unreadable,
            failure,
            PdfEnvironment(includeRenderer: false),
            _ => ValueTask.CompletedTask,
            sourceFileName: "source.docx");

        // Assert: the normalized summary matches its committed golden
        await AssertMatchesGoldenAsync("summary-unreadable-docx-no-backend.txt", summary);
    }

    /// <summary>
    ///     Drives the real sink and summary writer for one synthetic scenario and returns the summary text.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="options">The effective options.</param>
    /// <param name="detection">The detected format for the scenario.</param>
    /// <param name="selected">The selected extractor, or <see langword="null"/>.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="failure">The failure to record, or <see langword="null"/>.</param>
    /// <param name="environment">The fixed environment to record.</param>
    /// <param name="write">The scenario's sink interactions.</param>
    /// <param name="sourceFileName">The synthetic source file name.</param>
    /// <returns>The rendered summary text.</returns>
    private static async Task<string> RenderAsync(
        TempScratch temp,
        ExtractionOptions options,
        FormatDetection detection,
        ExtractorDescriptor? selected,
        ExtractionOutcome outcome,
        ExtractionFailure? failure,
        ExtractionEnvironment environment,
        Func<ExtractionSink, ValueTask> write,
        string sourceFileName = "source.pdf")
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        await write(sink);

        var source = DocumentSource.FromFile(temp.CreateFile(sourceFileName, "% synthetic fixture"));
        var report = new ExtractionReport(
            outcome,
            source,
            "0000",
            detection,
            selected,
            environment,
            options,
            FixedTimestamp,
            failure,
            selected is null ? null : "DemaConsulting.DocDown.Pdf");

        var content = outcome == ExtractionOutcome.Unreadable
            ? null
            : await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);
        return await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);
    }

    /// <summary>
    ///     Creates a PDF-shaped selected extractor for the golden scenarios.
    /// </summary>
    /// <returns>The selected PDF extractor descriptor.</returns>
    private static ExtractorDescriptor PdfExtractor() =>
        new("pdf", "PDF (PdfPig)", [DocumentFormat.Pdf], 100);

    /// <summary>
    ///     Creates a fixed PDF-shaped environment for the golden scenarios.
    /// </summary>
    /// <param name="includeRenderer">Whether a page-rendering fact should be reported as available.</param>
    /// <returns>The synthetic extraction environment.</returns>
    private static ExtractionEnvironment PdfEnvironment(bool includeRenderer) =>
        new(
            "TestOS 1.0",
            "X64",
            "test-runtime 8.0",
            "test-rid",
            includeRenderer
                ? [
                    new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                    new EnvironmentFact("DocDown.Pdf.Rendering", "pages.renderer", "PDFtoImage (PDFium/SkiaSharp, native)", true)
                ]
                : [
                    new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                    new EnvironmentFact("DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", false)
                ]);

    /// <summary>
    ///     Normalizes the machine-specific scratch-folder and source-document lines.
    /// </summary>
    /// <param name="summary">The rendered summary text.</param>
    /// <returns>The summary with the machine-specific lines replaced by placeholders.</returns>
    private static string Normalize(string summary)
    {
        var builder = new StringBuilder(summary.Length);
        foreach (var line in summary.Split('\n'))
        {
            if (line.StartsWith("Scratch folder  : ", StringComparison.Ordinal))
            {
                builder.Append(ScratchPlaceholder).Append('\n');
            }
            else if (line.StartsWith("Source document : ", StringComparison.Ordinal))
            {
                builder.Append(SourcePlaceholder).Append('\n');
            }
            else
            {
                builder.Append(line).Append('\n');
            }
        }

        builder.Length -= 1;
        return builder.ToString();
    }

    /// <summary>
    ///     Compares the normalized summary against the committed golden file, or regenerates it.
    /// </summary>
    /// <param name="goldenName">The golden file name under the <c>golden</c> folder.</param>
    /// <param name="summary">The rendered summary text.</param>
    /// <returns>A task that completes when the comparison or regeneration is done.</returns>
    private static async Task AssertMatchesGoldenAsync(string goldenName, string summary)
    {
        var normalized = Normalize(summary);
        var goldenPath = Path.Combine(GoldenFolder(), goldenName);

        if (Environment.GetEnvironmentVariable("DOCDOWN_UPDATE_GOLDEN") == "1")
        {
            Assert.True(
                Environment.GetEnvironmentVariable("CI") is null &&
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null,
                "DOCDOWN_UPDATE_GOLDEN must not be set in CI.");
            await File.WriteAllTextAsync(goldenPath, normalized, new UTF8Encoding(false), Ct);
        }

        Assert.True(File.Exists(goldenPath), $"Golden file '{goldenPath}' does not exist.");
        var expected = (await File.ReadAllTextAsync(goldenPath, Ct)).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    ///     Locates the committed golden folder in the source tree by walking up to the solution file.
    /// </summary>
    /// <returns>The absolute path of the <c>golden</c> folder.</returns>
    private static string GoldenFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocDown.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test output folder.");
        return Path.Combine(root, "test", "DemaConsulting.DocDown.Core.Tests", "golden");
    }
}
