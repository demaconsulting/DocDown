using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocDown.Excel.OpenXml;

/// <summary>
///     Reads the charts a worksheet shows into the model, recovering each chart's cached data series
///     — the numbers as last plotted — rather than the formulas that produced them.
/// </summary>
/// <remarks>
///     <para>
///         A chart part stores two things about every series: the formula reference naming the source
///         range, and the cached values as last plotted. A consumer that cannot recalculate the
///         workbook can use only the latter, so the cache is what this unit reads; the reference is
///         kept alongside purely so a reader can find the source range. Recovering the cache is what
///         makes a chart usable as data — a hundred cached points describe a curve that no
///         rendered picture of the same chart would yield.
///     </para>
///     <para>
///         Chart XML is read with <see cref="XDocument"/> rather than the typed Open XML chart classes
///         because every plot type (line, bar, scatter, pie, and the rest) spells its series the same
///         way — <c>c:ser</c> with <c>c:cat</c>/<c>c:val</c> caches — and a name-driven walk therefore
///         covers plot types this product has never seen, where a typed walk would silently cover only
///         the types it was written against. A chart part that cannot be parsed is returned as a
///         failure reason rather than thrown, so the caller can report it as a gap and keep going.
///     </para>
///     <para>Read-only over the package. Stateless and thread-safe.</para>
/// </remarks>
internal static class ExcelChartReader
{
    /// <summary>The DrawingML chart namespace every chart part element belongs to.</summary>
    private static readonly XNamespace ChartNamespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>The DrawingML main namespace carrying the text runs of a title.</summary>
    private static readonly XNamespace DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>The Open Packaging relationships namespace carrying the <c>r:id</c> attribute.</summary>
    private static readonly XNamespace RelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The reason recorded for a chart drawn with the newer extended chart grammar.</summary>
    /// <remarks>
    ///     Named so the reader and its test cannot drift. An extended chart (waterfall, funnel, tree
    ///     map, box-and-whisker, and the rest) caches its data under an entirely different grammar this
    ///     reader does not implement; saying so plainly is the honest outcome, and far
    ///     better than dropping the chart as this product previously dropped every chart.
    /// </remarks>
    internal const string ExtendedChartReason =
        "The chart uses the extended chart format (waterfall, funnel, tree map, and similar), whose "
        + "cached data this backend does not yet read.";

    /// <summary>
    ///     Collects the charts a worksheet shows, in drawing order, each with its cached data or the
    ///     reason that data could not be read.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part whose drawing is walked, or <see langword="null"/> when the sheet has no part.</param>
    /// <param name="sheetName">The name of the worksheet the charts belong to, carried into each chart.</param>
    /// <returns>The worksheet's charts in drawing order; empty when the worksheet shows none.</returns>
    /// <remarks>
    ///     Charts are ordered by the graphic frames in the worksheet's drawing rather than by
    ///     relationship order, so a workbook with several charts on one sheet reports them in the order
    ///     a reader sees them. A chart part that no frame references is still collected afterwards, so
    ///     an unreferenced chart is reported rather than lost. Read-only I/O over the package.
    /// </remarks>
    public static IReadOnlyList<ExcelChartModel> Collect(WorksheetPart? worksheetPart, string sheetName)
    {
        var drawingsPart = worksheetPart?.DrawingsPart;
        if (drawingsPart is null)
        {
            return [];
        }

        // Walk the drawing's graphic frames first so charts come out in the order they are laid out
        var charts = new List<ExcelChartModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relationshipId in FramedChartRelationshipIds(drawingsPart))
        {
            var part = TryGetPart(drawingsPart, relationshipId);
            if (part is not null && seen.Add(part.Uri.ToString()))
            {
                charts.Add(BuildChart(part, sheetName));
            }
        }

        // Collect any chart part no frame referenced, so an orphaned chart is reported rather than dropped
        foreach (var part in ChartPartsOf(drawingsPart).Where(part => seen.Add(part.Uri.ToString())))
        {
            charts.Add(BuildChart(part, sheetName));
        }

