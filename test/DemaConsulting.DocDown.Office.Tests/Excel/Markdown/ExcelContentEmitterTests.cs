using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Excel.Markdown;
using DocDown.Excel.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Excel.Markdown;

/// <summary>
///     Unit tests for <see cref="ExcelContentEmitter"/>, exercising the part writing, content
///     inventory, and note-reporting policy from hand-built models with no workbook behind them.
/// </summary>
public class ExcelContentEmitterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves an image a worksheet references is linked inline under its sheet section, using the
    ///     path the sink returned — following Word's convention.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_SheetImage_LinksInlineFromSheet()
    {
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/xl/media/image1.png", AltText: "Sales chart", SourcePages: [1]);
        var sheet = new ExcelSheetModel("Charts", [new ExcelCellModel("A1", "x", null)], [],
            [new ExcelSheetImageRef("/xl/media/image1.png", "Sales chart")]);
        var model = new ExcelWorkbookModel([sheet], [image]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = true }, model, Ct);

        var part = Assert.Single(sink.Parts);
        Assert.Contains("![Sales chart](images/0001-image.png)", part.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves each worksheet becomes a titled sheet part and the content inventory reports the
    ///     workbook's worksheet count.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_TwoSheets_WritesTitledSheetParts()
    {
        var model = new ExcelWorkbookModel(
        [
            new ExcelSheetModel("Requirements", [new ExcelCellModel("A1", "Pressure", null)], []),
            new ExcelSheetModel("Calculations", [new ExcelCellModel("A3", "5", "A1+A2")], [])
        ]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Equal(2, sink.Parts.Count);
        Assert.All(sink.Parts, part => Assert.Equal(ContentPartKind.Sheet, part.Part.Kind));
        Assert.Equal("Requirements", sink.Parts[0].Part.Title);
        Assert.Equal("Calculations", sink.Parts[1].Part.Title);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "worksheets" && feature.Count == 2);
    }

    /// <summary>
    ///     Proves a default extraction of a workbook that embeds no images records that zero-image
    ///     inventory explicitly, with no note about a missing step.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_NoImages_ReportsZeroImageInventory()
    {
        var model = new ExcelWorkbookModel([new ExcelSheetModel("S", [new ExcelCellModel("A1", "x", null)], [])]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Empty(sink.Notes);
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature.Label == "inline images" && feature.Count == 0 && feature.LookedFor);
    }

    /// <summary>
    ///     Proves the workbook's embedded images are written through the sink as passthroughs and the
    ///     content inventory reports the image count.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_WithRasterImages_WritesThroughSink()
    {
        var model = new ExcelWorkbookModel(
            [new ExcelSheetModel(
                "S",
                [new ExcelCellModel("A1", "x", null)],
                [],
                [new ExcelSheetImageRef("/xl/media/image1.png", "photo")])],
            [new EmbeddedImage([1, 2, 3], "image/png", "photo", "/xl/media/image1.png")]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var image = Assert.Single(sink.Images);
        Assert.Equal(ImageTransform.Passthrough, image.Hint.Transform);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "inline images" && feature.Count == 1);
    }

    /// <summary>
    ///     Proves an EMF vector metafile is written unchanged with no vector-only note: the bytes were
    ///     written successfully, so nothing was left incomplete.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_VectorImage_WritesSilently()
    {
        var model = new ExcelWorkbookModel(
            [new ExcelSheetModel("S", [new ExcelCellModel("A1", "x", null)], [])],
            [new EmbeddedImage([1, 2, 3], "image/x-emf", "schematic", "/xl/media/image1.emf")]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Single(sink.Images);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a formula is rendered alongside its value, and the cell address is cited.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_FormulaCell_RendersFormulaValueAndAddress()
    {
        var model = new ExcelWorkbookModel(
            [new ExcelSheetModel("Calc", [new ExcelCellModel("A3", "5", "A1+A2")], [])]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var markdown = Assert.Single(sink.Parts).Markdown;
        Assert.Contains("A3", markdown, StringComparison.Ordinal);
        Assert.Contains("5", markdown, StringComparison.Ordinal);
        Assert.Contains("=A1+A2", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a page-rendering request against a workbook is answered with silence by the backend,
    ///     because a workbook is non-paginated and the engine records that non-applicability itself.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_RenderPagesRequested_StaysSilent()
    {
        var model = new ExcelWorkbookModel([new ExcelSheetModel("S", [new ExcelCellModel("A1", "x", null)], [])]);
        var sink = new RecordingSink();
        var options = new ExtractionOptions { RenderPages = true, IncludeEmbeddedImages = false };

        await ExcelContentEmitter.EmitAsync(sink, options, model, Ct);

        Assert.Empty(sink.Pages);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an empty workbook is reported through zero-count inventory entries rather than a
    ///     gap, so the summary can say the backend looked and found none.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_EmptyWorkbook_ReportsZeroCountInventory()
    {
        var model = new ExcelWorkbookModel([]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Empty(sink.Parts);
        Assert.Empty(sink.Notes);
        Assert.Equal(0, Assert.Single(sink.DocumentInfos).PartCount);
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature.Label == "worksheets" && feature.Count == 0 && feature.LookedFor);
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature.Label == "inline images" && feature.Count == 0 && feature.LookedFor);
    }

    /// <summary>
    ///     Proves an empty worksheet says so in its sheet part and records no note, because the
    ///     absence is a fact about the document rather than an incomplete step.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_EmptySheet_WritesEmptySheetMessage()
    {
        var model = new ExcelWorkbookModel([new ExcelSheetModel("Blank", [], [])]);
        var sink = new RecordingSink();

        await ExcelContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var part = Assert.Single(sink.Parts);
        Assert.Contains("_This worksheet has no cell content._", part.Markdown, StringComparison.Ordinal);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a dense rectangular region is rendered as a markdown grid table (column-letter
    ///     header, row-number leading column) in addition to the always-present address listing.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_DenseGrid_RendersTableAndListing()
    {
        var sheet = new ExcelSheetModel("Grid", FullGrid(3, 3), []);
        var markdown = await RenderSingleSheetAsync(sheet);

        // The table header names each column letter and the separator row proves a real GFM table
        Assert.Contains("| A | B | C |", markdown, StringComparison.Ordinal);
        Assert.Contains("| --- |", markdown, StringComparison.Ordinal);
        // The listing is still present and addressable
        Assert.Contains("- `A1`:", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a genuinely sparse region (fill density below the threshold) keeps the address
    ///     listing alone, with no grid table.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_SparseGrid_KeepsListingOnly()
    {
        // 4 of 9 cells populated over a 3x3 bounding rectangle => density 0.44, below the 0.50 gate
        var cells = new List<ExcelCellModel>
        {
            new("A1", "a", null), new("C1", "b", null), new("B2", "c", null), new("A3", "d", null)
        };
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Sparse", cells, []));

        Assert.DoesNotContain("| --- |", markdown, StringComparison.Ordinal);
        Assert.Contains("- `A1`:", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a very wide region (more columns than a markdown table can carry readably) keeps the
    ///     address listing alone.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_VeryWideGrid_KeepsListingOnly()
    {
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Wide", FullGrid(3, 31), []));

        Assert.DoesNotContain("| --- |", markdown, StringComparison.Ordinal);
        Assert.Contains("- `A1`:", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a long cell is elided in the table only, with its full verbatim value preserved at
    ///     full length in the address listing below — the verbatim-full-length guarantee is not
    ///     weakened by the table.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_LongCell_ElidedInTableVerbatimInListing()
    {
        var longValue = new string('x', 200);
        var cells = FullGrid(3, 2).ToList();
        cells[0] = new ExcelCellModel("A1", longValue, null); // replace A1 with a long prose cell
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Prose", cells, []));

        // The table elides the long cell for readability
        Assert.Contains("…", markdown, StringComparison.Ordinal);
        // The listing reproduces the full value verbatim, at full length
        Assert.Contains("- `A1`: " + longValue, markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a formula cell shows its cached value in the table while the formula is preserved
    ///     verbatim, per cell, in the address listing.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_FormulaCellInGrid_ValueInTableFormulaInListing()
    {
        var cells = FullGrid(3, 2).ToList();
        cells[0] = new ExcelCellModel("A1", "5", "A1+A2");
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Calc", cells, []));

        Assert.Contains("| --- |", markdown, StringComparison.Ordinal);
        Assert.Contains("(formula: `=A1+A2`)", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves merged ranges are stated as a note, because a GFM table cannot visually span cells.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_MergedRanges_StatedAsNote()
    {
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Merged", FullGrid(3, 3), ["A1:C1"]));

        Assert.Contains("Merged ranges: A1:C1", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a multi-line cell is elided in the table and the elision is explained by a note,
    ///     so a reader cannot mistake the marker for content that was not recovered.
    /// </summary>
    /// <remarks>
    ///     Modeled on a real workbook whose <c>A1</c> renders as <c>…</c> purely because its value,
    ///     <c>Assumptions:</c>, carries a newline — three characters of content that a reader could
    ///     reasonably have concluded were lost.
    /// </remarks>
    [Fact]
    public async Task ExcelContentEmitter_Emit_ElidedCell_ExplainsTheElisionNearTheTable()
    {
        var cells = FullGrid(3, 2).ToList();
        cells[0] = new ExcelCellModel("A1", "Assumptions:\n\nFrom the requirements.", null);
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Notes", cells, []));

        // The note explains the marker and points at the listing, which still carries the full value
        Assert.Contains("| … |", markdown, StringComparison.Ordinal);
        Assert.Contains("Their full values appear verbatim in the cell listing below._", markdown, StringComparison.Ordinal);
        Assert.Contains("- `A1`: Assumptions:", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the elision note is absent when no cell was actually elided, so the caveat never
    ///     spends a reader's tokens on a limit that did not apply.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_NoElidedCell_OmitsTheElisionNote()
    {
        var markdown = await RenderSingleSheetAsync(new ExcelSheetModel("Plain", FullGrid(3, 3), []));

        Assert.Contains("| --- |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Their full values appear verbatim in the cell listing below._", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the workbook's structure is reported as content features, so the summary can say
    ///     what the extracted content holds rather than only how many characters it runs to.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_Workbook_ReportsContentFeatures()
    {
        // Arrange: a workbook of one sheet with a formula cell
        var cells = FullGrid(3, 2).ToList();
        cells[0] = new ExcelCellModel("A1", "5", "A1+A2");
        var sink = new RecordingSink();
        var model = new ExcelWorkbookModel([new ExcelSheetModel("Calc", cells, [])]);

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the counts come from the model, not from scanning the rendered markdown
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "worksheets" && feature.Count == 1);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "populated cells" && feature.Count == cells.Count);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "cells carrying a formula" && feature.Count == 1);
    }

    /// <summary>
    ///     Proves each chart a worksheet shows becomes its own titled chart part, emitted straight
    ///     after the sheet that shows it, and counted in the content inventory.
    /// </summary>
    /// <remarks>
    ///     A chart's cached series can run to hundreds of rows; emitting it as its own part keeps the
    ///     sheet's cells readable and gives the data a path a consumer can cite directly.
    /// </remarks>
    [Fact]
    public async Task ExcelContentEmitter_Emit_SheetWithChart_WritesChartPartAfterSheet()
    {
        // Arrange: a one-sheet workbook whose sheet shows one readable chart
        var model = new ExcelWorkbookModel([SheetWithChart(ReadableChart())]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: two parts, the chart second, titled by the chart and typed as a chart
        Assert.Equal(2, sink.Parts.Count);
        Assert.Equal(ContentPartKind.Sheet, sink.Parts[0].Part.Kind);
        Assert.Equal(ContentPartKind.Chart, sink.Parts[1].Part.Kind);
        Assert.Equal("Tank Pressure Trend", sink.Parts[1].Part.Title);
        Assert.Contains("| 0 | 0 | 101.3 |", sink.Parts[1].Markdown, StringComparison.Ordinal);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "charts" && feature.Count == 1);
    }

    /// <summary>
    ///     Proves the worksheet part names the chart it shows, so a reader of the sheet alone learns
    ///     the chart exists and that its data is elsewhere.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_SheetWithChart_NamesChartUnderTheSheet()
    {
        // Arrange: a one-sheet workbook showing one chart
        var model = new ExcelWorkbookModel([SheetWithChart(ReadableChart())]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the sheet part carries the chart reference
        Assert.Contains("Tank Pressure Trend", sink.Parts[0].Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart carrying no cached data still gets its own chart part stating that fact,
    ///     with no sink note because nothing failed to be read.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_ChartWithoutCache_WritesChartPartOnly()
    {
        // Arrange: a chart whose series references a range but caches no values
        var chart = new ExcelChartModel("/xl/charts/chart1.xml", "Results", new ExcelChartData(
            "Uncached chart", TitleIsAutomatic: false, ["line"], null, null, [], null,
            [new ExcelChartSeries("Run A", "Sheet1!$A$1:$A$9", null, 0, [])]), null);
        var model = new ExcelWorkbookModel([SheetWithChart(chart)]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the chart part states the absence and the sink records no note
        var chartPart = Assert.Single(sink.Parts, part => part.Part.Kind == ContentPartKind.Chart);
        Assert.Contains("declares no cached data points", chartPart.Markdown, StringComparison.Ordinal);
        Assert.Contains("Sheet1!$A$1:$A$9", chartPart.Markdown, StringComparison.Ordinal);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a chart part that could not be read records a note naming the chart and still gets
    ///     its own chart part, so an unreadable chart is never mistaken for an absent one.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_UnreadableChart_ReportsNoteAndKeepsChartPart()
    {
        // Arrange: a chart the reader could not parse
        var chart = new ExcelChartModel("/xl/charts/chart1.xml", "Results", null, "the part is not well-formed XML");
        var model = new ExcelWorkbookModel([SheetWithChart(chart)]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the unreadable chart is named in a note and still has a part of its own
        var note = Assert.Single(sink.Notes);
        Assert.Contains("could not be read", note.Message, StringComparison.Ordinal);
        Assert.Contains("/xl/charts/chart1.xml", note.Message, StringComparison.Ordinal);
        var chartPart = Assert.Single(sink.Parts, part => part.Part.Kind == ContentPartKind.Chart);
        Assert.Contains("could not be read", chartPart.Markdown, StringComparison.Ordinal);
        Assert.Contains("not well-formed XML", chartPart.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart cached beyond the rendering bound states the truncation in its own chart
    ///     part, with no sink note because the chart was still read successfully.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_ChartBeyondBound_StatesTruncationInChartPart()
    {
        // Arrange: a chart with one more point than the bound allows
        var count = ExcelChartWriter.MaxPlottedPoints + 1;
        var points = Enumerable.Range(0, count).Select(index => new ExcelChartPoint(index, "1")).ToList();
        var chart = new ExcelChartModel("/xl/charts/chart1.xml", "Results", new ExcelChartData(
            "Long sweep", TitleIsAutomatic: false, ["line"], null, null, [], null,
            [new ExcelChartSeries("Sweep", null, null, count, points)]), null);
        var model = new ExcelWorkbookModel([SheetWithChart(chart)]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the truncation is stated in the chart part itself, not as a sink note
        var chartPart = Assert.Single(sink.Parts, part => part.Part.Kind == ContentPartKind.Chart);
        Assert.Contains(
            $"Showing the first {ExcelChartWriter.MaxPlottedPoints} of {count} plotted points",
            chartPart.Markdown,
            StringComparison.Ordinal);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a workbook showing no chart records no chart-related note, so the chart ledger
    ///     never invents incomplete work for an ordinary chart-free workbook.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_NoCharts_ReportsNoChartNotes()
    {
        // Arrange: an ordinary one-sheet workbook
        var model = new ExcelWorkbookModel([new ExcelSheetModel("S", [new ExcelCellModel("A1", "x", null)], [])]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: nothing is said about charts that do not exist
        Assert.DoesNotContain(
            sink.Notes,
            note => note.Message.Contains("chart", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Proves the text of a worksheet's drawing shapes reaches the sheet part, because a callout
    ///     carrying a part number lives in no cell and would otherwise be dropped in silence.
    /// </summary>
    [Fact]
    public async Task ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet()
    {
        // Arrange: a sheet with one annotated drawing shape
        var sheet = new ExcelSheetModel(
            "Layout", [new ExcelCellModel("A1", "x", null)], [], [], [], ["Bearing retainer, part LX-4120"]);
        var model = new ExcelWorkbookModel([sheet]);
        var sink = new RecordingSink();

        // Act: emit the workbook
        await ExcelContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        // Assert: the annotation appears under its own heading and is counted
        Assert.Contains("Text on drawing shapes:", sink.Parts[0].Markdown, StringComparison.Ordinal);
        Assert.Contains("Bearing retainer, part LX-4120", sink.Parts[0].Markdown, StringComparison.Ordinal);
        Assert.Contains(
            sink.ContentFeatures, feature => feature.Label == "annotated drawing shapes" && feature.Count == 1);
    }

    /// <summary>
    ///     Builds a one-sheet model showing the supplied chart.
    /// </summary>
    /// <param name="chart">The chart the sheet shows.</param>
    /// <returns>The sheet model.</returns>
    private static ExcelSheetModel SheetWithChart(ExcelChartModel chart) =>
        new("Results", [new ExcelCellModel("A1", "x", null)], [], [], [chart]);

    /// <summary>
    ///     Builds a readable two-point chart with an invented title and axes.
    /// </summary>
    /// <returns>The chart model.</returns>
    private static ExcelChartModel ReadableChart() =>
        new("/xl/charts/chart1.xml", "Results", new ExcelChartData(
            "Tank Pressure Trend", TitleIsAutomatic: false, ["line"],
            "Elapsed time (min)", "Pressure (kPa)",
            [new ExcelChartPoint(0, "0"), new ExcelChartPoint(1, "5")], "General",
            [new ExcelChartSeries("Vessel A", "Sheet1!$B$2:$B$3", "0.0", 2,
                [new ExcelChartPoint(0, "101.3"), new ExcelChartPoint(1, "104.8")])]), null);

    /// <summary>
    ///     Proves an Excel workbook's inline image links resolve on disk from their <c>parts/*.md</c>
    ///     files — the exact defect this fix targets.
    /// </summary>
    /// <remarks>
    ///     Excel always writes each sheet through <c>AddContentPartAsync</c>, so every sheet is
    ///     routed into <c>parts/</c>. This drives the real <see cref="ExtractionSink"/>
    ///     + <see cref="ContentWriter"/> over a temp folder and resolves each link against its own
    ///     file, catching the dangling <c>parts/images/…</c> that string assertions missed.
    /// </remarks>
    [Fact]
    public async Task ExcelContentEmitter_Emit_ImageLinks_ResolveOnDisk()
    {
        // Act: emit an image-bearing workbook and finalize into parts/
        var resolved = await EmitAndResolveLinksAsync();

        // Assert: at least one image link was checked and all resolved on disk
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Emits an image-bearing workbook through a real sink and content writer, then asserts every
    ///     inline image link resolves on disk.
    /// </summary>
    /// <returns>The number of local resource links that were checked and resolved.</returns>
    /// <remarks>
    ///     Uses a real <see cref="ExtractionSink"/> over a temporary scratch folder so the image bytes
    ///     and sheet part files reach disk and link resolution is genuine rather than a string check.
    /// </remarks>
    private static async Task<int> EmitAndResolveLinksAsync()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true };
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/xl/media/image1.png", AltText: "Sales chart", SourcePages: [1]);
        var sheet = new ExcelSheetModel("Charts", [new ExcelCellModel("A1", "x", null)], [],
            [new ExcelSheetImageRef("/xl/media/image1.png", "Sales chart")]);
        var model = new ExcelWorkbookModel([sheet], [image]);

        await ExcelContentEmitter.EmitAsync(sink, options, model, Ct);
        await ContentWriter.WriteAsync(sink, "Workbook", Ct);

        return MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(folder.AbsolutePath);
    }

    /// <summary>
    ///     Emits a single-sheet workbook through the sink and returns the sheet part's markdown.
    /// </summary>
    /// <param name="sheet">The sheet to render.</param>
    /// <returns>The rendered markdown of the single sheet part.</returns>
    /// <remarks>Images are disabled so the run stays focused on sheet rendering alone.</remarks>
    private static async Task<string> RenderSingleSheetAsync(ExcelSheetModel sheet)
    {
        var sink = new RecordingSink();
        await ExcelContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, new ExcelWorkbookModel([sheet]), Ct);
        return Assert.Single(sink.Parts).Markdown;
    }

    /// <summary>
    ///     Builds a fully populated rectangular grid of cells, each carrying a distinct short value.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <returns>The grid's cells in row-major order.</returns>
    /// <remarks>Column references use spreadsheet letters so wide grids exercise multi-letter columns.</remarks>
    private static IReadOnlyList<ExcelCellModel> FullGrid(int rows, int columns)
    {
        var cells = new List<ExcelCellModel>();
        for (var row = 1; row <= rows; row++)
        {
            for (var column = 1; column <= columns; column++)
            {
                cells.Add(new ExcelCellModel(ColumnLetters(column) + row, $"r{row}c{column}", null));
            }
        }

        return cells;
    }

    /// <summary>Converts a 1-based column number to its spreadsheet letters (1 to A, 27 to AA).</summary>
    /// <param name="column">The 1-based column number.</param>
    /// <returns>The column's letters.</returns>
    private static string ColumnLetters(int column)
    {
        var letters = string.Empty;
        var remaining = column;
        while (remaining > 0)
        {
            var zeroBased = (remaining - 1) % 26;
            letters = (char)('A' + zeroBased) + letters;
            remaining = (remaining - 1) / 26;
        }

        return letters;
    }
}
