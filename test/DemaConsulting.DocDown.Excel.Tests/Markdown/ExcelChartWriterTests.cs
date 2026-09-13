using DocDown.Excel.Markdown;
using DocDown.Excel.OpenXml;

namespace DemaConsulting.DocDown.Excel.Tests.Markdown;

/// <summary>
///     Unit tests for <see cref="ExcelChartWriter"/>, exercising the table rendering, the sparse-index
///     handling, and the bound on plotted points from hand-built chart models.
/// </summary>
/// <remarks>
///     Every chart in these tests is invented: the titles, series names, axis labels, and numbers
///     describe a fictional test rig and come from no real document.
/// </remarks>
public class ExcelChartWriterTests
{
    /// <summary>
    ///     Proves a single-series chart renders its labeling and a category-against-value table, which
    ///     is what lets a model compute from a chart it can never see.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_SingleSeries_RendersLabelingAndTable()
    {
        // Arrange: a two-point chart with both axis titles
        var chart = Chart(new ExcelChartData(
            "Tank Pressure Trend", TitleIsAutomatic: false, ["line"],
            "Elapsed time (min)", "Pressure (kPa)",
            [new ExcelChartPoint(0, "0"), new ExcelChartPoint(1, "5")], "General",
            [new ExcelChartSeries("Vessel A", "Sheet1!$B$2:$B$3", "0.0", 2,
                [new ExcelChartPoint(0, "101.3"), new ExcelChartPoint(1, "104.8")])]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the context lines and the data rows are both present
        Assert.True(render.HasData);
        Assert.Equal(2, render.RenderedPoints);
        Assert.Contains("# Tank Pressure Trend", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("- Category axis: Elapsed time (min)", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("- Value axis: Pressure (kPa)", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("| Point | Elapsed time (min) | Vessel A |", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("| 1 | 5 | 104.8 |", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a sparse cache renders at its declared indices with empty cells where the cache has
    ///     none, rather than packing values into rows they do not belong to.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_SparseIndices_RendersDeclaredIndicesWithEmptyCells()
    {
        // Arrange: categories at 0, 1, and 2 but values only at 0 and 2
        var chart = Chart(new ExcelChartData(
            "Sparse run", TitleIsAutomatic: false, ["line"], null, null,
            [new ExcelChartPoint(0, "a"), new ExcelChartPoint(1, "b"), new ExcelChartPoint(2, "c")], null,
            [new ExcelChartSeries("Run A", null, null, 3,
                [new ExcelChartPoint(0, "1"), new ExcelChartPoint(2, "3")])]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the missing point renders as an empty cell at its own index
        Assert.Contains("| 1 | b |  |", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("| 2 | c | 3 |", render.Markdown, StringComparison.Ordinal);
        Assert.Equal(3, render.RenderedPoints);
    }

    /// <summary>
    ///     Proves every series of a multi-series chart gets its own column, so a comparison chart is
    ///     not reduced to one of the things being compared.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_MultipleSeries_RendersOneColumnPerSeries()
    {
        // Arrange: two named series over one category set
        var chart = Chart(new ExcelChartData(
            "Chamber comparison", TitleIsAutomatic: false, ["line"], "Cycle", "Temperature (C)",
            [new ExcelChartPoint(0, "1")], null,
            [
                new ExcelChartSeries("Chamber 1", null, null, 1, [new ExcelChartPoint(0, "21.0")]),
                new ExcelChartSeries("Chamber 2", null, null, 1, [new ExcelChartPoint(0, "19.5")])
            ]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: both series head their own column and both values appear on the row
        Assert.Contains("| Point | Cycle | Chamber 1 | Chamber 2 |", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("| 0 | 1 | 21.0 | 19.5 |", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an unnamed series is labeled by its position so its column is still referable.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_UnnamedSeries_LabelsByPosition()
    {
        // Arrange: a series carrying no name
        var chart = Chart(new ExcelChartData(
            "Unnamed", TitleIsAutomatic: false, [], null, null, [], null,
            [new ExcelChartSeries(null, null, null, 1, [new ExcelChartPoint(0, "4")])]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the column is headed by position
        Assert.Contains("| Point | Category | Series 1 |", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart cached beyond the bound is truncated and says so in the document itself, so
    ///     the bound is a stated choice rather than a silent loss.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_BeyondBound_TruncatesAndStatesIt()
    {
        // Arrange: one more point than the bound allows
        var count = ExcelChartWriter.MaxPlottedPoints + 1;
        var points = Enumerable.Range(0, count)
            .Select(index => new ExcelChartPoint(index, index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        var chart = Chart(new ExcelChartData(
            "Long sweep", TitleIsAutomatic: false, ["line"], null, null, [], null,
            [new ExcelChartSeries("Sweep", null, null, count, points)]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the table stops at the bound and the note names both counts
        Assert.True(render.Truncated);
        Assert.Equal(ExcelChartWriter.MaxPlottedPoints, render.RenderedPoints);
        Assert.Equal(count, render.TotalPoints);
        Assert.Contains(
            $"Showing the first {ExcelChartWriter.MaxPlottedPoints} of {count} plotted points",
            render.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"| {count - 1} |", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart exactly at the bound is not truncated, so the limit is inclusive and an
    ///     ordinary chart never carries a truncation caveat it did not earn.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_ExactlyAtBound_DoesNotTruncate()
    {
        // Arrange: exactly as many points as the bound allows
        var points = Enumerable.Range(0, ExcelChartWriter.MaxPlottedPoints)
            .Select(index => new ExcelChartPoint(index, "1"))
            .ToList();
        var chart = Chart(new ExcelChartData(
            "At bound", TitleIsAutomatic: false, [], null, null, [], null,
            [new ExcelChartSeries("Sweep", null, null, points.Count, points)]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: nothing is dropped and no caveat is emitted
        Assert.False(render.Truncated);
        Assert.DoesNotContain("Showing the first", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart whose series cache no points says so and reports no data, so the emitter can
    ///     raise the honest gap instead of writing an empty table.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_NoCachedPoints_StatesAbsenceAndReportsNoData()
    {
        // Arrange: a series with a source reference but no cached values
        var chart = Chart(new ExcelChartData(
            "Uncached chart", TitleIsAutomatic: false, ["bar"], null, null, [], null,
            [new ExcelChartSeries("Run A", "Sheet1!$A$1:$A$9", null, 0, [])]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the absence is stated and the source reference is still offered
        Assert.False(render.HasData);
        Assert.Contains("declares no cached data points", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("Sheet1!$A$1:$A$9", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a chart whose part could not be read still produces a part naming it and stating the
    ///     reason — the failure this whole feature exists to prevent is a chart that vanishes.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_UnreadableChart_StatesTheReason()
    {
        // Arrange: a chart the reader could not parse
        var chart = new ExcelChartModel("/xl/charts/chart1.xml", "Results", null, "the part is not well-formed XML");

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the chart is named, the reason is given, and no data is claimed
        Assert.False(render.HasData);
        Assert.Contains("could not be read", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("not well-formed XML", render.Markdown, StringComparison.Ordinal);
        Assert.Contains("/xl/charts/chart1.xml", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a pipe in a cached label cannot break the table's column structure.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Render_PipeInLabel_EscapesIt()
    {
        // Arrange: a category label containing a pipe character
        var chart = Chart(new ExcelChartData(
            "Escaping", TitleIsAutomatic: false, [], null, null,
            [new ExcelChartPoint(0, "A|B")], null,
            [new ExcelChartSeries("Run", null, null, 1, [new ExcelChartPoint(0, "1")])]));

        // Act: render the chart part
        var render = ExcelChartWriter.Render(chart);

        // Assert: the pipe is escaped rather than ending the cell
        Assert.Contains("| 0 | A\\|B | 1 |", render.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the worksheet-side description names the chart and points at its own part.
    /// </summary>
    [Fact]
    public void ExcelChartWriter_Describe_ReadableChart_NamesChartAndItsPart()
    {
        // Arrange: a readable line chart
        var chart = Chart(new ExcelChartData(
            "Tank Pressure Trend", TitleIsAutomatic: false, ["line"], null, null, [], null, []));

        // Act: describe it for its worksheet
        var description = ExcelChartWriter.Describe(chart);

        // Assert: the sheet's reader learns the chart exists and where its data is
        Assert.Contains("Tank Pressure Trend", description, StringComparison.Ordinal);
        Assert.Contains("(line)", description, StringComparison.Ordinal);
        Assert.Contains("separate chart part", description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Wraps chart data in a chart model on a fictional worksheet.
    /// </summary>
    /// <param name="data">The chart data to wrap.</param>
    /// <returns>The chart model.</returns>
    private static ExcelChartModel Chart(ExcelChartData data) =>
        new("/xl/charts/chart1.xml", "Results", data, null);
}