        return charts;
    }

    /// <summary>
    ///     Reads a chart part's XML into the cached-data model.
    /// </summary>
    /// <param name="stream">A readable stream over the chart part XML. Must not be null.</param>
    /// <returns>The chart's title, plot types, axis titles, categories, and series.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="XmlException">Thrown when the part is not well-formed XML; the caller converts this into a reported gap.</exception>
    /// <remarks>
    ///     Exposed separately from <see cref="Collect"/> so the parsing rules can be exercised against
    ///     hand-written chart XML with no package around it. Read-only over the stream.
    /// </remarks>
    public static ExcelChartData ReadChartData(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var document = XDocument.Load(stream);
        var chart = document.Root?.Element(ChartNamespace + "chart");
        var plotArea = chart?.Element(ChartNamespace + "plotArea");

        // The plot area names the plot types; a combination chart declares more than one
        var chartTypes = plotArea is null
            ? []
            : plotArea.Elements()
                .Where(element => element.Name.Namespace == ChartNamespace
                    && element.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal))
                .Select(element => element.Name.LocalName[..^"Chart".Length])
                .Distinct(StringComparer.Ordinal)
                .ToList();

        var series = ReadSeries(plotArea);
        var categories = ReadCategories(plotArea);

        return new ExcelChartData(
            ResolveTitle(chart, series, out var titleIsAutomatic),
            titleIsAutomatic,
            chartTypes,
            AxisTitle(plotArea, "catAx") ?? AxisTitle(plotArea, "dateAx"),
            AxisTitle(plotArea, "valAx"),
            categories.Points,
            categories.FormatCode,
            series);
    }

    /// <summary>
    ///     Resolves the title a chart displays, distinguishing an authored title from a generated one.
    /// </summary>
    /// <param name="chart">The <c>c:chart</c> element, or <see langword="null"/> when absent.</param>
    /// <param name="series">The chart's series, consulted for the automatic single-series title.</param>
    /// <param name="isAutomatic">Set to <see langword="true"/> when the returned title was derived from the sole series name.</param>
    /// <returns>The displayed title, or <see langword="null"/> when the chart shows none.</returns>
    /// <remarks>
    ///     A chart whose title was never edited carries an empty <c>c:title</c> element and the host
    ///     application draws the sole series name in its place — which is exactly what a reader sees on
    ///     the chart, so reporting nothing would understate what the document shows. The derived case
    ///     is flagged so the output can say the title was generated rather than authored. A deleted
    ///     title (<c>autoTitleDeleted</c>) yields no title at all. Pure.
    /// </remarks>
    private static string? ResolveTitle(XElement? chart, IReadOnlyList<ExcelChartSeries> series, out bool isAutomatic)
    {
        isAutomatic = false;
        if (chart is null)
        {
            return null;
        }

        var titleElement = chart.Element(ChartNamespace + "title");
        if (titleElement is null)
        {
            return null;
        }

        // An authored title carries its own text, either as rich text runs or a cached string reference
        var authored = TextOf(titleElement.Element(ChartNamespace + "tx"));
        if (authored is not null)
        {
            return authored;
        }

        // Otherwise the application draws the single series name as the title, unless the title is deleted
        var deleted = chart.Element(ChartNamespace + "autoTitleDeleted")?.Attribute("val")?.Value == "1";
        if (deleted || series.Count != 1 || series[0].Name is not { Length: > 0 } seriesName)
        {
            return null;
        }

        isAutomatic = true;
        return seriesName;
    }

    /// <summary>
    ///     Reads an axis title from the first axis of the given kind in the plot area.
    /// </summary>
    /// <param name="plotArea">The <c>c:plotArea</c> element, or <see langword="null"/> when absent.</param>
    /// <param name="axisName">The axis element's local name (<c>catAx</c>, <c>dateAx</c>, or <c>valAx</c>).</param>
    /// <returns>The axis title text, or <see langword="null"/> when the axis or its title is absent.</returns>
    /// <remarks>
    ///     An axis title states what the numbers measure and in what unit, which is what turns a column
    ///     of values into data a model can reason about; a chart without one still extracts, just less
    ///     usefully. Pure.
    /// </remarks>
    private static string? AxisTitle(XElement? plotArea, string axisName)
    {
        var axis = plotArea?.Element(ChartNamespace + axisName);
        return TextOf(axis?.Element(ChartNamespace + "title")?.Element(ChartNamespace + "tx"));
    }

    /// <summary>
    ///     Reads the text a chart text container holds, from rich text runs or a cached string reference.
    /// </summary>
    /// <param name="textContainer">A <c>c:tx</c> element, or <see langword="null"/>.</param>
    /// <returns>The text, or <see langword="null"/> when the container holds none.</returns>
    /// <remarks>
    ///     Paragraph breaks become spaces so a two-line title reads as one line in a table cell or
    ///     heading; runs within a paragraph concatenate so a title split across formatting runs is not
    ///     torn apart. Pure.
    /// </remarks>
    private static string? TextOf(XElement? textContainer)
    {
        if (textContainer is null)
        {
            return null;
        }

        // A rich-text title is paragraphs of runs; join paragraphs with a space so it stays one line
        var rich = textContainer.Element(ChartNamespace + "rich");
        if (rich is not null)
        {
            var builder = new StringBuilder();
            foreach (var paragraph in rich.Elements(DrawingNamespace + "p"))
            {
                var text = string.Concat(paragraph.Descendants(DrawingNamespace + "t").Select(run => run.Value));
                if (text.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(text);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        // A referenced title caches the text it resolved to; a literal title carries it directly
        var cached = textContainer.Descendants(ChartNamespace + "v").FirstOrDefault()?.Value;
        return string.IsNullOrEmpty(cached) ? null : cached;
    }

    /// <summary>
    ///     Reads every series the plot area declares, in document order.
    /// </summary>
    /// <param name="plotArea">The <c>c:plotArea</c> element, or <see langword="null"/> when absent.</param>
    /// <returns>The series with their names, source references, and cached values.</returns>
    /// <remarks>
    ///     Series are taken from every plot type in the plot area, so a combination chart yields all of
    ///     its series rather than only the first plot's. A scatter or bubble series spells its values
    ///     <c>c:yVal</c> instead of <c>c:val</c>, so both are accepted. Pure.
    /// </remarks>
    private static IReadOnlyList<ExcelChartSeries> ReadSeries(XElement? plotArea)
    {
        if (plotArea is null)
        {
            return [];
        }

        var series = new List<ExcelChartSeries>();
        foreach (var element in plotArea.Descendants(ChartNamespace + "ser"))
        {
            var values = ReadCache(
                element.Element(ChartNamespace + "val") ?? element.Element(ChartNamespace + "yVal"));
            series.Add(new ExcelChartSeries(
                TextOf(element.Element(ChartNamespace + "tx")),
                values.SourceReference,
                values.FormatCode,
                values.DeclaredCount,
                values.Points));
        }

        return series;
    }

    /// <summary>
    ///     Reads the category labels the chart plots against, from the first series that caches any.
    /// </summary>
    /// <param name="plotArea">The <c>c:plotArea</c> element, or <see langword="null"/> when absent.</param>
    /// <returns>The category cache contents; empty when no series caches categories.</returns>
    /// <remarks>
    ///     Every series of a chart is normally plotted against the same categories, and the file repeats
    ///     that cache in each series; taking the first non-empty one keeps a single category column in
    ///     the rendered table. A chart with no cached categories still renders, keyed by point index
    ///     alone. Pure.
    /// </remarks>
    private static CacheContents ReadCategories(XElement? plotArea)
    {
        if (plotArea is null)
        {
            return CacheContents.Empty;
        }

        foreach (var element in plotArea.Descendants(ChartNamespace + "ser"))
        {
            var categories = ReadCache(
                element.Element(ChartNamespace + "cat") ?? element.Element(ChartNamespace + "xVal"));
            if (categories.Points.Count > 0)
            {
                return categories;
            }
        }

        return CacheContents.Empty;
    }

    /// <summary>
    ///     Reads the cached points, format code, and source reference from a series' value or category holder.
    /// </summary>
    /// <param name="holder">A <c>c:val</c>, <c>c:yVal</c>, <c>c:cat</c>, or <c>c:xVal</c> element, or <see langword="null"/>.</param>
    /// <returns>The cache contents; empty when the holder is absent or caches nothing.</returns>
    /// <remarks>
    ///     Four shapes occur: a numeric or string reference carrying a formula and a cache, and a
    ///     numeric or string literal carrying the values directly with no source range. All four are
    ///     read, because data typed straight into the chart exists only in the literal form and would
    ///     otherwise be lost. A reference whose cache is absent yields no points but still yields its
    ///     formula, so the caller can report a chart whose data was never cached and say where it came
    ///     from. Pure.
    /// </remarks>
    private static CacheContents ReadCache(XElement? holder)
    {
        if (holder is null)
        {
            return CacheContents.Empty;
        }

        // A referenced series carries the source formula beside the cache; a literal series carries neither
        var reference = holder.Element(ChartNamespace + "numRef") ?? holder.Element(ChartNamespace + "strRef");
        var cache = reference is not null
            ? reference.Element(ChartNamespace + "numCache") ?? reference.Element(ChartNamespace + "strCache")
            : holder.Element(ChartNamespace + "numLit") ?? holder.Element(ChartNamespace + "strLit");
        var sourceReference = reference?.Element(ChartNamespace + "f")?.Value;

        if (cache is null)
        {
            return new CacheContents(
                string.IsNullOrEmpty(sourceReference) ? null : sourceReference, null, 0, []);
        }

        // Points carry their own index because a cache omits a point whose source cell was empty
        var points = new List<ExcelChartPoint>();
        foreach (var point in cache.Elements(ChartNamespace + "pt"))
        {
            var value = point.Element(ChartNamespace + "v")?.Value;
            if (value is null)
            {
                continue;
            }

            var index = int.TryParse(
                point.Attribute("idx")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : points.Count;
            points.Add(new ExcelChartPoint(index, value));
        }

        points.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        var declared = int.TryParse(
            cache.Element(ChartNamespace + "ptCount")?.Attribute("val")?.Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : points.Count;

        return new CacheContents(
            string.IsNullOrEmpty(sourceReference) ? null : sourceReference,
            cache.Element(ChartNamespace + "formatCode")?.Value,
            declared,
            points);
    }

    /// <summary>
    ///     Reads the relationship ids of the charts the worksheet's drawing frames reference, in layout order.
    /// </summary>
    /// <param name="drawingsPart">The worksheet's drawing part.</param>
    /// <returns>The relationship ids of every framed chart, in document order.</returns>
    /// <remarks>
    ///     A chart is anchored through a graphic frame whose graphic data holds a <c>chart</c> element
    ///     carrying the relationship id; both the classic and extended chart grammars spell that element
    ///     the same way, so the local name alone is matched and the part type resolved afterwards. Pure
    ///     over the already-loaded drawing.
    /// </remarks>
    private static IEnumerable<string> FramedChartRelationshipIds(DrawingsPart drawingsPart)
    {
        var drawing = drawingsPart.WorksheetDrawing;
        if (drawing is null)
        {
            yield break;
        }

        foreach (var frame in drawing.Descendants<Xdr.GraphicFrame>())
        {
            foreach (var element in frame.Descendants<DocumentFormat.OpenXml.OpenXmlElement>())
            {
                if (!string.Equals(element.LocalName, "chart", StringComparison.Ordinal))
                {
                    continue;
                }

                var id = element.GetAttributes()
                    .FirstOrDefault(attribute =>
                        string.Equals(attribute.LocalName, "id", StringComparison.Ordinal)
                        && string.Equals(
                            attribute.NamespaceUri, RelationshipNamespace.NamespaceName, StringComparison.Ordinal))
                    .Value;
                if (!string.IsNullOrEmpty(id))
                {
                    yield return id;
                }
            }
        }
    }

    /// <summary>
    ///     Resolves a relationship id to a chart part of either grammar.
    /// </summary>
    /// <param name="drawingsPart">The drawing part the relationship is scoped to.</param>
    /// <param name="relationshipId">The relationship id read from a graphic frame.</param>
    /// <returns>The referenced part when it is a chart part; otherwise <see langword="null"/>.</returns>
    /// <remarks>
    ///     A graphic frame can hold things that are not charts at all — an embedded table or a SmartArt
    ///     diagram — so a non-chart target is simply not a chart here. Read-only.
    /// </remarks>
    private static OpenXmlPart? TryGetPart(DrawingsPart drawingsPart, string relationshipId)
    {
        try
        {
            return drawingsPart.GetPartById(relationshipId) switch
            {
                ChartPart chartPart => chartPart,
                ExtendedChartPart extendedPart => extendedPart,
                _ => null
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            // A frame naming a relationship the package does not declare is malformed; it holds no chart
            return null;
        }
    }

    /// <summary>
    ///     Enumerates every chart part a drawing relates to, of either grammar.
    /// </summary>
    /// <param name="drawingsPart">The drawing part whose related parts are listed.</param>
    /// <returns>The classic and extended chart parts, in relationship order.</returns>
    /// <remarks>Used as the sweep that catches a chart part no graphic frame referenced. Read-only.</remarks>
    private static IEnumerable<OpenXmlPart> ChartPartsOf(DrawingsPart drawingsPart) =>
        drawingsPart.GetPartsOfType<ChartPart>().Cast<OpenXmlPart>()
            .Concat(drawingsPart.GetPartsOfType<ExtendedChartPart>());

    /// <summary>
    ///     Builds the model for one chart part, converting any parse failure into a stated reason.
    /// </summary>
    /// <param name="part">The chart part to read.</param>
    /// <param name="sheetName">The worksheet that shows the chart.</param>
    /// <returns>The chart model carrying either its cached data or the reason it could not be read.</returns>
    /// <remarks>
    ///     A malformed or unsupported chart must not abort the extraction of an otherwise readable
    ///     workbook, and must not disappear either; converting the failure into a reason lets the
    ///     emitter report it as a counted gap. Read-only I/O over the part.
    /// </remarks>
    private static ExcelChartModel BuildChart(OpenXmlPart part, string sheetName)
    {
        var uri = part.Uri.ToString();

        // The extended chart grammar caches its data differently; say so rather than reading nothing silently
        if (part is ExtendedChartPart)
        {
            return new ExcelChartModel(uri, sheetName, null, ExtendedChartReason);
        }

        try
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            return new ExcelChartModel(uri, sheetName, ReadChartData(stream), null);
        }
        catch (XmlException exception)
        {
            return new ExcelChartModel(
                uri, sheetName, null, $"The chart part is not well-formed XML: {exception.Message}");
        }
        catch (IOException exception)
        {
            return new ExcelChartModel(
                uri, sheetName, null, $"The chart part could not be read from the package: {exception.Message}");
        }
    }

    /// <summary>
    ///     The contents of one cached data range: its source formula, format code, declared size, and points.
    /// </summary>
    /// <param name="SourceReference">The formula naming the source range, or <see langword="null"/> when the data is literal.</param>
    /// <param name="FormatCode">The declared number format, or <see langword="null"/> when none.</param>
    /// <param name="DeclaredCount">The point count the cache declares, which a sparse cache exceeds its point list with.</param>
    /// <param name="Points">The cached points in ascending index order.</param>
    /// <remarks>Internal scratch shape shared by the value and category readers. Immutable and thread-safe.</remarks>
    private sealed record CacheContents(
        string? SourceReference, string? FormatCode, int DeclaredCount, IReadOnlyList<ExcelChartPoint> Points)
    {
        /// <summary>Gets the empty cache, used when a holder is absent or caches nothing.</summary>
        /// <remarks>Shared instance because it is immutable; avoids allocating an empty record per miss.</remarks>
        public static CacheContents Empty { get; } = new(null, null, 0, []);
    }
}
