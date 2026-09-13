using DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocDown.PowerPoint.OpenXml;

/// <summary>
///     Resolves the embedded images of a presentation to their bytes and provenance, across every
///     part that can carry a picture — slides, their notes, and the slide layouts and masters — and
///     records which slides reference each image so the association survives extraction.
/// </summary>
/// <remarks>
///     <para>
///         An Open XML image part stores a complete image file byte-for-byte, so every image this
///         unit yields is a passthrough. A deck reuses a single media part across many slides, and
///         its logos and diagrams live on layouts and masters as much as on slides, so walking slides
///         alone under-extracts. This unit therefore gathers image parts from slides, notes slides,
///         layouts, and masters, and deduplicates by package-part identity so a part shared across
///         many slides is yielded once. The sink deduplicates again by content, so nothing is written
///         twice regardless.
///     </para>
///     <para>
///         Crucially, a part referenced by several slides records <em>every</em> referring slide in
///         its <see cref="EmbeddedImage.SourcePages"/>, and a part reached only through a layout or
///         master is flagged <see cref="EmbeddedImage.ReferencedByTemplate"/> rather than given a
///         fabricated slide — so a reader can tell "on slides 3 and 7" from "template furniture" from
///         "true orphan". Alongside the deck images it returns, per slide, the ordered list of the
///         images that slide shows, so the emitter can link each one inline at its point of
///         occurrence following Word's convention.
///     </para>
///     <para>
///         A picture in PowerPoint carries both a raster fallback (<c>a:blip</c>) and, when the author
///         inserted a scalable graphic, the true vector asset (an <c>svgBlip</c>); both are real,
///         distinct media parts and both are yielded. Naming candidates come from the referencing
///         picture's non-visual properties (description, title, object name) so a file is named from
///         the document's own meaning. Read-only; stateless and thread-safe.
///     </para>
/// </remarks>
internal static class PowerPointOpenXmlImageReader
{
    /// <summary>The Open Packaging relationships namespace carrying the <c>r:embed</c> attribute.</summary>
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    ///     Collects every embedded image referenced anywhere in the presentation, recording each
    ///     image's every referring slide and the per-slide image occurrences for inline linking.
    /// </summary>
    /// <param name="presentationPart">The presentation part whose slides, notes, layouts, and masters are walked.</param>
    /// <param name="slideOrdinals">The map from a slide part to its 1-based presentation ordinal, used to record image provenance.</param>
    /// <returns>The resolved images (each distinct part once, in slide-then-template order) and the per-slide image references.</returns>
    /// <remarks>
    ///     Slides are walked first in presentation order (with each slide's notes), then the masters
    ///     and their layouts, so template furniture follows content and image files keep their
    ///     stable, content-order names. Every referring slide is accumulated into each part's referrer
    ///     set; a part reached only through a layout or master is flagged as template-referenced. Read-only.
    /// </remarks>
    public static PowerPointImageCollection Collect(
        PresentationPart presentationPart, IReadOnlyDictionary<SlidePart, int> slideOrdinals)
    {
        ArgumentNullException.ThrowIfNull(presentationPart);
        ArgumentNullException.ThrowIfNull(slideOrdinals);

        var accumulators = new Dictionary<string, PartAccumulator>(StringComparer.Ordinal);
        var emitOrder = new List<string>();
        var slideRefUris = new Dictionary<int, List<string>>();

        // Slides in presentation order, each with its notes, so content precedes template furniture
        foreach (var (slidePart, ordinal) in slideOrdinals.OrderBy(pair => pair.Value))
        {
            RecordContainer(slidePart, MapCandidatesByPart(slidePart), accumulators, emitOrder,
                slideOrdinal: ordinal, referencedByTemplate: false);
            slideRefUris[ordinal] = OrderedPictureUris(slidePart);

            // A notes slide is not a slide render and no slide-layout/master, so an image it alone
            // carries is recorded as neither a slide referrer nor template furniture; it is still
            // extracted. Such an image does not arise in practice, but is represented honestly if it does.
            if (slidePart.NotesSlidePart is { } notesPart)
            {
                RecordContainer(notesPart, MapCandidatesByPart(notesPart), accumulators, emitOrder,
                    slideOrdinal: null, referencedByTemplate: false);
            }
        }

        // Masters and their layouts carry the deck's shared logos and backgrounds
        foreach (var masterPart in presentationPart.SlideMasterParts)
        {
            RecordContainer(masterPart, MapCandidatesByPart(masterPart), accumulators, emitOrder,
                slideOrdinal: null, referencedByTemplate: true);
            foreach (var layoutPart in masterPart.SlideLayoutParts)
            {
                RecordContainer(layoutPart, MapCandidatesByPart(layoutPart), accumulators, emitOrder,
                    slideOrdinal: null, referencedByTemplate: true);
            }
        }

        // Build the distinct images in first-occurrence (content-then-template) order
        var images = new List<EmbeddedImage>(emitOrder.Count);
        var altByUri = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var uri in emitOrder)
        {
            var accumulator = accumulators[uri];
            var image = BuildImage(accumulator);
            images.Add(image);
            altByUri[uri] = image.AltText;
        }

