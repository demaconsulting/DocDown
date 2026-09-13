using System.Globalization;
using System.Text;
using DocDown.Excel.OpenXml;

namespace DocDown.Excel.Markdown;

/// <summary>
///     Renders one chart's cached data as markdown: the labeling that makes the numbers mean
///     something, then a table of categories against series values.
/// </summary>
/// <remarks>
///     <para>
///         A chart's cached series is the only form in which a chart's information survives into a
///         text extraction: a picture of the plot shows a shape, while the cache is the plotted series,
///         the test sweep, or the yield curve itself, which a consumer can compute from. Rendering it
///         as a table — categories in the first column, one column per series — lets a reader consume
///         it with no knowledge of the chart grammar at all.
///     </para>
///     <para>
///         The table follows the worksheet grid-table conventions of
///         <see cref="ExcelContentEmitter"/>: a leading index column that keeps every row addressable,
///         pipe characters escaped so a value cannot break the column structure, and an explanatory
///         note emitted only when it actually applies. Point indices are honored rather than assumed
///         dense, so a sparse cache renders as the sparse data it is instead of silently shifting
///         values into the wrong rows. Pure; performs no I/O.
///     </para>
/// </remarks>
internal static class ExcelChartWriter
{
    /// <summary>The maximum number of plotted points rendered into a chart's data table.</summary>
    /// <remarks>
    ///     A chart's cached series has no inherent size limit — a logged sweep can cache tens of
    ///     thousands of points — while the extraction exists to be read by a model with a bounded
    ///     context. The measured real chart carries 101 points and renders in about two kilobytes, so
    ///     500 points (roughly ten kilobytes for a single-series chart) keeps every ordinary engineering
    ///     chart complete while capping a pathological one at a size that cannot crowd out the rest of
    ///     the workbook. Truncation is never silent: the chart part itself says what it omitted.
    /// </remarks>
    internal const int MaxPlottedPoints = 500;

    /// <summary>The label used for the category column when the chart names no category axis.</summary>
    /// <remarks>Named so the header and any test that pins it cannot drift.</remarks>
    private const string CategoryColumnFallback = "Category";

