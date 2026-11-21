using System.IO.Packaging;
using DemaConsulting.DocDown.Core;

namespace DemaConsulting.DocDown.Visio.OpenXml;

/// <summary>
///     Resolves the embedded images of a Visio drawing to their bytes and provenance, directly from
///     the Open Packaging container because <c>DocumentFormat.OpenXml</c> has zero Visio types, and
///     records which page references each image so the association survives extraction.
/// </summary>
/// <remarks>
///     <para>
///         A Visio drawing embeds a raster or metafile image through a <c>ForeignData</c> shape whose
///         relationship, of the Open Packaging image type, targets a part under <c>visio/media</c>.
///         This unit walks the container's parts, follows every image relationship to its media part,
///         and yields the bytes unchanged as a passthrough, deduplicated by package-part identity so a
///         media part shared across pages is yielded once.
///     </para>
///     <para>
///         An image relationship on a page-contents part records that page's 1-based index as a
///         referrer; a relationship on a master part flags the image as template-referenced rather
///         than giving it a fabricated page — so a reader can tell "on page 2" from "master furniture"
///         from "true orphan". A media part shared across pages records every referring page. Alongside
///         the images it returns, per page, the ordered list of images that page shows so the emitter
///         can link each one inline under its page section.
///     </para>
///     <para>
///         The package thumbnail (<c>docProps/thumbnail.emf</c>) is deliberately excluded: it is
///         referenced from the package root by a <em>thumbnail</em> relationship, not an image
///         relationship on a page or master, and it is document furniture rather than embedded
///         content — reporting it as an embedded image would itself be dishonest. Naming falls back to
///         the media part name, since a foreign-data image carries no authored description. Read-only;
///         stateless and thread-safe.
///     </para>
/// </remarks>
internal static class VisioImageReader
{
    /// <summary>The Open Packaging relationship type of an embedded image.</summary>
    private const string ImageRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>The URI-path prefix of package furniture that is never embedded content (the thumbnail lives here).</summary>
    private const string DocumentPropertiesPrefix = "/docProps/";

    /// <summary>The URI-path marker of a master part, whose images are template furniture rather than page content.</summary>
    private const string MastersPathMarker = "/masters/";

