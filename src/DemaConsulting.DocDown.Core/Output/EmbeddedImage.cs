namespace DocDown.Core;

/// <summary>
///     A backend-neutral reference to one embedded image resolved from a document: the exact stored
///     bytes, their media type, and the naming and provenance a backend derived from the document.
/// </summary>
/// <param name="Bytes">The complete image file bytes, exactly as the document stored them.</param>
/// <param name="MediaType">The image media type (for example <c>image/png</c>), from the part content type.</param>
/// <param name="PreferredName">
///     The base name Core slugs the file from — the selected image text of any usable source, the
///     media part name, or <see langword="null"/> to let Core name it from its ordinal alone.
/// </param>
/// <param name="SourceRef">The part URI within the package, recorded as provenance, or <see langword="null"/>.</param>
/// <param name="AltText">
///     Descriptive alt text, present only when a genuinely descriptive source was chosen; otherwise
///     <see langword="null"/> so a neutral placeholder is used instead of implying a description.
/// </param>
/// <param name="Description">
///     The chosen text recorded in the manifest as image metadata, present for descriptive and
///     contextual sources alike; <see langword="null"/> when only the media name was available.
/// </param>
/// <param name="DescriptionSource">
///     The camelCase provenance of <paramref name="Description"/> (for example <c>description</c> or
///     <c>heading</c>), or <see langword="null"/> when there is no description.
/// </param>
/// <param name="SourcePage">
///     The 1-based source page or slide the image came from, or <see langword="null"/> when not
///     applicable. A convenience alias for the first (lowest) entry of <paramref name="SourcePages"/>.
/// </param>
/// <param name="SourcePages">
///     Every 1-based slide, page, or worksheet that references the image, sorted and distinct, or
///     <see langword="null"/> when none are derivable at page level. The authoritative multi-referrer
///     record: a part reused across several slides lists them all here.
/// </param>
/// <param name="ReferencedByTemplate">
///     <see langword="true"/> when the image is referenced through a template container — a
///     PowerPoint slide layout or master, or a Visio master — so a template-borne asset is told apart
///     from a true orphan.
/// </param>
/// <remarks>
///     An Open XML image part stores a complete image file byte-for-byte, so a reference always
///     describes bytes that can be written through unchanged as a passthrough. The naming, alt-text,
///     and manifest-description fields are populated from the shared image-text policy so a heading
///     is used to help name the file yet never asserted as if it described the picture. The same
///     shape serves PowerPoint, Excel, and Visio so the sink-write pipeline is written once.
///     Immutable and thread-safe.
/// </remarks>
public sealed record EmbeddedImage(
    byte[] Bytes,
    string MediaType,
    string? PreferredName = null,
    string? SourceRef = null,
    string? AltText = null,
    string? Description = null,
    string? DescriptionSource = null,
    int? SourcePage = null,
    IReadOnlyList<int>? SourcePages = null,
    bool ReferencedByTemplate = false)
{
    /// <summary>
    ///     Builds an embedded-image reference from resolved bytes and the document-supplied text
    ///     candidates, applying the shared naming and honesty policy.
    /// </summary>
    /// <param name="bytes">The complete image file bytes.</param>
    /// <param name="mediaType">The image media type from the part content type.</param>
    /// <param name="sourceRef">The part URI recorded as provenance, or <see langword="null"/>.</param>
    /// <param name="candidates">
    ///     The document-supplied text candidates for the image, in preference order (description,
    ///     title, object name, and so on). Must not be null; the media part name is appended here as
    ///     the final fallback candidate.
    /// </param>
    /// <param name="mediaName">The media part's own name, appended as the last-resort naming candidate, or <see langword="null"/> when none.</param>
    /// <param name="sourcePage">The 1-based source page or slide, or <see langword="null"/> when not applicable.</param>
    /// <param name="sourcePages">Every 1-based referrer (slide, page, or worksheet), sorted and distinct, or <see langword="null"/> when none.</param>
    /// <param name="referencedByTemplate"><see langword="true"/> when a template container (layout, master) references the part.</param>
    /// <returns>The image reference with its naming hint, alt text, and manifest description set honestly.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/>, <paramref name="mediaType"/>, or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Mirrors the Word image reader's naming rule: the slug hint may use any usable selection
    ///     including a heading, alt text is set only for a descriptive selection, and the manifest
    ///     description and its source are set for descriptive and contextual selections but never the
    ///     bare media-name fallback. Centralized so every Open XML backend names images identically.
    ///     Pure.
    /// </remarks>
    public static EmbeddedImage Create(
        byte[] bytes, string mediaType, string? sourceRef,
        IReadOnlyList<ImageTextCandidate> candidates, string? mediaName, int? sourcePage = null,
        IReadOnlyList<int>? sourcePages = null, bool referencedByTemplate = false)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(mediaType);
        ArgumentNullException.ThrowIfNull(candidates);

        // Append the media-name fallback so the policy always has a last resort, then let the shared
        // ranker pick the best source; the media name only wins when nothing better exists
        var ranked = new List<ImageTextCandidate>(candidates)
        {
            new(mediaName, ImageTextSource.Uri)
        };
        var selected = ImageTextSelector.Select(ranked);

        // The file name may be seeded from any usable source, including a heading, because a naming
        // hint is not a claim about the picture's content
        var preferredName = selected?.Text;

        // Alt text is a claim about the picture, so it is asserted only for a descriptive source
        var altText = selected is { Confidence: ImageTextConfidence.Descriptive } ? selected.Text : null;

        // The manifest records the chosen text and its provenance for descriptive and contextual
        // sources alike, but never the bare media-name fallback, which describes nothing
        string? description = null;
        string? descriptionSource = null;
        if (selected is not null && selected.Confidence != ImageTextConfidence.Fallback)
        {
            description = selected.Text;
            descriptionSource = ImageTextSelector.SourceName(selected.Source);
        }

        // The referrer set is the authoritative record; the scalar source page is its deterministic
        // first (lowest) entry so a consumer reading only the scalar still sees a stable referrer
        var normalizedPages = sourcePages is { Count: > 0 }
            ? sourcePages.Distinct().OrderBy(page => page).ToArray()
            : null;
        var scalarPage = normalizedPages is { Length: > 0 } ? normalizedPages[0] : sourcePage;

        return new EmbeddedImage(
            bytes, mediaType, preferredName, sourceRef, altText, description, descriptionSource,
            scalarPage, normalizedPages, referencedByTemplate);
    }
}
