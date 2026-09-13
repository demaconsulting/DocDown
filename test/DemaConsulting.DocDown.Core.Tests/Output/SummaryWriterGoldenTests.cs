using System.Text;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Golden-file tests that render real <c>summary.txt</c> output for representative reconciled
///     inputs and compare it, byte for byte, against committed, human-readable golden files.
/// </summary>
/// <remarks>
///     <para>
///         These tests exist to close a specific review gap: prior review rounds examined code,
///         requirements, design, verification docs and tests, but never the generated artifact a
///         reader actually consumes — so two clarity defects (a Layout/Completeness vocabulary
///         collision and unattributed environment facts) survived four rounds. The committed golden
///         files make the rendered output itself a reviewable artifact.
///     </para>
///     <para>
///         Scope is deliberately narrow and honestly bounded. Each scenario constructs a ledger by
///         driving the real <see cref="ExtractionSink"/>, reconciling with
///         <see cref="ManifestWriter"/>, and rendering with <see cref="SummaryWriter"/> — with a
///         fixed <see cref="ExtractionOptions.TimestampUtc"/> and a fixed
///         <see cref="ExtractionEnvironment"/>. They therefore prove <em>the summary renderer's
///         output for a given reconciled ledger</em>, not that a real end-to-end PDF (or page
///         rendering) extraction produces that ledger. End-to-end behavior remains the job of the
///         platform-conditional <c>DocDown.Pdf</c>/<c>DocDown.Pdf.Rendering</c> tests. Because the
///         golden files are Core-level and deterministic, they run on every platform with no native
///         dependency and no skip.
///     </para>
///     <para>
///         Determinism: the timestamp, environment strings and relative paths are fixed by
///         construction, and <see cref="SummaryWriter"/> already emits <c>\n</c> endings, no BOM and
///         invariant-culture formatting. The only machine-specific lines are the absolute
///         <c>Scratch folder</c> path and the temporary <c>Source document</c> path; both are
///         replaced with a fixed placeholder token before comparison (see <see cref="Normalize"/>).
///         Everything else is byte-compared against the golden.
///     </para>
/// </remarks>
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
    ///     its golden — the scenario that exposed the Layout/Completeness vocabulary collision, where
    ///     <c>images/</c> is legitimately empty (Layout says not present, Completeness says COMPLETE).
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_RenderedPagesNoImages_MatchesCommittedGolden()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true };

        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [
                new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                new EnvironmentFact("DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", false),
                new EnvironmentFact("DocDown.Pdf.Rendering", "pages.renderer", "PDFtoImage (PDFium/SkiaSharp, native)", true)
            ]);

        var summary = await RenderAsync(temp, options, environment, ExtractionOutcome.Succeeded, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nText plus two rendered pages, no embedded images.\n", Ct);
            sink.ReportDocumentInfo(new DocumentInfo("Rendered Report", PageCount: 2));
            await sink.AddPageAsync(1, new MemoryStream([1, 2, 3]), Ct);
            await sink.AddPageAsync(2, new MemoryStream([4, 5, 6]), Ct);
            sink.ReportFound(GapKind.Pages, 2);
        });

        await AssertMatchesGoldenAsync("summary-rendered-pages-no-images.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary for a successful run with embedded images matches its golden.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_EmbeddedImages_MatchesCommittedGolden()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };

        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [
                new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                new EnvironmentFact("DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", false)
            ]);

        var summary = await RenderAsync(temp, options, environment, ExtractionOutcome.Succeeded, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nText with two embedded images.\n", Ct);
            sink.ReportDocumentInfo(new DocumentInfo("Illustrated Report", PageCount: 2));
            await sink.AddImageAsync(new MemoryStream([10, 11, 12]),
                new ImageHint("logo", "image/png", 640, 480, 1), Ct);
            await sink.AddImageAsync(new MemoryStream([20, 21, 22]),
                new ImageHint("chart", "image/png", 800, 600, 2), Ct);
            sink.ReportFound(GapKind.Images, 2);
        });

        await AssertMatchesGoldenAsync("summary-embedded-images.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary for a degraded run with a genuine, explained gap matches its
    ///     golden — here some but not all embedded images could be decoded.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_DegradedWithGap_MatchesCommittedGolden()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };

        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [
                new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                new EnvironmentFact("DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", false)
            ]);

        var summary = await RenderAsync(temp, options, environment, ExtractionOutcome.Degraded, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nText with a partial image set.\n", Ct);
            sink.ReportDocumentInfo(new DocumentInfo("Partly Illustrated Report", PageCount: 4));
            await sink.AddImageAsync(new MemoryStream([30, 31, 32]),
                new ImageHint("figure-1", "image/png", 320, 240, 1), Ct);
            await sink.AddImageAsync(new MemoryStream([40, 41, 42]),
                new ImageHint("figure-2", "image/png", 320, 240, 2), Ct);
            await sink.AddImageAsync(new MemoryStream([50, 51, 52]),
                new ImageHint("figure-3", "image/png", 320, 240, 3), Ct);
            sink.ReportFound(GapKind.Images, 4);
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                "DD0202", DiagnosticSeverity.Warning,
                "An embedded image could not be decoded and was skipped.", "page 4"));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Images, "images/", GapScope.PartiallyExtracted,
                "1 of 4 embedded images could not be decoded and was skipped.",
                Impact: "One illustration is missing from the extracted content.",
                Remedy: "Re-run against a source whose embedded images are not corrupt.",
                AffectedCount: 1));
        });

        await AssertMatchesGoldenAsync("summary-degraded-gap.txt", summary);
    }

    /// <summary>
    ///     Proves the rendered summary matches its golden when page rendering was requested but no
    ///     rendering backend is registered — the <c>DD0301</c> path. Here <c>pdf.pageRendering</c>
    ///     reads <c>NOT available</c> under its <c>DocDown.Pdf</c> heading, a <c>DD0301</c> diagnostic
    ///     is present, and pages Completeness reads <c>MISSING</c>.
    /// </summary>
    [Fact]
    public async Task SummaryWriter_Golden_RenderingNotRegisteredDd0301_MatchesCommittedGolden()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true };

        var environment = new ExtractionEnvironment(
            "TestOS 1.0", "X64", "test-runtime 8.0", "test-rid",
            [
                new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", true),
                new EnvironmentFact("DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", false)
            ]);

        var summary = await RenderAsync(temp, options, environment, ExtractionOutcome.Degraded, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nText extracted; pages requested but not rendered.\n", Ct);
            sink.ReportDocumentInfo(new DocumentInfo("Unrendered Report", PageCount: 3));
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                "DD0301", DiagnosticSeverity.Warning,
                "The requested renderedPages capability is unavailable in this environment."));
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                "DD0702", DiagnosticSeverity.Warning,
                "Degraded: the selected backend 'pdf' lacks the requested renderedPages capability."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Pages, "pages/", GapScope.Unavailable,
                "Page rendering was requested but the selected backend 'pdf' does not provide the "
                + "renderedPages capability in this environment.",
                Impact: "Rendered page images are not available.",
                Remedy: "Run in an environment where a page-rendering backend for this format is available."));
        });

        await AssertMatchesGoldenAsync("summary-rendering-not-registered-dd0301.txt", summary);
    }

    /// <summary>
    ///     Drives the real sink/reconcile/render chain for a scenario and returns the summary text.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="options">The effective options (timestamp and render flags).</param>
    /// <param name="environment">The fixed environment to record.</param>
    /// <param name="outcome">The extraction outcome to record.</param>
    /// <param name="write">The scenario's sink interactions.</param>
    /// <returns>The rendered summary text.</returns>
    /// <remarks>Uses a PDF-shaped selection so the backend block reads like a real PDF run.</remarks>
    private static async Task<string> RenderAsync(
        TempScratch temp, ExtractionOptions options, ExtractionEnvironment environment,
        ExtractionOutcome outcome, Func<ExtractionSink, Task> write)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        await write(sink);

        var source = DocumentSource.FromFile(temp.CreateFile("source.pdf", "%PDF-1.7 golden fixture"));
        var detection = new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension, 0.9);
        var selection = PdfSelection();
        var report = new ExtractionReport(
            outcome, source, "0000", detection, selection, environment, options, FixedTimestamp, null);

        var content = await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);

        return await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "summary.txt"), Ct);
    }

    /// <summary>
    ///     Creates a PDF-shaped automatic selection naming the <c>pdf</c> backend.
    /// </summary>
    /// <returns>A selection result whose selected descriptor is the PDF backend.</returns>
    /// <remarks>Keeps the backend block deterministic and realistic for a PDF extraction.</remarks>
    private static SelectionResult PdfSelection()
    {
        var descriptor = new ExtractorDescriptor("pdf", "PDF (PdfPig)", [DocumentFormat.Pdf], ExtractorCapabilities.Text, 100);
        var trace = new[] { new CandidateVerdict("pdf", "PDF (PdfPig)", 100, CandidateOutcome.Selected, "selected for the golden fixture") };
        return new SelectionResult(descriptor, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.Text, trace, null);
    }

    /// <summary>
    ///     Normalizes the two machine-specific lines so the remainder can be byte-compared.
    /// </summary>
    /// <param name="summary">The rendered summary text.</param>
    /// <returns>The summary with the scratch-folder and source-document lines replaced by placeholders.</returns>
    /// <remarks>
    ///     Only the absolute scratch path and the temporary source path/size vary between machines;
    ///     every other line is deterministic by construction and left intact for the comparison.
    /// </remarks>
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

        // Split on '\n' drops the trailing empty element; the loop re-adds one '\n' per line, so the
        // reconstructed text ends with exactly one extra '\n'. Trim it to preserve the original bytes.
        builder.Length -= 1;
        return builder.ToString();
    }

    /// <summary>
    ///     Compares the normalized summary against the committed golden file, or regenerates it.
    /// </summary>
    /// <param name="goldenName">The golden file name under the <c>golden</c> folder.</param>
    /// <param name="summary">The rendered summary text.</param>
    /// <returns>A task that completes when the comparison (or regeneration) is done.</returns>
    /// <remarks>
    ///     Setting <c>DOCDOWN_UPDATE_GOLDEN=1</c> rewrites the committed golden from the current
    ///     output. Regeneration is refused when a CI environment is detected, so the assertion is
    ///     always a real comparison there and a golden can never silently self-heal. The golden is
    ///     read from the source tree so the file a reviewer reads is the file under test.
    /// </remarks>
    private static async Task AssertMatchesGoldenAsync(string goldenName, string summary)
    {
        var normalized = Normalize(summary);
        var goldenPath = Path.Combine(GoldenFolder(), goldenName);

        if (Environment.GetEnvironmentVariable("DOCDOWN_UPDATE_GOLDEN") == "1")
        {
            // These golden files exist to make generated output reviewable. Regenerating them in
            // CI would turn a failing comparison into a silent rewrite, defeating that purpose, so
            // the escape hatch is refused wherever a CI environment is detected.
            Assert.True(
                Environment.GetEnvironmentVariable("CI") is null &&
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null,
                "DOCDOWN_UPDATE_GOLDEN must not be set in CI: golden files would self-heal instead of failing.");

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
    /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be located.</exception>
    /// <remarks>Anchored on the solution file so the walk cannot stop at a coincidentally named folder.</remarks>
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
