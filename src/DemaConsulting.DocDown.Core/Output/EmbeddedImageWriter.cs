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
///         readability caveat, the size-skip gap, and the unhonored force-PNG note carry
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
    /// <param name="options">The effective options governing the size limits and output mode. Must not be null.</param>
    /// <param name="images">The resolved images in document order, already deduplicated by package part. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The accounting of what was found, written, skipped for size, written as a vector metafile, and left unconverted under a PNG request.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/>, <paramref name="options"/>, or <paramref name="images"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Deduplicates by SHA-256 so <see cref="EmbeddedImageWriteResult.Found"/> counts each
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
        var forcePngUnhonored = 0;

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

            // Account for each distinct written file once: a vector metafile carries a readability
            // caveat, and a non-PNG file written under a PNG request could not be honored
            if (writtenPaths.Add(path))
            {
                if (IsVectorMetafile(image.MediaType))
                {
                    vectorWritten++;
                }

                if (options.ImageOutput == ImageOutputMode.ForcePng && !IsPng(image.MediaType))
                {
                    forcePngUnhonored++;
                }
            }
        }

        return new EmbeddedImageWriteResult
        {
            Found = found,
            WrittenCount = writtenPaths.Count,
            PathsBySourceRef = pathsBySourceRef,
            SizeSkippedCount = sizeSkipped,
            SizeSkippedItems = sizeSkippedItems,
            VectorWrittenCount = vectorWritten,
            ForcePngUnhonoredCount = forcePngUnhonored
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
    ///     Reports whether an image media type names PNG.
    /// </summary>
    /// <param name="mediaType">The image media type.</param>
    /// <returns><see langword="true"/> for <c>image/png</c>; otherwise <see langword="false"/>.</returns>
    /// <remarks>Used to decide whether a force-PNG request was already satisfied by the stored bytes. Pure.</remarks>
    private static bool IsPng(string mediaType) =>
        mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

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
///     written as a vector metafile, and left unconverted under a PNG request.
/// </summary>
/// <remarks>
///     A backend turns this into its own gaps and diagnostics: it reports the found count, and — when
///     the corresponding count is non-zero — the vector readability caveat, the size-skip gap, and
///     the unhonored force-PNG note. Immutable once constructed and thread-safe.
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

    /// <summary>Gets the number of distinct written files not in PNG form under a force-PNG request.</summary>
    public required int ForcePngUnhonoredCount { get; init; }
}
