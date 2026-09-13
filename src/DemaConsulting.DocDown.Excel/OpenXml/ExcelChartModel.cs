namespace DocDown.Excel.OpenXml;

/// <summary>
///     One chart a worksheet shows: where it came from, and either the cached data read from its
///     chart part or the reason that data could not be read.
/// </summary>
/// <param name="PartUri">
///     The chart part URI within the package (for example <c>/xl/charts/chart1.xml</c>); the citable
///     identity of a chart, used to name the chart in a gap when it carries no readable data.
/// </param>
/// <param name="SheetName">The name of the worksheet that shows the chart, so a chart keeps its sheet context.</param>
/// <param name="Data">
///     The chart's cached data and labeling, or <see langword="null"/> when the chart part could not
///     be parsed — in which case <paramref name="ReadFailureReason"/> states why.
/// </param>
/// <param name="ReadFailureReason">
///     Why the chart part could not be read, or <see langword="null"/> when it was read. Exactly one
///     of this and <paramref name="Data"/> is non-null, so a chart is never both readable and
///     unreadable and never silently neither.
/// </param>
/// <remarks>
///     A chart is carried through the model rather than resolved at emission time so the decision
///     about what reaches the output — a data table, or an honest gap naming the part — is made once
///     against a model a test can build by hand. Immutable and thread-safe.
/// </remarks>
internal sealed record ExcelChartModel(
    string PartUri, string SheetName, ExcelChartData? Data, string? ReadFailureReason);

/// <summary>
///     The cached content of one chart part: what the chart is called, what kind of plot it is, what
///     its axes measure, and the values as last plotted.
/// </summary>
/// <param name="Title">
///     The chart's displayed title, or <see langword="null"/> when the chart shows none. When
///     <paramref name="TitleIsAutomatic"/> is set this is the title the host application generates
///     from the sole series name rather than an authored one.
/// </param>
/// <param name="TitleIsAutomatic">
///     <see langword="true"/> when the title was derived from the single series name because the
///     chart carries an automatic title, so the output can say the title was generated rather than
///     implying an author wrote it.
/// </param>
/// <param name="ChartTypes">
///     The plot types the chart's plot area declares (for example <c>line</c>), in document order and
///     distinct. A combination chart declares more than one. Empty when the plot area declares none.
/// </param>
/// <param name="CategoryAxisTitle">
///     The category (horizontal) axis title, or <see langword="null"/> when the axis carries none. An
///     axis title is what turns a column of numbers into a measured quantity, so it is captured
///     alongside the values.
/// </param>
/// <param name="ValueAxisTitle">The value (vertical) axis title, or <see langword="null"/> when the axis carries none.</param>
/// <param name="Categories">
///     The category labels as last plotted, by point index; empty when the chart caches none, in
///     which case the point index alone identifies a row.
/// </param>
/// <param name="CategoryFormatCode">
///     The number format the category cache declares (for example <c>General</c>), or
///     <see langword="null"/> when the categories are text or declare no format.
/// </param>
/// <param name="Series">The chart's data series in plot order. Empty when the chart plots none.</param>
/// <remarks>
///     Only cached values are carried: a chart part stores the values as last plotted alongside the
///     formula that produced them, and a consumer that cannot recalculate the workbook can use only
///     the former. The formula reference is kept per series so a reader can still find the source
///     range. Immutable and thread-safe.
/// </remarks>
internal sealed record ExcelChartData(
    string? Title,
    bool TitleIsAutomatic,
    IReadOnlyList<string> ChartTypes,
    string? CategoryAxisTitle,
    string? ValueAxisTitle,
    IReadOnlyList<ExcelChartPoint> Categories,
    string? CategoryFormatCode,
    IReadOnlyList<ExcelChartSeries> Series);

/// <summary>
///     One data series of a chart: its name, where its values came from, and the values themselves as
///     last plotted.
/// </summary>
/// <param name="Name">
///     The series name from its cached name reference, or <see langword="null"/> when the series is
///     unnamed — in which case the emitter labels it by its 1-based position.
/// </param>
/// <param name="SourceReference">
///     The formula reference the series plots (for example <c>Sheet1!$B$2:$B$9</c>), or
///     <see langword="null"/> when the series declares none. Carried so a reader can locate the
///     source range even though the reference alone is useless to a consumer that cannot recalculate.
/// </param>
/// <param name="FormatCode">
///     The number format the value cache declares, or <see langword="null"/> when it declares none.
///     Kept because a format code is often the only statement of a value's unit or precision.
/// </param>
/// <param name="DeclaredPointCount">
///     The point count the cache declares, which may exceed <paramref name="Points"/> when the cache
///     is sparse. Zero when the cache declares none.
/// </param>
/// <param name="Points">
///     The cached values by point index, in ascending index order. Indices may be sparse — a cache
///     omits a point whose source cell was empty — so a point carries its own index rather than
///     relying on position. Empty when the series caches no values.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelChartSeries(
    string? Name, string? SourceReference, string? FormatCode, int DeclaredPointCount,
    IReadOnlyList<ExcelChartPoint> Points);

/// <summary>
///     One cached point: the index it occupies in its series and its value verbatim.
/// </summary>
/// <param name="Index">The 0-based point index the cache declares, preserved so a sparse cache stays honestly sparse.</param>
/// <param name="Value">The cached value exactly as stored, at full precision, never reformatted or rounded.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelChartPoint(int Index, string Value);
