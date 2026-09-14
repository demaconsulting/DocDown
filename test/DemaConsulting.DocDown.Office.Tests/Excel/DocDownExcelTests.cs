using DemaConsulting.DocDown.Office.Tests.Excel.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Excel;

namespace DemaConsulting.DocDown.Office.Tests.Excel;

/// <summary>
///     System-level integration tests for the DocDown Excel extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against workbooks generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real workbook and confirms the invariant output
///     layout is written, so the structured result and the files on disk describe the same run.
/// </remarks>
public class DocDownExcelTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a workbook's embedded image is extracted into the <c>images/</c> folder and the run
    ///     succeeds cleanly with the full contract layout.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_ImageWorkbook_WritesEmbeddedImage()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.ImageWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".png", images[0], StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a workbook whose only image is an EMF vector metafile still reports <see
    ///     cref="ExtractionOutcome.Produced"/>: the bytes are written unchanged and no vector-only
    ///     caveat is recorded.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_VectorImageWorkbook_Succeeds()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.VectorImageWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".emf", images[0], StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the Excel backend is the one selected for a modern workbook.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_Xlsx_SelectsOpenXml()
    {
        using var temp = new TempScratch();
        var (_, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), FixedOptions());

        Assert.Equal("excel-openxml", result.SelectedExtractor?.Id);
        Assert.False(result.SelectedExtractor?.PageRenderingApplicable ?? true);
    }

    /// <summary>
    ///     Proves each worksheet becomes a part under <c>parts/</c> and the full contract layout is
    ///     present. A well-formed workbook that embeds no images still produces output cleanly and
    ///     records no extraction note about absent pictures.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_TwoSheetWorkbook_WritesPartPerSheet()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);

        var parts = Directory.GetFiles(Path.Combine(scratch, "parts"), "*.md");
        Assert.Equal(2, parts.Length);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a long prose cell survives extraction intact, never truncated, and its formula is
    ///     preserved alongside its value.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_PreservesLongProseAndFormulas()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), FixedOptions());

        var allText = string.Concat(
            Directory.GetFiles(Path.Combine(scratch, "parts"), "*.md").Select(File.ReadAllText));
        Assert.Contains(XlsxFixtures.LongProse, allText, StringComparison.Ordinal);
        Assert.Contains("=A1+A2", allText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves requesting rendered pages for a non-paginated workbook is honored with silence:
    ///     the run still produces output, no page images are written, and no note is recorded.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_RenderPagesRequested_StaysSilentAndSucceeds()
    {
        using var temp = new TempScratch();
        var options = FixedOptions();
        options.RenderPages = true;
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), options);

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.PagePaths);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a legacy binary workbook is unreadable with a structured explanation that states
    ///     plainly that the format is unsupported and never instructs an installation.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_LegacyXls_IsUnreadableWithUnsupportedFormatExplanation()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "report.xls", XlsxFixtures.LegacyXlsBytes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Null(result.SelectedExtractor);
        Assert.Equal("No registered extractor supports the detected format.", result.Failure.Summary);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Explanation,
            StringComparison.Ordinal);
        Assert.Contains("Detected format: xls", result.Failure.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Explanation, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a workbook's chart reaches the output as its own part carrying the cached data
    ///     series, its title, and both axis titles — the defect this fixture exists to prevent was a
    ///     chart that vanished while the run still reported a complete extraction.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_ChartWorkbook_WritesChartDataPart()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.ChartWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);
        var chartPart = Assert.Single(
            Directory.GetFiles(Path.Combine(scratch, "parts"), "*chart*.md"));
        var markdown = await File.ReadAllTextAsync(chartPart, Ct);
        Assert.Contains("# Tank Pressure Trend", markdown, StringComparison.Ordinal);
        Assert.Contains("- Category axis: Elapsed time (min)", markdown, StringComparison.Ordinal);
        Assert.Contains("- Value axis: Pressure (kPa)", markdown, StringComparison.Ordinal);
        Assert.Contains("| 2 | 10 | 109.2 |", markdown, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Extracts a fixture through the full Excel system and returns the folder and result.
    /// </summary>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes, ExtractionOptions options)
    {
        var engine = new DocDownBuilder().AddExcel().Build();
        var input = WriteFixture(temp, name, bytes);
        var scratch = Path.Combine(temp.Path, "out");
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, options, Ct);
        return (scratch, result);
    }

    /// <summary>Creates options carrying the fixed timestamp for reproducible output.</summary>
    private static ExtractionOptions FixedOptions() => new();

    /// <summary>Writes a fixture to disk.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
