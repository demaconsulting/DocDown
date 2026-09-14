using System.Security.Cryptography;

namespace DocDown.Core;

/// <summary>
///     Writes a backend's resolved embedded images through the extraction sink, applying the
///     caller's size limits and content deduplication, and returns an honest accounting a backend
///     turns into its own gaps and diagnostics.
/// </summary>
/// <remarks>
///     <para>
///         The Open XML backends (PowerPoint, Excel, Visio) all deliver the same kind of thing — a
///         list of complete image files resolved from package parts — so the mechanical write path is
///         written once here: deduplicate byte-identical images so a logo reused across many slides is
///         one file, skip an image that exceeds a caller's dimension or byte limit rather than writing
///         it, and pass every kept image through the sink unchanged as a passthrough. Naming, the
///         final relative path, and content deduplication across the whole run remain the sink's job.
///     </para>
///     <para>
///         What this unit deliberately does not do is decide how to report the result: the vector
///         readability caveat and the size-skip gap carry
///         backend-owned diagnostic codes and prose, so each backend inspects the returned accounting
///         and reports its own. This keeps the honesty policy uniform while leaving each backend its
///         own voice. Callers invoke this only when embedded-image extraction is enabled; suppression
///         is recorded by the sink and the engine. Stateless and thread-safe.
///     </para>
/// </remarks>
public static class EmbeddedImageWriter
{
    /// <summary>The maximum number of size-skipped item references recorded, to bound gap text for a pathological document.</summary>
    private const int MaxAffectedItems = 20;

    /// <summary>
    ///     Writes the resolved images through the sink, honoring the caller's size limits.
    /// </summary>
    /// <param name="sink">The sink every kept image is written through. Must not be null.</param>
    /// <param name="options">The effective options governing the size limits. Must not be null.</param>
    /// <param name="images">The resolved images in document order, already deduplicated by package part. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The accounting of what was found, written, skipped for size, and written as a vector metafile.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/>, <paramref name="options"/>, or <paramref name="images"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Deduplicates by content so <see cref="EmbeddedImageWriteResult.Found"/> counts each
    ///     distinct image once and a reused part never inflates the denominator, then applies the
    ///     dimension limit (for a raster image whose header can be read) and the byte limit before
    ///     writing. Side effect: writes images and reads from the sink.
    /// </remarks>
    public static async ValueTask<EmbeddedImageWriteResult> WriteAsync(
        IExtractionSink sink, ExtractionOptions options,
        IReadOnlyList<EmbeddedImage> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(images);

        var pathsBySourceRef = new Dictionary<string, string>(StringComparer.Ordinal);
        var writtenPaths = new HashSet<string>(StringComparer.Ordinal);
        var decisionByContent = new Dictionary<string, string?>(StringComparer.Ordinal);
        var sizeSkippedItems = new List<string>();
        var found = 0;
        var sizeSkipped = 0;
        var vectorWritten = 0;

        // Merge the page associations of byte-identical images up front: two distinct package parts
        // that store the same bytes become one written file, so its referrer set is the union of both
        // parts' referrers rather than only the first seen. Records every reference, never discards.
        var referrersByContent = MergeReferrersByContent(images);

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Deduplicate by content: the same image reused across slides is one distinct image, so it
            // is counted, size-checked, and written exactly once and every reference resolves to it
            var digest = Convert.ToHexString(SHA256.HashData(image.Bytes));
            if (decisionByContent.TryGetValue(digest, out var priorPath))
            {
                if (priorPath is { } shared && image.SourceRef is { } dupRef)
                {
                    pathsBySourceRef[dupRef] = shared;
                }

                continue;
            }

            found++;
            var reference = image.SourceRef ?? $"image {found.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            // A raster image whose header can be read carries a real pixel size; a vector metafile has
            // none, so its dimensions stay unknown and the dimension limit cannot apply to it
            int? width = null;
            int? height = null;
            if (ImageDimensions.TryRead(image.Bytes, out var readWidth, out var readHeight))
            {
                width = readWidth;
                height = readHeight;
                if (options.MaxImageDimensionPx is { } maxDimension
                    && (readWidth > maxDimension || readHeight > maxDimension))
                {
                    sizeSkipped++;
                    Append(sizeSkippedItems, reference);
                    decisionByContent[digest] = null;
                    continue;
                }
            }

            // A payload beyond the caller's byte budget is a deliberate skip, not a loss
            if (options.MaxImageBytes is { } maxBytes && image.Bytes.LongLength > maxBytes)
            {
                sizeSkipped++;
                Append(sizeSkippedItems, reference);
                decisionByContent[digest] = null;
                continue;
            }

            var merged = referrersByContent.TryGetValue(digest, out var found2) ? found2 : null;
            var sourcePages = merged is { Pages.Count: > 0 } ? merged.Pages.ToArray() : null;
            var referencedByTemplate = merged?.Template ?? image.ReferencedByTemplate;

            var hint = new ImageHint(
                PreferredName: image.PreferredName,
                MediaType: image.MediaType,
                WidthPx: width,
                HeightPx: height,
                SourcePage: sourcePages is { Length: > 0 } ? sourcePages[0] : image.SourcePage,
                SourceRef: image.SourceRef,
                Transform: ImageTransform.Passthrough,
                Description: image.Description,
                DescriptionSource: image.DescriptionSource,
                SourcePages: sourcePages,
                ReferencedByTemplate: referencedByTemplate);
            using var stream = new MemoryStream(image.Bytes, writable: false);
            var path = await sink.AddImageAsync(stream, hint, cancellationToken).ConfigureAwait(false);
            decisionByContent[digest] = string.IsNullOrEmpty(path) ? null : path;
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (image.SourceRef is { } sourceRef)
            {
                pathsBySourceRef[sourceRef] = path;
            }

            // Account for each distinct written file once: a vector metafile carries a readability caveat
            if (writtenPaths.Add(path) && IsVectorMetafile(image.MediaType))
            {
                vectorWritten++;
            }
        }