        // Resolve each slide's ordered image occurrences into inline references carrying alt text
        var slideImageRefs = new Dictionary<int, IReadOnlyList<PowerPointSlideImageRef>>();
        foreach (var (ordinal, uris) in slideRefUris)
        {
            var refs = new List<PowerPointSlideImageRef>(uris.Count);
            foreach (var uri in uris)
            {
                refs.Add(new PowerPointSlideImageRef(uri, altByUri.TryGetValue(uri, out var alt) ? alt : null));
            }

            slideImageRefs[ordinal] = refs;
        }

        return new PowerPointImageCollection(images, slideImageRefs);
    }

    /// <summary>
    ///     Records every image part a container references into the accumulators, adding a slide
    ///     referrer or template flag as the container dictates.
    /// </summary>
    /// <param name="container">The part to walk (a slide, notes slide, layout, or master).</param>
    /// <param name="candidatesByUri">The naming candidates for each image part the container's pictures reference.</param>
    /// <param name="accumulators">The per-part referrer accumulators, keyed by part URI.</param>
    /// <param name="emitOrder">The first-occurrence order of part URIs, so image files keep stable names.</param>
    /// <param name="slideOrdinal">The 1-based slide ordinal to record as a referrer, or <see langword="null"/> for a non-slide container.</param>
    /// <param name="referencedByTemplate"><see langword="true"/> when the container is a layout or master.</param>
    /// <remarks>
    ///     Every image part of the container is recorded — including a background or fill image no
    ///     picture names — so nothing embedded is missed. A part already seen keeps its first
    ///     container's naming candidates and simply gains the new referrer. Side effect: mutates the
    ///     accumulators and emit order.
    /// </remarks>
    private static void RecordContainer(
        OpenXmlPartContainer container, IReadOnlyDictionary<string, IReadOnlyList<ImageTextCandidate>> candidatesByUri,
        Dictionary<string, PartAccumulator> accumulators, List<string> emitOrder,
        int? slideOrdinal, bool referencedByTemplate)
    {
        foreach (var imagePart in container.GetPartsOfType<ImagePart>())
        {
            var uri = imagePart.Uri.ToString();
            if (!accumulators.TryGetValue(uri, out var accumulator))
            {
                accumulator = new PartAccumulator(imagePart)
                {
                    Candidates = candidatesByUri.TryGetValue(uri, out var found) ? found : []
                };
                accumulators[uri] = accumulator;
                emitOrder.Add(uri);
            }

            if (slideOrdinal is { } ordinal)
            {
                accumulator.Slides.Add(ordinal);
            }

            accumulator.ReferencedByTemplate |= referencedByTemplate;
        }
    }

    /// <summary>
    ///     Reads the ordered, distinct image-part URIs a slide's pictures reference, in reading order.
    /// </summary>
    /// <param name="slidePart">The slide part whose pictures are walked.</param>
    /// <returns>The image-part URIs the slide shows, in document order, each once.</returns>
    /// <remarks>
    ///     Used to emit an inline link at each image's point of occurrence. A background or fill image
    ///     no picture names is intentionally absent here (it has no reading-order occurrence) yet is
    ///     still extracted and still records this slide as a referrer via the rel-based walk. Read-only.
    /// </remarks>
    private static List<string> OrderedPictureUris(SlidePart slidePart)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (slidePart.RootElement is null)
        {
            return ordered;
        }

        foreach (var picture in slidePart.RootElement.Descendants<P.Picture>())
        {
            foreach (var embedId in EmbedIds(picture))
            {
                if (slidePart.GetPartById(embedId) is ImagePart imagePart
                    && seen.Add(imagePart.Uri.ToString()))
                {
                    ordered.Add(imagePart.Uri.ToString());
                }
            }
        }

        return ordered;
    }

    /// <summary>
    ///     Maps each image part a container references to the naming candidates of the picture that
    ///     references it.
    /// </summary>
    /// <param name="container">The part whose pictures are walked.</param>
    /// <returns>The map from an image part URI to its picture's naming candidates.</returns>
    /// <remarks>
    ///     A picture carries its description, title, and object name in its non-visual properties, and
    ///     references its raster and vector media through <c>r:embed</c>; resolving each embed to its
    ///     part lets the media be named from the picture's own text. A part referenced by more than
    ///     one picture keeps the first picture's candidates. Read-only.
    /// </remarks>
    private static Dictionary<string, IReadOnlyList<ImageTextCandidate>> MapCandidatesByPart(
        OpenXmlPartContainer container)
    {
        var map = new Dictionary<string, IReadOnlyList<ImageTextCandidate>>(StringComparer.Ordinal);
        if (container is not OpenXmlPart part || part.RootElement is null)
        {
            return map;
        }

        foreach (var picture in part.RootElement.Descendants<P.Picture>())
        {
            var candidates = CandidatesFor(picture);
            foreach (var embedId in EmbedIds(picture))
            {
                if (container.GetPartById(embedId) is ImagePart imagePart)
                {
                    map.TryAdd(imagePart.Uri.ToString(), candidates);
                }
            }
        }

        return map;
    }

    /// <summary>
    ///     Gathers the naming candidates a picture offers, in preference order.
    /// </summary>
    /// <param name="picture">The picture element.</param>
    /// <returns>The description, title, and object-name candidates.</returns>
    /// <remarks>
    ///     The non-visual drawing properties carry the author's description and title (alt text) and
    ///     the object name; the shared policy discards an auto-generated object name such as
    ///     <c>Picture 1</c>, so only meaningful text names a file. Pure.
    /// </remarks>
    private static IReadOnlyList<ImageTextCandidate> CandidatesFor(P.Picture picture)
    {
        var properties = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
        return
        [
            new ImageTextCandidate(properties?.Description?.Value, ImageTextSource.Description),
            new ImageTextCandidate(properties?.Title?.Value, ImageTextSource.Title),
            new ImageTextCandidate(properties?.Name?.Value, ImageTextSource.PictureName)
        ];
    }

    /// <summary>
    ///     Reads every <c>r:embed</c> relationship id a picture carries, across its raster and vector blips.
    /// </summary>
    /// <param name="picture">The picture element.</param>
    /// <returns>The distinct embed relationship ids the picture references.</returns>
    /// <remarks>
    ///     The raster fallback is an <c>a:blip</c>; the scalable graphic is an <c>svgBlip</c> in an
    ///     extension, which the typed model does not surface, so its embed attribute is read from the
    ///     raw element by name. Both are collected so the vector asset is not lost. Pure.
    /// </remarks>
    private static IEnumerable<string> EmbedIds(P.Picture picture)
    {
        var ids = new List<string>();
        foreach (var blip in picture.Descendants<D.Blip>())
        {
            if (blip.Embed?.Value is { Length: > 0 } embed)
            {
                ids.Add(embed);
            }
        }

        // The SVG blip is a raw extension element the typed model does not expose; read its embed by name
        foreach (var element in picture.Descendants<OpenXmlElement>())
        {
            if (!string.Equals(element.LocalName, "svgBlip", StringComparison.Ordinal))
            {
                continue;
            }

            var embed = element.GetAttributes()
                .FirstOrDefault(attribute =>
                    string.Equals(attribute.LocalName, "embed", StringComparison.Ordinal)
                    && string.Equals(attribute.NamespaceUri, RelationshipNamespace, StringComparison.Ordinal))
                .Value;
            if (!string.IsNullOrEmpty(embed))
            {
                ids.Add(embed);
            }
        }

        return ids.Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Reads an accumulated image part fully and builds its reference with naming and every referrer.
    /// </summary>
    /// <param name="accumulator">The accumulated part with its referring slides and template flag.</param>
    /// <returns>The image reference carrying the exact bytes, naming, and the full referrer set.</returns>
    /// <remarks>The media part name is appended by the factory as the final naming fallback. Read-only I/O.</remarks>
    private static EmbeddedImage BuildImage(PartAccumulator accumulator)
    {
        var imagePart = accumulator.Part;
        var bytes = ReadPartBytes(imagePart);
        var sourcePages = accumulator.Slides.Count > 0 ? accumulator.Slides.ToArray() : null;
        return EmbeddedImage.Create(
            bytes, imagePart.ContentType, imagePart.Uri.ToString(), accumulator.Candidates,
            NameFromUri(imagePart.Uri), sourcePage: null,
            sourcePages: sourcePages, referencedByTemplate: accumulator.ReferencedByTemplate);
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

    /// <summary>
    ///     The accumulating provenance of one distinct image part: the part itself, its naming
    ///     candidates, every slide that references it, and whether a template does.
    /// </summary>
    /// <remarks>Mutable scratch state used only while walking the package; never leaves this unit.</remarks>
    private sealed class PartAccumulator(ImagePart part)
    {
        /// <summary>Gets the image part whose bytes and content type are read once at the end.</summary>
        public ImagePart Part { get; } = part;

        /// <summary>Gets or sets the naming candidates from the first picture that referenced the part.</summary>
        public IReadOnlyList<ImageTextCandidate> Candidates { get; set; } = [];

        /// <summary>Gets the sorted, distinct 1-based slide ordinals that reference the part.</summary>
        public SortedSet<int> Slides { get; } = [];

        /// <summary>Gets or sets whether a slide layout or master references the part.</summary>
        public bool ReferencedByTemplate { get; set; }
    }
}

/// <summary>
///     The result of collecting a deck's embedded images: the distinct images with their full
///     referrer sets, and the per-slide ordered image references used for inline linking.
/// </summary>
/// <param name="Images">The distinct embedded images, in content-then-template order, each carrying every referrer.</param>
/// <param name="SlideImageRefs">The map from a 1-based slide ordinal to the images that slide shows, in reading order.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointImageCollection(
    IReadOnlyList<EmbeddedImage> Images,
    IReadOnlyDictionary<int, IReadOnlyList<PowerPointSlideImageRef>> SlideImageRefs);
