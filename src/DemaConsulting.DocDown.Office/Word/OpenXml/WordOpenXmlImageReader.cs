using DocDown.Core;
using DocDown.Word.Markdown;
using DocumentFormat.OpenXml.Packaging;

namespace DocDown.Word.OpenXml;

/// <summary>
///     Resolves the embedded images of a Word document to their bytes and provenance.
/// </summary>
/// <remarks>
///     <para>
///         An Open XML image part stores a complete image file byte-for-byte, so every image this
///         unit yields is a passthrough: the bytes are written unchanged and the provenance says so.
///         The pixel dimensions stay unknown because a Word drawing records an EMU display size, not
///         a pixel count, and reporting EMU as pixels would be a false provenance claim.
///     </para>
///     <para>
///         Deduplication is Core's job, by SHA-256, so the same logo referenced many times is stored
///         once and every link resolves to it; this unit simply yields each part it is asked for.
///         Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordOpenXmlImageReader
{
    /// <summary>
    ///     Resolves a blip relationship id to an image reference within a part container.
    /// </summary>
    /// <param name="container">The part the relationship is scoped to (the main part, or a header or footer part). Must not be null.</param>
    /// <param name="embedId">The <c>r:embed</c> relationship id from a blip.</param>
    /// <param name="candidates">
    ///     The document-supplied text candidates for the image, in preference order, gathered by the
    ///     reader (description, title, caption, object name, nearest heading). Must not be null; the
    ///     media part name is appended here as the final fallback candidate.
    /// </param>
    /// <returns>The resolved image reference, or <see langword="null"/> when the id does not resolve to an image part.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Scoping to the container matters because a header or footer references its own image parts,
    ///     not the main part's. Read-only. A relationship that does not resolve to an image part
    ///     yields <see langword="null"/> rather than throwing. The shared
    ///     <see cref="ImageTextSelector"/> chooses the naming hint, alt text, and manifest description
    ///     so a heading helps name the file yet is never asserted as its description.
    /// </remarks>
    public static WordImageRef? Resolve(
        OpenXmlPartContainer container, string? embedId, IReadOnlyList<ImageTextCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(candidates);
        if (string.IsNullOrEmpty(embedId))
        {
            return null;
        }

        if (container.GetPartById(embedId) is not ImagePart imagePart)
        {
            return null;
        }

        var bytes = ReadPartBytes(imagePart);
        var sourceRef = imagePart.Uri.ToString();

        // Append the media-name fallback so the policy always has a last resort, then let the shared
        // ranker pick the best source; the media name only wins when nothing better exists
        var ranked = new List<ImageTextCandidate>(candidates)
        {
            new(NameFromUri(imagePart.Uri), ImageTextSource.Uri)
        };
        var selected = ImageTextSelector.Select(ranked);
        return BuildReference(bytes, imagePart.ContentType, sourceRef, selected);
    }

    /// <summary>
    ///     Builds an image reference from resolved bytes and the selected image text.
    /// </summary>
    /// <param name="bytes">The complete image bytes.</param>
    /// <param name="mediaType">The image media type from the part content type.</param>
    /// <param name="sourceRef">The part URI recorded as provenance.</param>
    /// <param name="selected">The selected image text, or <see langword="null"/> when nothing was usable.</param>
    /// <returns>The image reference with its naming hint, alt text, and manifest description set honestly.</returns>
    /// <remarks>
    ///     The slug hint uses any usable selection; alt text is set only for a descriptive selection;
    ///     the manifest description and its source are set for descriptive and contextual selections
    ///     but not for the bare media-name fallback, so a heading is captured with its provenance yet
    ///     never presented as the picture's description. Pure.
    /// </remarks>
    private static WordImageRef BuildReference(
        byte[] bytes, string mediaType, string sourceRef, SelectedImageText? selected)
    {
        // The file name may be seeded from any usable source, including a heading, because a naming
        // hint is not a claim about the picture's content
        var preferredName = selected?.Text;

        // Alt text is a claim about the picture, so it is asserted only for a descriptive source;
        // a heading or the bare media name leaves it null and a neutral placeholder is emitted
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

        return new WordImageRef(bytes, mediaType, preferredName, sourceRef, altText, description, descriptionSource);
    }

    /// <summary>
    ///     Reads an image part fully into a byte array.
    /// </summary>
    /// <param name="imagePart">The image part to read.</param>
    /// <returns>The complete image bytes.</returns>
    /// <remarks>Read-only I/O.</remarks>
    private static byte[] ReadPartBytes(ImagePart imagePart)
    {
        using var stream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Derives a base name from an image part URI.
    /// </summary>
    /// <param name="uri">The part URI within the package.</param>
    /// <returns>The file name portion of the URI, or <see langword="null"/> when none.</returns>
    /// <remarks>Pure.</remarks>
    private static string? NameFromUri(Uri uri)
    {
        var path = uri.ToString();
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