        return new EmbeddedImageWriteResult
        {
            Found = found,
            WrittenCount = writtenPaths.Count,
            PathsBySourceRef = pathsBySourceRef,
            SizeSkippedCount = sizeSkipped,
            SizeSkippedItems = sizeSkippedItems,
            VectorWrittenCount = vectorWritten
        };
    }

    /// <summary>
    ///     Merges the page associations of byte-identical images, keyed by their content digest.
    /// </summary>
    /// <param name="images">The resolved images, possibly including distinct package parts with identical bytes.</param>
    /// <returns>The map from a content digest to the union of its referring pages and template flag.</returns>
    /// <remarks>
    ///     Two distinct package parts that store the same bytes are written as one file, so its honest
    ///     referrer set is the union of both parts' referrers; recording only the first seen would
    ///     discard a real reference, which is exactly what this change forbids. Pure aside from hashing.
    /// </remarks>
    private static Dictionary<string, MergedReferrers> MergeReferrersByContent(IReadOnlyList<EmbeddedImage> images)
    {
        var map = new Dictionary<string, MergedReferrers>(StringComparer.Ordinal);
        foreach (var image in images)
        {
            var digest = Convert.ToHexString(SHA256.HashData(image.Bytes));
            if (!map.TryGetValue(digest, out var merged))
            {
                merged = new MergedReferrers();
                map[digest] = merged;
            }

            if (image.SourcePages is { } pages)
            {
                foreach (var page in pages)
                {
                    merged.Pages.Add(page);
                }
            }

            if (image.SourcePage is { } scalar)
            {
                merged.Pages.Add(scalar);
            }

            merged.Template |= image.ReferencedByTemplate;
        }

        return map;
    }

    /// <summary>The accumulating referrer set of one distinct written image: every referring page and whether a template references it.</summary>
    private sealed class MergedReferrers
    {
        /// <summary>Gets the sorted, distinct 1-based referring pages, slides, or worksheets.</summary>
        public SortedSet<int> Pages { get; } = [];

        /// <summary>Gets or sets whether any template container (layout or master) references the image.</summary>
        public bool Template { get; set; }
    }

    /// <summary>
    ///     Reports whether an image media type names an EMF or WMF vector metafile.
    /// </summary>
    /// <param name="mediaType">The image media type.</param>
    /// <returns><see langword="true"/> for an EMF or WMF vector metafile; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Only the Windows metafiles carry the caveat, because they are the vector images most
    ///     viewers and image libraries cannot render; SVG is a vector image too but is broadly
    ///     renderable, so it is written through without a caveat. Pure.
    /// </remarks>
    public static bool IsVectorMetafile(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return mediaType.Contains("emf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("wmf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Appends a reference to a bounded list, ignoring further items once the cap is reached.
    /// </summary>
    /// <param name="items">The list to append to.</param>
    /// <param name="reference">The reference to append.</param>
    /// <remarks>Bounds gap text for a pathological document while the count stays exact. Side effect: mutates <paramref name="items"/>.</remarks>
    private static void Append(List<string> items, string reference)
    {
        if (items.Count < MaxAffectedItems)
        {
            items.Add(reference);
        }
    }
}

/// <summary>
///     The accounting of one embedded-image write pass: what was found, written, skipped for size,
///     and written as a vector metafile.
/// </summary>
/// <remarks>
///     A backend turns this into its own gaps and diagnostics: it reports the found count, and — when
///     the corresponding count is non-zero — the vector readability caveat, the size-skip gap, and
///     Immutable once constructed and thread-safe.
/// </remarks>
public sealed class EmbeddedImageWriteResult
{
    /// <summary>Gets the number of distinct images found (deduplicated by content), the honest denominator.</summary>
    public required int Found { get; init; }

    /// <summary>Gets the number of distinct image files written through the sink.</summary>
    public required int WrittenCount { get; init; }

    /// <summary>Gets the map from an image's package-part reference to its written relative path.</summary>
    public required IReadOnlyDictionary<string, string> PathsBySourceRef { get; init; }

    /// <summary>Gets the number of distinct images skipped because they exceeded a caller size limit.</summary>
    public required int SizeSkippedCount { get; init; }

    /// <summary>Gets the bounded, ordered references of the size-skipped images.</summary>
    public required IReadOnlyList<string> SizeSkippedItems { get; init; }

    /// <summary>Gets the number of distinct written files that are EMF or WMF vector metafiles.</summary>
    public required int VectorWrittenCount { get; init; }
}

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

/// <summary>
///     How an extractor produced the image bytes it hands to the sink.
/// </summary>
/// <remarks>
///     <para>
///         Image provenance is a claim the manifest makes about every extracted image, so the set
///         of legal claims must be closed rather than free-form: a manifest that labels a
///         decode-and-re-encode as a byte-for-byte passthrough is a false provenance claim, and
///         downstream trust in every other manifest field rests on no field being decorative.
///         Modeling the vocabulary as an enumeration makes the closure structural — an extractor
///         cannot name a transform that does not exist, and <c>ManifestWriter</c>'s projection
///         throws on any member it has not been taught to serialize.
///     </para>
///     <para>
///         The set is exactly two members because there are exactly two ways bytes reach
///         <c>images/</c>: either the source document's stored bytes are already a complete image
///         file and are written unchanged, or the source samples must be decoded and re-encoded as
///         PNG to be usable. A third member would itself be a documented value that nothing can produce,
///         which is the defect this enumeration exists to prevent.
///     </para>
/// </remarks>
public enum ImageTransform
{
    /// <summary>The bytes were written exactly as the source document stored them.</summary>
    /// <remarks>
    ///     The default when an extractor reports no transform, because Core writes whatever bytes
    ///     it is given verbatim and therefore changes nothing on its own.
    /// </remarks>
    Passthrough,

    /// <summary>The source samples were decoded and re-encoded as PNG.</summary>
    /// <remarks>
    ///     Reported by an extractor whose source encoding is not itself a usable image file — for
    ///     example a compressed sample buffer — so the written bytes are a new encoding rather
    ///     than the stored ones.
    /// </remarks>
    DecodedToPng
}
