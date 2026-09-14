using System.Globalization;
using DocDown.Visio.OpenXml;

namespace DocDown.Visio.Markdown;

/// <summary>
///     Where a rendered endpoint label came from, so a reader can tell an authored name from a
///     classification from an admission of ignorance.
/// </summary>
/// <remarks>
///     The distinction is the whole point: a label that reads like a name, but was actually derived
///     from the shape's type, would imply someone labeled that shape when they did not. Every
///     consumer of <see cref="VisioShapeLabeler"/> must be able to make that distinction, so it is
///     carried in the data rather than inferred from the rendered string.
/// </remarks>
internal enum VisioLabelSource
{
    /// <summary>The label is the shape's own text, exactly as the drawing carries it.</summary>
    Text,

    /// <summary>The label is the name of the master the shape was instantiated from — its type, not its name.</summary>
    Type,

    /// <summary>The drawing names the shape only by its id; nothing else is known about it.</summary>
    Unresolved
}

/// <summary>
///     One endpoint label: what to render, and where it came from.
/// </summary>
/// <param name="Display">The text to render for the endpoint.</param>
/// <param name="Source">Where the label came from.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal readonly record struct VisioShapeLabel(string Display, VisioLabelSource Source);

/// <summary>
///     Decides how a shape is named in the rendered topology, from what the drawing actually says
///     about it — and nothing more.
/// </summary>
/// <remarks>
///     <para>
///         A schematic's topology is unreadable when most endpoints render as <c>(shape 96)</c>, but
///         the cure must not be invention. Two facts are available and both are the document's own:
///         the shape's text, and the name of the master it was instantiated from. A shape drawn from
///         the <c>3-way Plug Valve</c> master *is* a 3-way plug valve by the drawing's own
///         statement, so reporting that is recovery, not fabrication. Where neither fact exists the
///         shape id remains, because a plain fallback beats a plausible guess.
///     </para>
///     <para>
///         Two rules keep the result truthful. First, a type-derived label is rendered
///         parenthesized and paired with the shape id — <c>(3-way Plug Valve, shape 34)</c> — so it
///         can never be mistaken for a name someone authored, and so two shapes of the same type
///         stay distinguishable. Second, masters that describe connective geometry rather than a
///         component are refused as endpoint labels: naming an endpoint <c>Straight Line</c> would
///         assert that a line is the thing being connected to, which is worse than admitting the
///         endpoint is unidentified.
///     </para>
///     <para>Stateless, pure, and thread-safe.</para>
/// </remarks>
internal static class VisioShapeLabeler
{
    /// <summary>
    ///     The master names that describe a drawing primitive — a bare line — rather than a component.
    /// </summary>
    /// <remarks>
    ///     These are Visio's own line-drawing masters. A shape instantiated from one of them is a
    ///     piece of geometry the author drew, so its master name classifies the stroke, not the
    ///     equipment at the end of a connector. Compared case-insensitively. Names containing
    ///     "connector" are caught by <see cref="IsConnectiveMaster"/> instead, which covers the
    ///     stencil variants (<c>Dynamic connector</c>, <c>Line-curve connector</c>) without
    ///     enumerating them.
    /// </remarks>
    private static readonly HashSet<string> LineMasters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Straight Line",
        "Curved Line",
        "Freeform Line"
    };

    /// <summary>
    ///     The word that marks a master as describing the connector itself.
    /// </summary>
    /// <remarks>Matched as a substring so stencil variants are covered without enumerating them.</remarks>
    private const string ConnectorWord = "connector";

    /// <summary>
    ///     The one-line statement of the labeling convention, so a reader of the output — human or
    ///     machine — can decode a parenthesized label without reverse-engineering it.
    /// </summary>
    /// <remarks>
    ///     Published verbatim into markdown content whenever a page uses a type-derived or id-fallback
    ///     endpoint label, so the rendered edge list explains itself in place.
    /// </remarks>
    internal const string Convention =
        "Topology endpoint labels: an unparenthesized label is the shape's own text; "
        + "'(Type, shape N)' is the name of the master the shape was instantiated from — its type, "
        + "not a name anyone gave it; '(shape N)' means the drawing identifies the shape only by id. "
        + "Masters describing connective geometry are not used as endpoint labels.";

    /// <summary>
    ///     Produces the label for a connector endpoint.
    /// </summary>
    /// <param name="id">The endpoint shape id. Must not be null.</param>
    /// <param name="shapes">The page's shape-id-to-shape map. Must not be null.</param>
    /// <returns>
    ///     The shape's own text when it carries any; otherwise its master name, parenthesized with
    ///     the shape id, when the master describes a component; otherwise the bare shape-id label.
    ///     Never empty.
    /// </returns>
    /// <remarks>
    ///     The precedence is strict: authored text always outranks type, and type always outranks
    ///     the id fallback. An endpoint naming a shape absent from the map — which the reader's
    ///     topology resolution already excludes — falls back to the id label rather than throwing.
    ///     Pure.
    /// </remarks>
    public static VisioShapeLabel Label(string id, IReadOnlyDictionary<string, VisioShapeModel> shapes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(shapes);

        var fallback = new VisioShapeLabel(
            string.Create(CultureInfo.InvariantCulture, $"(shape {id})"), VisioLabelSource.Unresolved);

        if (!shapes.TryGetValue(id, out var shape))
        {
            return fallback;
        }

        // The shape's own text is the only label anyone actually authored for it, so it always wins
        if (shape.Text is { } text)
        {
            return new VisioShapeLabel(text, VisioLabelSource.Text);
        }

        // Failing that, the master name classifies the shape — but only where the master describes a
        // component; a connective master describes the line, not the thing at the end of it
        if (shape.MasterName is { } master && !IsConnectiveMaster(master))
        {
            return new VisioShapeLabel(
                string.Create(CultureInfo.InvariantCulture, $"({master}, shape {id})"), VisioLabelSource.Type);
        }

        return fallback;
    }

    /// <summary>
    ///     Decides whether a master name describes connective geometry rather than a component.
    /// </summary>
    /// <param name="name">The master name to classify. Must not be null.</param>
    /// <returns><see langword="true"/> when the name describes a connector or a bare line.</returns>
    /// <remarks>
    ///     A connector's own master — <c>Dynamic connector</c>, <c>Line-curve connector</c> — and the
    ///     line-drawing masters describe the stroke between two things, so using one as an endpoint
    ///     label would actively mislead: it would report the wire as the equipment. Such an endpoint
    ///     keeps the id fallback. Pure.
    /// </remarks>
    private static bool IsConnectiveMaster(string name) =>
        name.Contains(ConnectorWord, StringComparison.OrdinalIgnoreCase) || LineMasters.Contains(name);
}