    /// <summary>
    ///     Renders a chart to markdown.
    /// </summary>
    /// <param name="chart">The chart to render. Must not be null.</param>
    /// <returns>The chart part markdown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Three outcomes are possible and all three are stated in the markdown itself: a chart whose
    ///     part could not be read, a chart that was read but caches no values, and a chart with data.
    ///     Pure.
    /// </remarks>
    public static string Render(ExcelChartModel chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        var builder = new StringBuilder();
        builder.Append("# ").Append(Heading(chart)).Append('\n').Append('\n');

        // A chart part that could not be read still gets a part that states why, so it is never a silent absence
        if (chart.Data is not { } data)
        {
            builder.Append("_This chart could not be read: ")
                .Append(chart.ReadFailureReason ?? "no reason was recorded.").Append("_\n");
            builder.Append('\n').Append("Chart part: `").Append(chart.PartUri).Append("`, on worksheet ")
                .Append(Quoted(chart.SheetName)).Append(".\n");
            return builder.ToString();
        }

        AppendContext(builder, chart, data);
        AppendSeriesList(builder, data);

        // Assemble every row index either the categories or any series declares, so a sparse cache stays sparse
        var indices = CollectIndices(data);
        if (indices.Count == 0)
        {
            builder.Append('\n')
                .Append("_This chart declares no cached data points, so no data table could be produced._\n");
            return builder.ToString();
        }

        var truncated = indices.Count > MaxPlottedPoints;
        var rendered = truncated ? MaxPlottedPoints : indices.Count;
        AppendTable(builder, data, indices, rendered);

        // State the truncation in the document itself, where the omitted rows are already in context
        if (truncated)
        {
            builder.Append('\n').Append("_Showing the first ")
                .Append(rendered.ToString(CultureInfo.InvariantCulture)).Append(" of ")
                .Append(indices.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" plotted points; the remainder were omitted to bound the extracted output._\n");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Produces the one-line description of a chart used under its owning worksheet.
    /// </summary>
    /// <param name="chart">The chart to describe. Must not be null.</param>
    /// <returns>The description line, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     A worksheet's own part must say that the sheet shows a chart, because a reader who opens only
    ///     that part would otherwise have no way to know one exists; the chart's data lives in its own
    ///     part so the sheet's cells are not buried under hundreds of plotted points. Pure.
    /// </remarks>
    public static string Describe(ExcelChartModel chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        var name = chart.Data?.Title is { Length: > 0 } title ? Quoted(title) : "an untitled chart";
        return chart.Data is null
            ? $"- Chart {name} (`{chart.PartUri}`) could not be read; its chart part records the reason."
            : $"- Chart {name}{PlotTypeSuffix(chart.Data)}; its cached data is extracted as a separate chart part.";
    }

    /// <summary>
    ///     Produces the heading a chart part carries.
    /// </summary>
    /// <param name="chart">The chart to head.</param>
    /// <returns>The chart's title, or a sheet-qualified fallback when it has none.</returns>
    /// <remarks>A titleless chart still needs a heading a reader can navigate by, so its worksheet names it. Pure.</remarks>
    private static string Heading(ExcelChartModel chart) =>
        chart.Data?.Title is { Length: > 0 } title ? title : $"Chart on {chart.SheetName}";

    /// <summary>
    ///     Appends the chart's context: what kind of plot it is, where it lives, and what its axes measure.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="data">The chart's cached data.</param>
    /// <remarks>
    ///     Axis titles are the difference between a column of numbers and a measured quantity, so they
    ///     are stated before the table rather than left for a reader to infer. A generated title is
    ///     flagged as generated so the output never implies an author wrote it. Side effect: appends to
    ///     <paramref name="builder"/>.
    /// </remarks>
    private static void AppendContext(StringBuilder builder, ExcelChartModel chart, ExcelChartData data)
    {
        if (data.TitleIsAutomatic)
        {
            builder.Append(
                "_The chart carries no authored title; the title above is the one the spreadsheet "
                + "application generates from the series name._\n\n");
        }

        builder.Append("Chart on worksheet ").Append(Quoted(chart.SheetName)).Append(".\n\n");

        if (data.ChartTypes.Count > 0)
        {
            builder.Append("- Plot type: ").Append(string.Join(", ", data.ChartTypes)).Append('\n');
        }

        if (data.CategoryAxisTitle is { Length: > 0 } categoryAxis)
        {
            builder.Append("- Category axis: ").Append(categoryAxis).Append('\n');
        }

        if (data.ValueAxisTitle is { Length: > 0 } valueAxis)
        {
            builder.Append("- Value axis: ").Append(valueAxis).Append('\n');
        }

        if (data.CategoryFormatCode is { Length: > 0 } categoryFormat)
        {
            builder.Append("- Category number format: `").Append(categoryFormat).Append("`\n");
        }
    }

    /// <summary>
    ///     Appends the series inventory: what each series is called, how many points it caches, and where it came from.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="data">The chart's cached data.</param>
    /// <remarks>
    ///     Stated per series rather than per chart because a combination chart's series can differ in
    ///     unit, precision, and length, and a single summary line would hide exactly those differences.
    ///     The source range is included so a reader can find the cells behind a series even though the
    ///     reference alone carries no values. Side effect: appends to <paramref name="builder"/>.
    /// </remarks>
    private static void AppendSeriesList(StringBuilder builder, ExcelChartData data)
    {
        if (data.Series.Count == 0)
        {
            return;
        }

        builder.Append("- Series: ").Append(data.Series.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        for (var index = 0; index < data.Series.Count; index++)
        {
            var series = data.Series[index];
            builder.Append("  - ").Append(SeriesLabel(series, index))
                .Append(": ").Append(series.Points.Count.ToString(CultureInfo.InvariantCulture))
                .Append(series.Points.Count == 1 ? " cached point" : " cached points");

            // A cache that declares more points than it stores is genuinely sparse; say so rather than implying loss
            if (series.DeclaredPointCount > series.Points.Count)
            {
                builder.Append(" of ").Append(series.DeclaredPointCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" declared (the cache omits points whose source cells were empty)");
            }

            if (series.FormatCode is { Length: > 0 } format)
            {
                builder.Append(", number format `").Append(format).Append('`');
            }

            if (series.SourceReference is { Length: > 0 } source)
            {
                builder.Append(", source `").Append(source).Append('`');
            }

            builder.Append('\n');
        }
    }

    /// <summary>
    ///     Appends the data table: one row per plotted point index, one column per series.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="data">The chart's cached data.</param>
    /// <param name="indices">Every point index the chart declares, in ascending order.</param>
    /// <param name="rowLimit">The number of leading rows to render, which truncation may cap.</param>
    /// <remarks>
    ///     The leading point-index column mirrors the row-number column of the worksheet grid table, so
    ///     a chart row stays addressable and a hole in the indices stays visible as the hole it is. A
    ///     series with no value at an index renders as an empty cell rather than a shifted one. Side effect:
    ///     appends to <paramref name="builder"/>.
    /// </remarks>
    private static void AppendTable(
        StringBuilder builder, ExcelChartData data, IReadOnlyList<int> indices, int rowLimit)
    {
        var categories = ToMap(data.Categories);
        var seriesMaps = data.Series.Select(series => ToMap(series.Points)).ToList();

        // Header: the point index, the category label, then one column per series
        builder.Append('\n').Append("| Point | ")
            .Append(Escape(data.CategoryAxisTitle is { Length: > 0 } title ? title : CategoryColumnFallback))
            .Append(" |");
        for (var index = 0; index < data.Series.Count; index++)
        {
            builder.Append(' ').Append(Escape(SeriesLabel(data.Series[index], index))).Append(" |");
        }

        builder.Append('\n').Append("| --- | --- |");
        for (var index = 0; index < data.Series.Count; index++)
        {
            builder.Append(" --- |");
        }

        builder.Append('\n');

        // Body: one row per declared index, up to the bound
        for (var row = 0; row < rowLimit; row++)
        {
            var pointIndex = indices[row];
            builder.Append("| ").Append(pointIndex.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(categories.TryGetValue(pointIndex, out var label) ? Escape(label) : string.Empty)
                .Append(" |");
            foreach (var map in seriesMaps)
            {
                builder.Append(' ')
                    .Append(map.TryGetValue(pointIndex, out var value) ? Escape(value) : string.Empty)
                    .Append(" |");
            }

            builder.Append('\n');
        }
    }

    /// <summary>
    ///     Collects every point index the chart's categories or series declare, in ascending order.
    /// </summary>
    /// <param name="data">The chart's cached data.</param>
    /// <returns>The distinct point indices, ascending.</returns>
    /// <remarks>
    ///     The union rather than the first series' indices, because two series of a combination chart
    ///     can cache different points and taking one series' indices would drop the other's data. Pure.
    /// </remarks>
    private static IReadOnlyList<int> CollectIndices(ExcelChartData data)
    {
        var indices = new SortedSet<int>();
        foreach (var point in data.Categories)
        {
            indices.Add(point.Index);
        }

        foreach (var series in data.Series)
        {
            foreach (var point in series.Points)
            {
                indices.Add(point.Index);
            }
        }

        return [.. indices];
    }

    /// <summary>
    ///     Indexes a point list by its declared point index.
    /// </summary>
    /// <param name="points">The points to index.</param>
    /// <returns>The map from point index to value.</returns>
    /// <remarks>A duplicate index keeps the first value, because a cache that declares one twice is malformed. Pure.</remarks>
    private static Dictionary<int, string> ToMap(IReadOnlyList<ExcelChartPoint> points)
    {
        var map = new Dictionary<int, string>();
        foreach (var point in points)
        {
            map.TryAdd(point.Index, point.Value);
        }

        return map;
    }

    /// <summary>
    ///     Produces the label for a series, falling back to its 1-based position when it is unnamed.
    /// </summary>
    /// <param name="series">The series to label.</param>
    /// <param name="index">The series' 0-based position in the chart.</param>
    /// <returns>The label.</returns>
    /// <remarks>An unnamed series still needs a stable column header a reader can refer to. Pure.</remarks>
    private static string SeriesLabel(ExcelChartSeries series, int index) =>
        series.Name is { Length: > 0 } name
            ? name
            : $"Series {(index + 1).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    ///     Produces the plot-type clause of a chart description, when the chart declares one.
    /// </summary>
    /// <param name="data">The chart's cached data.</param>
    /// <returns>The clause (for example <c> (line)</c>), or an empty string.</returns>
    /// <remarks>Pure.</remarks>
    private static string PlotTypeSuffix(ExcelChartData data) =>
        data.ChartTypes.Count == 0 ? string.Empty : $" ({string.Join(", ", data.ChartTypes)})";

    /// <summary>
    ///     Quotes a name for prose.
    /// </summary>
    /// <param name="value">The name to quote.</param>
    /// <returns>The quoted name.</returns>
    /// <remarks>Single quotes match the worksheet naming used elsewhere in this backend's prose. Pure.</remarks>
    private static string Quoted(string value) => $"'{value}'";

    /// <summary>
    ///     Escapes the characters a markdown table cell cannot carry.
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>The table-safe text.</returns>
    /// <remarks>
    ///     Pipes would end the cell and newlines would end the row; a cached label containing either is
    ///     rare but would silently corrupt every column to its right. Values are never shortened, because
    ///     a cached data point is short by nature and its precision is the point. Pure.
    /// </remarks>
    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal);
}
