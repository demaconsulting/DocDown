namespace DocDown.Core;

/// <summary>
///     A hint an extractor supplies when adding an image, guiding naming and provenance without
///     dictating the final path.
/// </summary>
/// <param name="PreferredName">
///     A preferred base name for the image (used to derive the file slug), or
///     <see langword="null"/> to let Core name it from its ordinal alone.
/// </param>
/// <param name="MediaType">The media type of the image bytes (for example <c>image/png</c>).</param>
/// <param name="WidthPx">The image width in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="HeightPx">The image height in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="SourcePage">
///     The 1-based source page the image came from, or <see langword="null"/> when not applicable.
///     A convenience alias for the first (lowest) entry of <paramref name="SourcePages"/>; kept so a
///     consumer that reads only the scalar still sees the deterministic first referrer.
/// </param>
/// <param name="SourceRef">
///     A backend-specific reference identifying the image within the document (for example an
///     XObject name), or <see langword="null"/> when none.
/// </param>
/// <param name="SourcePages">
///     Every 1-based page, slide, or worksheet that references the image, sorted and distinct, or
///     <see langword="null"/> when none are derivable at page level. This is the authoritative
///     multi-referrer record; a part shown on several slides lists them all here.
/// </param>
/// <param name="ReferencedByTemplate">
///     <see langword="true"/> when the image is referenced through a template container — a
///     PowerPoint slide layout or master, or a Visio master — rather than (or in addition to) a
///     specific page. Lets a reader tell a template-borne asset apart from a true orphan.
/// </param>
/// <param name="Transform">
///     How the extractor produced the bytes it is adding, or <see langword="null"/> to accept the
///     default of <see cref="ImageTransform.Passthrough"/>.
/// </param>
/// <param name="Description">
///     A human-meaningful description of the image (for example authored alt text or a caption), or
///     <see langword="null"/> when the document offered none. Recorded in the manifest as image
///     metadata; never guessed.
/// </param>
/// <param name="DescriptionSource">
///     Where <paramref name="Description"/> came from, as a camelCase provenance string (for example
///     <c>description</c>, <c>caption</c>, or <c>heading</c>), or <see langword="null"/> when there
///     is no description.
/// </param>
/// <remarks>
///     The hint is advisory in every respect that concerns naming: Core still allocates the
///     ordinal, slug, extension, and final relative path so naming stays consistent and
///     collision-free regardless of what an extractor suggests. Provenance, by contrast, is
///     recorded from the hint and defaulted when absent — never guessed — because only the
///     extractor knows whether it passed bytes through or re-encoded them. The dimensions, source
///     references, and description are likewise recorded in the manifest for provenance. A
///     description and its source travel together: both are present or both are absent. The page
///     associations (<see cref="SourcePage"/>, <see cref="SourcePages"/>, and
///     <see cref="ReferencedByTemplate"/>) are likewise recorded from the hint and defaulted when
///     absent. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ImageHint(string? PreferredName, string MediaType,
    int? WidthPx = null, int? HeightPx = null, int? SourcePage = null, string? SourceRef = null,
    ImageTransform? Transform = null, string? Description = null, string? DescriptionSource = null,
    IReadOnlyList<int>? SourcePages = null, bool ReferencedByTemplate = false);