    /// <summary>
    ///     Collects every embedded image the drawing references from a page or master, recording each
    ///     image's every referring page and the per-page occurrences for inline linking.
    /// </summary>
    /// <param name="package">The opened Visio package.</param>
    /// <param name="pageIndexByContentsUri">The map from a page-contents part URI to its 1-based page index.</param>
    /// <returns>The resolved images (each distinct media part once, in package-part order) and the per-page image references.</returns>
    /// <remarks>
    ///     Only internal image relationships are followed; the thumbnail, referenced by a thumbnail
    ///     relationship under <c>docProps</c>, never matches and is additionally excluded by its path
    ///     as a safeguard. Parts are ordered by URI so the result is deterministic and image files keep
    ///     stable names. An image on a page-contents part records that page; one on a master is flagged
    ///     as template-referenced. Read-only.
    /// </remarks>
    public static VisioImageCollection Collect(
        Package package, IReadOnlyDictionary<string, int> pageIndexByContentsUri)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pageIndexByContentsUri);

        var accumulators = new Dictionary<string, PartAccumulator>(StringComparer.Ordinal);
        var emitOrder = new List<string>();
        var pageRefUris = new Dictionary<int, List<string>>();

        foreach (var part in package.GetParts().OrderBy(candidate => candidate.Uri.ToString(), StringComparer.Ordinal))
        {
            // A relationship part cannot itself carry relationships; asking it for any would throw, so
            // it is skipped rather than inspected
            if (PackUriHelper.IsRelationshipPartUri(part.Uri))
            {
                continue;
            }

            var hostUri = part.Uri.ToString();
            var pageIndex = pageIndexByContentsUri.TryGetValue(hostUri, out var index) ? index : (int?)null;
            var isMaster = hostUri.Contains(MastersPathMarker, StringComparison.OrdinalIgnoreCase);

            foreach (var targetUri in EmbeddedImageTargets(package, part))
            {
                var uri = targetUri.ToString();
                if (!accumulators.TryGetValue(uri, out var accumulator))
                {
                    accumulator = new PartAccumulator(package.GetPart(targetUri));
                    accumulators[uri] = accumulator;
                    emitOrder.Add(uri);
                }

                if (pageIndex is { } page)
                {
                    accumulator.Pages.Add(page);
                    AddPageRef(pageRefUris, page, uri);
                }
                else if (isMaster)
                {
                    accumulator.ReferencedByTemplate = true;
                }
            }
        }

        // Build the distinct images in first-occurrence (package-part) order
        var images = new List<EmbeddedImage>(emitOrder.Count);
        var altByUri = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var uri in emitOrder)
        {
            var accumulator = accumulators[uri];
            var image = BuildImage(accumulator);
            images.Add(image);
            altByUri[uri] = image.AltText;
        }

        return new VisioImageCollection(images, BuildPageImageRefs(pageRefUris, altByUri));
    }

    /// <summary>
    ///     Resolves the embedded image parts a package part references.
    /// </summary>
    /// <param name="package">The opened Visio package.</param>
    /// <param name="part">The referring part (a page, master, or other content part).</param>
    /// <returns>The resolved target URIs of the part's image relationships, in relationship order.</returns>
    /// <remarks>
    ///     An external image is a link, not embedded content, so only internal targets are followed;
    ///     package furniture (the thumbnail under <c>docProps</c>) and missing targets are excluded.
    ///     Read-only.
    /// </remarks>
    private static IEnumerable<Uri> EmbeddedImageTargets(Package package, PackagePart part) =>
        part.GetRelationshipsByType(ImageRelationshipType)
            .Where(relationship => relationship.TargetMode == TargetMode.Internal)
            .Select(relationship => PackUriHelper.ResolvePartUri(part.Uri, relationship.TargetUri))
            .Where(target => !target.ToString().StartsWith(DocumentPropertiesPrefix, StringComparison.OrdinalIgnoreCase)
                && package.PartExists(target));

    /// <summary>
    ///     Resolves each page's ordered image occurrences into inline references carrying alt text.
    /// </summary>
    /// <param name="pageRefUris">The per-page ordered image-part URIs.</param>
    /// <param name="altByUri">The alt text chosen for each media part URI.</param>
    /// <returns>The map from a 1-based page index to that page's inline image references.</returns>
    /// <remarks>Pure.</remarks>
    private static Dictionary<int, IReadOnlyList<VisioPageImageRef>> BuildPageImageRefs(
        Dictionary<int, List<string>> pageRefUris, Dictionary<string, string?> altByUri) =>
        pageRefUris.ToDictionary(
            entry => entry.Key,
            IReadOnlyList<VisioPageImageRef> (entry) => entry.Value
                .Select(uri => new VisioPageImageRef(uri, altByUri.GetValueOrDefault(uri)))
                .ToList());

    /// <summary>
    ///     Adds a media part URI to a page's ordered image references, keeping it distinct.
    /// </summary>
    /// <param name="pageRefUris">The per-page ordered image-part URIs.</param>
    /// <param name="page">The 1-based page index.</param>
    /// <param name="uri">The media part URI referenced by the page.</param>
    /// <remarks>Side effect: mutates <paramref name="pageRefUris"/>.</remarks>
    private static void AddPageRef(Dictionary<int, List<string>> pageRefUris, int page, string uri)
    {
        if (!pageRefUris.TryGetValue(page, out var list))
        {
            list = [];
            pageRefUris[page] = list;
        }

        if (!list.Contains(uri, StringComparer.Ordinal))
        {
            list.Add(uri);
        }
    }

    /// <summary>
    ///     Reads an accumulated media part fully and builds its reference with naming and every referrer.
    /// </summary>
    /// <param name="accumulator">The accumulated part with its referring pages and template flag.</param>
    /// <returns>The image reference carrying the exact bytes and the full referrer set.</returns>
    /// <remarks>The media part name is the only naming source; a foreign-data image carries no authored description. Read-only I/O.</remarks>
    private static EmbeddedImage BuildImage(PartAccumulator accumulator)
    {
        var part = accumulator.Part;
        var bytes = ReadPartBytes(part);
        var sourcePages = accumulator.Pages.Count > 0 ? accumulator.Pages.ToArray() : null;
        return EmbeddedImage.Create(
            bytes, part.ContentType, part.Uri.ToString(), [], NameFromUri(part.Uri),
            sourcePage: null, sourcePages: sourcePages, referencedByTemplate: accumulator.ReferencedByTemplate);
    }

    /// <summary>
    ///     Reads a package part fully into a byte array.
    /// </summary>
    /// <param name="part">The part to read.</param>
    /// <returns>The complete part bytes.</returns>
    /// <remarks>Read-only I/O.</remarks>
    private static byte[] ReadPartBytes(PackagePart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Derives a base name from a media part URI.
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

    /// <summary>
    ///     The accumulating provenance of one distinct media part: the part itself, every page that
    ///     references it, and whether a master does.
    /// </summary>
    /// <remarks>Mutable scratch state used only while walking the package; never leaves this unit.</remarks>
    private sealed class PartAccumulator(PackagePart part)
    {
        /// <summary>Gets the media part whose bytes and content type are read once at the end.</summary>
        public PackagePart Part { get; } = part;

        /// <summary>Gets the sorted, distinct 1-based page indices that reference the part.</summary>
        public SortedSet<int> Pages { get; } = [];

        /// <summary>Gets or sets whether a master references the part.</summary>
        public bool ReferencedByTemplate { get; set; }
    }
}

/// <summary>
///     The result of collecting a drawing's embedded images: the distinct images with their full
///     referrer sets, and the per-page ordered image references used for inline linking.
/// </summary>
/// <param name="Images">The distinct embedded images, in package-part order, each carrying every referrer.</param>
/// <param name="PageImageRefs">The map from a 1-based page index to the images that page shows, in relationship order.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioImageCollection(
    IReadOnlyList<EmbeddedImage> Images,
    IReadOnlyDictionary<int, IReadOnlyList<VisioPageImageRef>> PageImageRefs);
