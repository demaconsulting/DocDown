using DemaConsulting.DocDown.Excel.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Excel;

namespace DemaConsulting.DocDown.Excel.Tests;

/// <summary>
///     System-level integration tests for the DocDown Excel extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against workbooks generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real workbook and confirms the contract verifier
///     finds no violations, so a reported gap always matches what is on disk.
/// </remarks>
public class DocDownExcelTests
{
    /// <summary>A fixed timestamp for reproducible output.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

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

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".png", images[0], StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a workbook whose only image is an EMF vector metafile still reports <see
    ///     cref="ExtractionOutcome.Succeeded"/>: the bytes are written unchanged and counted, the
    ///     <c>XLSX0003</c> caveat is stated as an informational diagnostic, and no images gap is
    ///     opened — a well-formed vector-bearing workbook must not degrade.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_VectorImageWorkbook_Succeeds()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.VectorImageWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".emf", images[0], StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "XLSX0003" && diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Images);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
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
    }

    /// <summary>
    ///     Proves each worksheet becomes a part under <c>parts/</c> and the full contract layout is
    ///     present with no violations. A well-formed workbook that embeds no images reports no images
    ///     gap and succeeds cleanly — the backend no longer claims a workbook cannot carry pictures.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_TwoSheetWorkbook_WritesPartPerSheet()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Images);

        var parts = Directory.GetFiles(Path.Combine(scratch, "parts"), "*.md");
        Assert.Equal(2, parts.Length);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
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
    ///     Proves requesting rendered pages for a non-paginated workbook is honored with silence: the
    ///     run succeeds, no pages gap is emitted, and the engine records the non-applicability as an
    ///     informational <c>DD0303</c> diagnostic rather than a false shortfall.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_RenderPagesRequested_StaysSilentAndSucceeds()
    {
        using var temp = new TempScratch();
        var options = FixedOptions();
        options.RenderPages = true;
        var (scratch, result) = await ExtractAsync(temp, "book.xlsx", XlsxFixtures.TwoSheetWorkbook(), options);

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Pages);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0303");
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a legacy binary workbook fails with a structured refusal whose remedy states plainly
    ///     that the format is unsupported and never instructs an installation.
    /// </summary>
    [Fact]
    public async Task DocDownExcel_Extract_LegacyXls_FailsWithUnsupportedFormatRemedy()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "report.xls", XlsxFixtures.LegacyXlsBytes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Remedy!,
            StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Remedy!, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
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
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>Writes a fixture to disk.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
