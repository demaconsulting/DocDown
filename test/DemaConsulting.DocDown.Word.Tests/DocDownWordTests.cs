using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Word.Tests.TestData;
using DocDown.Core;
using DocDown.Word;
using DocDown.Word.OpenXml;

namespace DemaConsulting.DocDown.Word.Tests;

/// <summary>
///     System-level integration tests for the DocDownWord extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against documents generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real document and confirms the contract verifier
///     finds no violations, so a reported gap always matches what is on disk.
/// </remarks>
public class DocDownWordTests
{
    /// <summary>A fixed timestamp for reproducible output.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a generated document produces the full contract layout with a real table and a
    ///     document-control section, and no gaps.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_GeneratedDocx_ProducesContractLayout()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "clean.docx", DocxFixtures.CleanDocument());

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Gaps);
        Assert.Equal("word-openxml", result.SelectedExtractor?.Id);

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("| --- | --- |", content, StringComparison.Ordinal);
        Assert.Contains("## Document Control", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the summary's content outline makes a document's author-attributed comments
    ///     discoverable, rather than reporting only how many characters the content runs to.
    /// </summary>
    /// <remarks>
    ///     This is the defect that prompted the change: an agent asked to find reviewer commentary in
    ///     a draft carrying dozens of author-attributed comments went to an entirely different
    ///     document and answered by inference, because <c>summary.txt</c> said nothing but
    ///     "36,509 characters of text". The outline must name the comments and their comment authors.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithComment_SummaryOutlineNamesTheComments()
    {
        // Arrange / Act: extract a document carrying one author-attributed comment
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "commented.docx", DocxFixtures.DocumentWithComment());

        // Assert: the summary's content outline names the comment and its comment author, and the
        // manifest carries the same counts for a machine reader
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("1 comment", summary, StringComparison.Ordinal);
        Assert.Contains("1 distinct comment author", summary, StringComparison.Ordinal);

        var manifest = await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct);
        Assert.Contains("\"contentFeatures\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"comments\"", manifest, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves embedded images are written and linked from the content.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "images.docx", DocxFixtures.DocumentWithImage());

        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        var written = Assert.Single(images);
        Assert.EndsWith(".png", written, StringComparison.Ordinal);
        Assert.Equal(DocxFixtures.PngBytes(), await File.ReadAllBytesAsync(written, Ct));

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("](images/", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves an engineering-style document preserves its revision in the document-control section.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_EngineeringStyleDocx_PreservesRevisionInDocumentControl()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "engineering.docx", DocxFixtures.EngineeringStyleDocument());

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("## Document Control", content, StringComparison.Ordinal);
        Assert.Contains("Revision", content, StringComparison.Ordinal);
        Assert.Contains("CONFIDENTIAL", content, StringComparison.Ordinal);

        // The page-number footer is furniture: it is omitted and recorded as an informational
        // diagnostic, not a gap, so nothing was lost and the run stays complete
        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.True(result.IsComplete);
        Assert.DoesNotContain(result.Gaps, gap => gap.Reason.Contains("page numbering", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "WORD0009"
            && diagnostic.Severity == DiagnosticSeverity.Info
            && diagnostic.Message.Contains("page-numbering fields", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Page 1 of", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a document embedding a chart reports it as a counted gap naming the loss, instead of
    ///     dropping a chart that carries no image blip and so would otherwise leave no trace at all.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithChart_ReportsChartGap()
    {
        // Arrange / Act: extract a document carrying one embedded chart
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "charted.docx", DocxFixtures.DocumentWithChart());

        // Assert: the chart is counted, explained, and remedied rather than silently absent
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        var gap = Assert.Single(result.Gaps, candidate => candidate.Reason.Contains("charts", StringComparison.Ordinal));
        Assert.Equal(1, gap.AffectedCount);
        Assert.NotNull(gap.Remedy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "WORD0010");
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the Open XML backend is the one selected for a modern document.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_Docx_SelectsOpenXml()
    {
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, "clean.docx", DocxFixtures.CleanDocument());
        var scratch = Path.Combine(temp.Path, "out");

        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);

        Assert.Equal("word-openxml", result.SelectedExtractor?.Id);
    }

    /// <summary>
    ///     Proves a legacy binary document fails with a structured refusal whose remedy states
    ///     plainly that the format is unsupported.
    /// </summary>
    /// <remarks>
    ///     This package reads Open XML documents and nothing else, so a <c>.doc</c> has no reader
    ///     here and never will. The value of the test is the shape of the refusal: a structured
    ///     failure with the full output layout still written, a remedy that promises no capability,
    ///     and no exception reaching the caller — never a silent omission and never an install
    ///     directive the reader cannot act on.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_LegacyDoc_FailsWithUnsupportedFormatRemedy()
    {
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, "report.doc", DocxFixtures.LegacyDocBytes());
        var scratch = Path.Combine(temp.Path, "out");

        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);

        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Contains("doc", result.Failure.Remedy!, StringComparison.Ordinal);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Remedy!,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft Office", result.Failure.Remedy!, StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Remedy!, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Extracts a fixture through the full Word system and returns the folder and result.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The fixture file name.</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <returns>The extraction folder and result.</returns>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes)
    {
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, name, bytes);
        var scratch = Path.Combine(temp.Path, "out");
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);
        return (scratch, result);
    }

    /// <summary>
    ///     Creates options carrying the fixed timestamp for reproducible output.
    /// </summary>
    /// <returns>The options.</returns>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Writes a fixture to disk.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The file name, whose extension drives format detection.</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <returns>The written path.</returns>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
