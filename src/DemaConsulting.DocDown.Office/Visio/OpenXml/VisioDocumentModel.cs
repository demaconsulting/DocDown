using DocDown.Core;

namespace DocDown.Visio.OpenXml;

/// <summary>
///     The backend-neutral model of a whole Visio drawing: its pages in document order, each with
///     its named identity, the shapes that carry text, and the directed connections between them.
/// </summary>
/// <param name="Pages">The pages, in the order the drawing declares them.</param>
/// <param name="Images">
///     The embedded images resolved from the drawing's page and master image relationships,
///     deduplicated by package part, excluding the package thumbnail. Empty when the drawing embeds
///     no content images. Written through the sink, which deduplicates again by content.
/// </param>
/// <param name="Metadata">
///     What the drawing asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model).
/// </param>
/// <remarks>
///     The reader populates this model from the Open Packaging container and hands it to the emitter,
///     so every decision about what reaches the output is made once against a model that can be built
///     by hand with no drawing behind it. Immutable and thread-safe.
/// </remarks>
internal sealed record VisioDocumentModel(
    IReadOnlyList<VisioPageModel> Pages,
    IReadOnlyList<EmbeddedImage> Images,
    DocumentMetadata? Metadata = null)
{
    /// <summary>
    ///     Initializes a drawing model that embeds no images, for a hand-built model with no drawing behind it.
    /// </summary>
    /// <param name="pages">The pages, in document order.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests and callers that do not exercise embedded images; images default to empty.</remarks>
    public VisioDocumentModel(IReadOnlyList<VisioPageModel> pages, DocumentMetadata? metadata = null)
        : this(pages, [], metadata)
    {
    }
}

/// <summary>
///     One page: its name, its shapes, the directed connections between them, and the images it
///     references.
/// </summary>
/// <param name="Name">The page name, because engineers navigate and cite drawings by page name.</param>
/// <param name="Shapes">The page's shapes, in document order.</param>
/// <param name="Connections">The directed connections resolved from the page's connector records.</param>
/// <param name="Images">
///     The images this page references, in relationship order and distinct by package part, so the
///     emitter can link each one inline under the page section — following Word's convention. A media
///     part shared across pages appears in each page's list, recording every reference. Empty when the
///     page shows no embedded image.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioPageModel(
    string Name, IReadOnlyList<VisioShapeModel> Shapes, IReadOnlyList<VisioConnectionModel> Connections,
    IReadOnlyList<VisioPageImageRef> Images)
{
    /// <summary>
    ///     Initializes a page model that references no images inline, for a hand-built model.
    /// </summary>
    /// <param name="name">The page name.</param>
    /// <param name="shapes">The page's shapes.</param>
    /// <param name="connections">The directed connections.</param>
    /// <remarks>A convenience for tests that do not exercise inline image links; images default to empty.</remarks>
    public VisioPageModel(
        string name, IReadOnlyList<VisioShapeModel> shapes, IReadOnlyList<VisioConnectionModel> connections)
        : this(name, shapes, connections, [])
    {
    }
}

/// <summary>
///     One image occurrence on a page: the package-part reference that keys the written-path map, and
///     the alt text to show, if any.
/// </summary>
/// <param name="SourceRef">The media part URI within the package; the key into the sink's written-path map.</param>
/// <param name="AltText">
///     The image's descriptive alt text when one was available; otherwise <see langword="null"/> so a
///     neutral placeholder is used rather than implying a description a foreign-data image never gave.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioPageImageRef(string SourceRef, string? AltText);

/// <summary>
///     One shape: its id, the text it carries, if any, and the name of the master it was
///     instantiated from, if any.
/// </summary>
/// <param name="Id">The shape's page-local id, used to resolve connector endpoints back to a shape.</param>
/// <param name="Text">The shape's text, or <see langword="null"/> when the shape carries none.</param>
/// <param name="MasterName">
///     The name of the master (stencil shape) this shape was instantiated from, or
///     <see langword="null"/> when the shape names no master or the drawing does not declare it.
///     Carried verbatim from the drawing; never synthesized.
/// </param>
/// <remarks>
///     <para>
///         <see cref="MasterName"/> exists because a schematic's text-less shapes are still
///         classified by whoever drew them: a shape instantiated from the <c>Positive
///         displacement</c> master *is* a positive-displacement pump, by the drawing's own
///         statement. That is recoverable fact, not inference, so it is carried in the model and the
///         consumer decides how — and whether — to show it. Defaulted so a model built by hand for a
///         test that does not care about masters stays terse.
///     </para>
///     <para>Immutable and thread-safe.</para>
/// </remarks>
internal sealed record VisioShapeModel(string Id, string? Text, string? MasterName = null);

/// <summary>
///     One directed connection between two shapes, resolved from a connector's begin and end records.
/// </summary>
/// <param name="FromId">The id of the shape at the connector's begin end (the source).</param>
/// <param name="ToId">The id of the shape at the connector's end end (the target).</param>
/// <remarks>
///     The direction is carried by which cell (<c>BeginX</c> or <c>EndX</c>) each connector record
///     names, so a connector that reads "Inlet Tank at its begin, Transfer Pump at its end" becomes the
///     directed edge <c>Inlet Tank → Transfer Pump</c>. Immutable and thread-safe.
/// </remarks>
internal sealed record VisioConnectionModel(string FromId, string ToId);
