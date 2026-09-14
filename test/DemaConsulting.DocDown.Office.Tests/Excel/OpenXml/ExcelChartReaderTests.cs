using System.Text;
using DocDown.Excel.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Excel.OpenXml;

/// <summary>
///     Unit tests for <see cref="ExcelChartReader"/>, exercising the cached-data parsing rules
///     against hand-written chart XML with no package behind it.
/// </summary>
/// <remarks>
///     Every chart in these tests is invented: the titles, series names, axis labels, and numbers
///     describe a fictional test rig and come from no real document.
/// </remarks>
public class ExcelChartReaderTests
{
    /// <summary>
    ///     Proves a single-series line chart yields its authored title, axis titles, categories, and
    ///     values, which is the whole reason cached data beats a picture of the plot.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_SingleSeries_ReadsTitlesAxesAndValues()
    {
        // Arrange: a line chart with an authored title, both axis titles, and three plotted points
        var xml = ChartXml(
            title: RichTitle("Tank Pressure Trend"),
            categoryAxisTitle: "Elapsed time (min)",
            valueAxisTitle: "Pressure (kPa)",
            series: [SeriesXml("Vessel A", "Sheet1!$B$2:$B$4", ["10", "20", "30"], ["0", "101.3", "102.7"])]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: the labeling and both cached ranges survive
        Assert.Equal("Tank Pressure Trend", data.Title);
        Assert.False(data.TitleIsAutomatic);
        Assert.Equal(["line"], data.ChartTypes);
        Assert.Equal("Elapsed time (min)", data.CategoryAxisTitle);
        Assert.Equal("Pressure (kPa)", data.ValueAxisTitle);
        Assert.Equal(["10", "20", "30"], data.Categories.Select(point => point.Value));
        var series = Assert.Single(data.Series);
        Assert.Equal("Vessel A", series.Name);
        Assert.Equal("Sheet1!$B$2:$B$4", series.SourceReference);
        Assert.Equal(["0", "101.3", "102.7"], series.Points.Select(point => point.Value));
    }

    /// <summary>
    ///     Proves a chart with no authored title reports the title the application generates from its
    ///     sole series name, flagged as generated so the output never implies an author wrote it.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_AutomaticTitle_ReportsSeriesNameAsGenerated()
    {
        // Arrange: an empty title element, as a chart whose title was never edited carries
        var xml = ChartXml(
            title: "<c:title><c:overlay val=\"0\"/></c:title>",
            categoryAxisTitle: null,
            valueAxisTitle: null,
            series: [SeriesXml("Coolant flow", "Sheet1!$C$2:$C$3", ["1", "2"], ["4", "5"])]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: the series name stands in as the title and is marked generated
        Assert.Equal("Coolant flow", data.Title);
        Assert.True(data.TitleIsAutomatic);
    }

    /// <summary>
    ///     Proves a deleted automatic title yields no title at all rather than borrowing the series name.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_DeletedAutoTitle_ReportsNoTitle()
    {
        // Arrange: the same empty title, but with the automatic title explicitly deleted
        var xml = ChartXml(
            title: "<c:title/><c:autoTitleDeleted val=\"1\"/>",
            categoryAxisTitle: null,
            valueAxisTitle: null,
            series: [SeriesXml("Coolant flow", null, ["1"], ["4"])]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: no title is claimed
        Assert.Null(data.Title);
        Assert.False(data.TitleIsAutomatic);
    }

    /// <summary>
    ///     Proves a sparse cache keeps its declared point indices rather than being packed down, so a
    ///     value never silently moves to a row it does not belong to.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_SparseIndices_PreservesDeclaredIndices()
    {
        // Arrange: a series declaring five points but caching only indices 0, 2, and 4
        var values = "<c:ptCount val=\"5\"/>"
            + Point(0, "1.5") + Point(2, "3.5") + Point(4, "5.5");
        var xml = ChartXml(
            title: RichTitle("Sparse run"),
            categoryAxisTitle: null,
            valueAxisTitle: null,
            series: [$"<c:ser><c:tx><c:v>Run A</c:v></c:tx><c:val><c:numRef><c:f>Sheet1!$A$1:$A$5</c:f>"
                + $"<c:numCache>{values}</c:numCache></c:numRef></c:val></c:ser>"]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: the gaps in the cache survive as gaps in the indices
        var series = Assert.Single(data.Series);
        Assert.Equal([0, 2, 4], series.Points.Select(point => point.Index));
        Assert.Equal(5, series.DeclaredPointCount);
    }

    /// <summary>
    ///     Proves every series of a multi-series chart is read, because a design that assumed one
    ///     series would drop the rest of a comparison chart without a word.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_MultipleSeries_ReadsEverySeries()
    {
        // Arrange: two series over the same categories
        var xml = ChartXml(
            title: RichTitle("Chamber comparison"),
            categoryAxisTitle: "Cycle",
            valueAxisTitle: "Temperature (C)",
            series:
            [
                SeriesXml("Chamber 1", "Sheet1!$B$2:$B$3", ["1", "2"], ["21.0", "22.5"]),
                SeriesXml("Chamber 2", "Sheet1!$C$2:$C$3", ["1", "2"], ["19.5", "20.1"])
            ]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: both series and both value sets are present
        Assert.Equal(2, data.Series.Count);
        Assert.Equal("Chamber 1", data.Series[0].Name);
        Assert.Equal("Chamber 2", data.Series[1].Name);
        Assert.Equal(["19.5", "20.1"], data.Series[1].Points.Select(point => point.Value));
    }

    /// <summary>
    ///     Proves a series whose reference carries no cache yields no points but keeps its source
    ///     reference, so the emitter can report the loss and still say where the data came from.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_MissingCache_KeepsReferenceAndYieldsNoPoints()
    {
        // Arrange: a series with a formula reference and no cached values at all
        var xml = ChartXml(
            title: RichTitle("Uncached chart"),
            categoryAxisTitle: null,
            valueAxisTitle: null,
            series: ["<c:ser><c:tx><c:v>Run A</c:v></c:tx><c:val><c:numRef><c:f>Sheet1!$A$1:$A$9</c:f>"
                + "</c:numRef></c:val></c:ser>"]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: nothing is invented, and the reference survives for the reader
        var series = Assert.Single(data.Series);
        Assert.Empty(series.Points);
        Assert.Equal("Sheet1!$A$1:$A$9", series.SourceReference);
    }

    /// <summary>
    ///     Proves values typed directly into a chart (a literal cache with no source range) are read,
    ///     because such data exists nowhere else in the workbook.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_LiteralValues_ReadsPoints()
    {
        // Arrange: a series whose values are literals rather than a reference
        var xml = ChartXml(
            title: RichTitle("Typed data"),
            categoryAxisTitle: null,
            valueAxisTitle: null,
            series: ["<c:ser><c:val><c:numLit><c:ptCount val=\"2\"/>"
                + Point(0, "7") + Point(1, "9") + "</c:numLit></c:val></c:ser>"]);

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: the literal points are recovered and no source range is claimed
        var series = Assert.Single(data.Series);
        Assert.Equal(["7", "9"], series.Points.Select(point => point.Value));
        Assert.Null(series.SourceReference);
    }

    /// <summary>
    ///     Proves a scatter series, which spells its values <c>c:yVal</c> and its categories
    ///     <c>c:xVal</c>, is read like any other — the name-driven walk covers plot types this product
    ///     was not written against.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_ScatterSeries_ReadsXAndYValues()
    {
        // Arrange: a scatter chart with x and y caches
        var xml =
            "<?xml version=\"1.0\"?><c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" "
            + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><c:chart><c:plotArea>"
            + "<c:scatterChart><c:ser><c:tx><c:v>Deflection</c:v></c:tx>"
            + "<c:xVal><c:numRef><c:f>Sheet1!$A$1:$A$2</c:f><c:numCache><c:ptCount val=\"2\"/>"
            + Point(0, "0") + Point(1, "5") + "</c:numCache></c:numRef></c:xVal>"
            + "<c:yVal><c:numRef><c:f>Sheet1!$B$1:$B$2</c:f><c:numCache><c:ptCount val=\"2\"/>"
            + Point(0, "0.01") + Point(1, "0.04") + "</c:numCache></c:numRef></c:yVal>"
            + "</c:ser></c:scatterChart></c:plotArea></c:chart></c:chartSpace>";

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: both axes of the scatter are recovered under the same model
        Assert.Equal(["scatter"], data.ChartTypes);
        Assert.Equal(["0", "5"], data.Categories.Select(point => point.Value));
        Assert.Equal(["0.01", "0.04"], Assert.Single(data.Series).Points.Select(point => point.Value));
    }

    /// <summary>
    ///     Proves a chart part with no chart element at all yields an empty model rather than throwing,
    ///     so a malformed part degrades the chart and not the whole extraction.
    /// </summary>
    [Fact]
    public void ExcelChartReader_ReadChartData_NoChartElement_YieldsEmptyModel()
    {
        // Arrange: a well-formed chart space carrying no chart
        var xml = "<?xml version=\"1.0\"?><c:chartSpace "
            + "xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/>";

        // Act: read the chart part
        var data = ExcelChartReader.ReadChartData(ToStream(xml));

        // Assert: nothing is claimed about a chart that is not there
        Assert.Null(data.Title);
        Assert.Empty(data.Series);
        Assert.Empty(data.Categories);
    }

    /// <summary>
    ///     Builds a one-plot-type chart part around the supplied title, axes, and series XML.
    /// </summary>
    /// <param name="title">The title element XML, or an empty string for none.</param>
    /// <param name="categoryAxisTitle">The category axis title text, or <see langword="null"/> for an untitled axis.</param>
    /// <param name="valueAxisTitle">The value axis title text, or <see langword="null"/> for an untitled axis.</param>
    /// <param name="series">The series element XML fragments.</param>
    /// <returns>The chart part XML.</returns>
    private static string ChartXml(
        string title, string? categoryAxisTitle, string? valueAxisTitle, IReadOnlyList<string> series)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\"?><c:chartSpace ")
            .Append("xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" ")
            .Append("xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><c:chart>")
            .Append(title).Append("<c:plotArea><c:lineChart>");
        foreach (var element in series)
        {
            builder.Append(element);
        }

        builder.Append("</c:lineChart>")
            .Append("<c:catAx>").Append(AxisTitleXml(categoryAxisTitle)).Append("</c:catAx>")
            .Append("<c:valAx>").Append(AxisTitleXml(valueAxisTitle)).Append("</c:valAx>")
            .Append("</c:plotArea></c:chart></c:chartSpace>");
        return builder.ToString();
    }

    /// <summary>
    ///     Builds a series with cached categories and values.
    /// </summary>
    /// <param name="name">The series name.</param>
    /// <param name="sourceReference">The value source formula, or <see langword="null"/> for none.</param>
    /// <param name="categories">The cached category values.</param>
    /// <param name="values">The cached series values.</param>
    /// <returns>The series XML fragment.</returns>
    private static string SeriesXml(
        string name, string? sourceReference, IReadOnlyList<string> categories, IReadOnlyList<string> values)
    {
        var builder = new StringBuilder();
        builder.Append("<c:ser><c:tx><c:v>").Append(name).Append("</c:v></c:tx>");
        builder.Append("<c:cat><c:numRef><c:f>Sheet1!$A$2:$A$4</c:f><c:numCache><c:formatCode>General</c:formatCode>")
            .Append("<c:ptCount val=\"").Append(categories.Count).Append("\"/>");
        for (var index = 0; index < categories.Count; index++)
        {
            builder.Append(Point(index, categories[index]));
        }

        builder.Append("</c:numCache></c:numRef></c:cat><c:val><c:numRef><c:f>")
            .Append(sourceReference ?? string.Empty)
            .Append("</c:f><c:numCache><c:formatCode>0.0</c:formatCode><c:ptCount val=\"")
            .Append(values.Count).Append("\"/>");
        for (var index = 0; index < values.Count; index++)
        {
            builder.Append(Point(index, values[index]));
        }

        builder.Append("</c:numCache></c:numRef></c:val></c:ser>");
        return builder.ToString();
    }

    /// <summary>
    ///     Builds one cached point at an explicit index.
    /// </summary>
    /// <param name="index">The point index.</param>
    /// <param name="value">The cached value.</param>
    /// <returns>The point XML fragment.</returns>
    private static string Point(int index, string value) =>
        $"<c:pt idx=\"{index}\"><c:v>{value}</c:v></c:pt>";

    /// <summary>
    ///     Builds an authored rich-text chart title.
    /// </summary>
    /// <param name="text">The title text.</param>
    /// <returns>The title XML fragment.</returns>
    private static string RichTitle(string text) =>
        $"<c:title><c:tx><c:rich><a:p><a:r><a:t>{text}</a:t></a:r></a:p></c:rich></c:tx></c:title>";

    /// <summary>
    ///     Builds an axis title element, or an empty fragment for an untitled axis.
    /// </summary>
    /// <param name="text">The axis title text, or <see langword="null"/>.</param>
    /// <returns>The axis title XML fragment.</returns>
    private static string AxisTitleXml(string? text) =>
        text is null ? string.Empty : $"<c:title><c:tx><c:rich><a:p><a:r><a:t>{text}</a:t></a:r></a:p></c:rich></c:tx></c:title>";

    /// <summary>
    ///     Opens a readable stream over chart XML.
    /// </summary>
    /// <param name="xml">The chart XML.</param>
    /// <returns>The stream.</returns>
    private static Stream ToStream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));
}
