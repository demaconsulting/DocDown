using DocDown.Core;

namespace DocDown.PowerPoint.OpenXml;

/// <summary>
///     The backend-neutral model of a whole deck: its slides in presentation order, each with its
///     title, body text, and speaker notes.
/// </summary>
/// <param name="Slides">The slides, in the order the presentation declares them.</param>
/// <param name="Images">
///     The embedded images resolved from across the deck — slides, notes, layouts, and masters —
///     deduplicated by package part, in slide-then-template order. Empty when the deck embeds no
///     images. Written through the sink, which deduplicates again by content.
/// </param>
/// <param name="Metadata">
///     What the deck asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model). The
///     first-slide title used as the document title is a heuristic and is deliberately not part of
///     this authored metadata.
/// </param>
/// <remarks>
///     The reader populates this model from the Open XML package and hands it to the emitter, so
///     every decision about what reaches the output is made once against a model that can be built
///     by hand with no deck behind it. Immutable and thread-safe.
/// </remarks>
internal sealed record PowerPointDeckModel(
    IReadOnlyList<PowerPointSlideModel> Slides,
    IReadOnlyList<EmbeddedImage> Images,
    DocumentMetadata? Metadata = null)
{
    /// <summary>
    ///     Initializes a deck model that embeds no images, for a hand-built model with no deck behind it.
    /// </summary>
    /// <param name="slides">The slides, in presentation order.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests and callers that do not exercise embedded images; images default to empty.</remarks>
    public PowerPointDeckModel(IReadOnlyList<PowerPointSlideModel> slides, DocumentMetadata? metadata = null)
        : this(slides, [], metadata)
    {
    }
}

/// <summary>
///     One slide: its 1-based ordinal, its title where one exists, its body text lines, its
///     speaker notes, and the images it references in reading order.
/// </summary>
/// <param name="Ordinal">The slide's 1-based position, because a deck's argument is sequential.</param>
/// <param name="Title">The slide title from the title placeholder, or <see langword="null"/> when none.</param>
/// <param name="TextLines">
///     The slide's body text as a sequence of lines (one per paragraph across the non-title shapes),
///     in reading order. Empty when the slide carries no body text.
/// </param>
/// <param name="Notes">
///     The speaker notes attached to the slide, or <see langword="null"/> when the slide has none.
///     Notes never appear in any render, so they are extracted from the file itself.
/// </param>
/// <param name="Images">
///     The images this slide references, in reading order and distinct by package part, so the
///     emitter can link each one inline at the point of occurrence — following Word's convention. A
///     part shown on several slides appears in each slide's list, recording every reference. Empty
///     when the slide shows no picture.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointSlideModel(
    int Ordinal, string? Title, IReadOnlyList<string> TextLines, string? Notes,
    IReadOnlyList<PowerPointSlideImageRef> Images)
{
    /// <summary>
    ///     Initializes a slide model that references no images inline, for a hand-built model.
    /// </summary>
    /// <param name="ordinal">The slide's 1-based position.</param>
    /// <param name="title">The slide title, or <see langword="null"/>.</param>
    /// <param name="textLines">The slide's body text lines.</param>
    /// <param name="notes">The speaker notes, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests that do not exercise inline image links; images default to empty.</remarks>
    public PowerPointSlideModel(int ordinal, string? title, IReadOnlyList<string> textLines, string? notes)
        : this(ordinal, title, textLines, notes, [])
    {
    }
}

/// <summary>
///     One image occurrence on a slide: the package-part reference that keys the written-path map,
///     and the alt text to show, if any.
/// </summary>
/// <param name="SourceRef">The image part URI within the package; the key into the sink's written-path map.</param>
/// <param name="AltText">
///     The image's descriptive alt text when a genuinely descriptive source was chosen; otherwise
///     <see langword="null"/> so a neutral placeholder is used rather than implying a description.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointSlideImageRef(string SourceRef, string? AltText);
