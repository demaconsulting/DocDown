using DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocDown.Excel.OpenXml;

/// <summary>
///     Resolves the embedded images of a workbook to their bytes and provenance, from the drawing
///     part attached to each worksheet, recording which worksheet references each image so the
///     association survives extraction.
/// </summary>
/// <remarks>
///     <para>
///         An Open XML image part stores a complete image file byte-for-byte, so every image this
///         unit yields is a passthrough. A worksheet's pictures live in a drawing part related to it,
///         each picture referencing its media through a blip; this unit walks each worksheet's drawing
///         part <em>in workbook (tab) order</em>, yields every image part it holds, and deduplicates
///         by package-part identity so an image reused across sheets is yielded once. The sink
///         deduplicates again by content.
///     </para>
///     <para>
///         Each yielded image records the 1-based tab index of every worksheet that references it in
///         its <see cref="EmbeddedImage.SourcePages"/>, so a picture shown on sheets 2 and 4 records
///         both — a workbook has no template concept, so no image is ever flagged template-referenced.
///         Alongside the images it returns, per worksheet, the ordered list of the pictures that sheet
///         shows so the emitter can link each one inline under its worksheet section.
///     </para>
///     <para>
///         Naming candidates come from the referencing picture's non-visual properties (description,
///         title, object name) so a file is named from the document's own meaning; a picture that
///         also carries a scalable vector graphic yields that too. Read-only; stateless and
///         thread-safe.
///     </para>
/// </remarks>
internal static class ExcelOpenXmlImageReader
{
    /// <summary>The Open Packaging relationships namespace carrying the <c>r:embed</c> attribute.</summary>
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    ///     Collects every embedded image referenced by the workbook's worksheet drawings, in workbook
    ///     order, recording each image's every referring worksheet and the per-worksheet occurrences.
    /// </summary>
    /// <param name="workbookPart">The workbook part whose worksheet drawings are walked.</param>
    /// <returns>The resolved images (each distinct part once, in worksheet order) and the per-worksheet image references.</returns>
    /// <remarks>
    ///     Worksheets are resolved in workbook (tab) order via the workbook's sheet list — the same
    ///     order the cell reader uses — so the recorded sheet index matches what a reader sees on the
    ///     tab, not the arbitrary part order. A worksheet without a drawing part contributes nothing.
    ///     A part already seen keeps its first sheet's naming candidates and gains the new referrer. Read-only.
    /// </remarks>
    public static ExcelImageCollection Collect(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);

        var accumulators = new Dictionary<string, PartAccumulator>(StringComparer.Ordinal);
        var emitOrder = new List<string>();
        var sheetRefUris = new Dictionary<int, List<string>>();

        var sheetIndex = 1;
        foreach (var sheet in workbookPart.Workbook?.Sheets?.Elements<S.Sheet>() ?? [])
        {
            var worksheetPart = sheet.Id?.Value is { } relationshipId
                && workbookPart.GetPartById(relationshipId) is WorksheetPart part
                ? part
                : null;
            if (worksheetPart?.DrawingsPart is { } drawingsPart)
            {
                RecordDrawing(drawingsPart, sheetIndex, accumulators, emitOrder, sheetRefUris);
            }

            sheetIndex++;
        }

        // Build the distinct images in first-occurrence (worksheet) order
        var images = new List<EmbeddedImage>(emitOrder.Count);
        var altByUri = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var uri in emitOrder)
        {
            var accumulator = accumulators[uri];
            var image = BuildImage(accumulator);
            images.Add(image);
            altByUri[uri] = image.AltText;
        }

        // Resolve each worksheet's ordered image occurrences into inline references carrying alt text
        var sheetImageRefs = new Dictionary<int, IReadOnlyList<ExcelSheetImageRef>>();
        foreach (var (index, uris) in sheetRefUris)
        {
            var refs = new List<ExcelSheetImageRef>(uris.Count);
            foreach (var uri in uris)
            {
                refs.Add(new ExcelSheetImageRef(uri, altByUri.TryGetValue(uri, out var alt) ? alt : null));
            }

            sheetImageRefs[index] = refs;
        }

        return new ExcelImageCollection(images, sheetImageRefs);
    }

    /// <summary>
    ///     Records every image part a worksheet's drawing references into the accumulators, adding the
    ///     worksheet's tab index as a referrer and its ordered picture occurrences.
    /// </summary>
    /// <param name="drawingsPart">The worksheet's drawing part.</param>
    /// <param name="sheetIndex">The 1-based tab index of the worksheet that owns the drawing.</param>
    /// <param name="accumulators">The per-part referrer accumulators, keyed by part URI.</param>
    /// <param name="emitOrder">The first-occurrence order of part URIs, so image files keep stable names.</param>
    /// <param name="sheetRefUris">The per-sheet ordered image-part URIs, for inline linking.</param>
    /// <remarks>Side effect: mutates the accumulators, emit order, and per-sheet references.</remarks>
    private static void RecordDrawing(
        DrawingsPart drawingsPart, int sheetIndex, Dictionary<string, PartAccumulator> accumulators,
        List<string> emitOrder, Dictionary<int, List<string>> sheetRefUris)
    {
        var candidatesByUri = MapCandidatesByPart(drawingsPart);
        var ordered = OrderedPictureUris(drawingsPart);
        sheetRefUris[sheetIndex] = ordered;

        foreach (var imagePart in drawingsPart.GetPartsOfType<ImagePart>())
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

            accumulator.Sheets.Add(sheetIndex);
        }
    }

    /// <summary>
    ///     Reads the ordered, distinct image-part URIs a drawing's pictures reference, in drawing order.
    /// </summary>
    /// <param name="drawingsPart">The drawing part whose pictures are walked.</param>
    /// <returns>The image-part URIs the worksheet shows, in document order, each once.</returns>
    /// <remarks>Used to emit an inline link at each image's point of occurrence under the worksheet section. Read-only.</remarks>
    private static List<string> OrderedPictureUris(DrawingsPart drawingsPart)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (drawingsPart.WorksheetDrawing is null)
        {
            return ordered;
        }

        foreach (var picture in drawingsPart.WorksheetDrawing.Descendants<Xdr.Picture>())
        {
            foreach (var embedId in EmbedIds(picture))
            {
                if (drawingsPart.GetPartById(embedId) is ImagePart imagePart
                    && seen.Add(imagePart.Uri.ToString()))
                {
                    ordered.Add(imagePart.Uri.ToString());
                }
            }
        }

        return ordered;
    }

    /// <summary>
    ///     Maps each image part a drawing references to the naming candidates of the picture that
    ///     references it.
    /// </summary>
    /// <param name="drawingsPart">The drawing part whose pictures are walked.</param>
    /// <returns>The map from an image part URI to its picture's naming candidates.</returns>
    /// <remarks>A part referenced by more than one picture keeps the first picture's candidates. Read-only.</remarks>
    private static Dictionary<string, IReadOnlyList<ImageTextCandidate>> MapCandidatesByPart(DrawingsPart drawingsPart)
    {
        var map = new Dictionary<string, IReadOnlyList<ImageTextCandidate>>(StringComparer.Ordinal);
        if (drawingsPart.WorksheetDrawing is null)
        {
            return map;
        }

        foreach (var picture in drawingsPart.WorksheetDrawing.Descendants<Xdr.Picture>())
        {
            var candidates = CandidatesFor(picture);
            foreach (var embedId in EmbedIds(picture))
            {
                if (drawingsPart.GetPartById(embedId) is ImagePart imagePart)
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
    /// <remarks>The shared policy discards an auto-generated object name such as <c>Picture 1</c>, so only meaningful text names a file. Pure.</remarks>
    private static IReadOnlyList<ImageTextCandidate> CandidatesFor(Xdr.Picture picture)
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
    /// <remarks>The raster fallback is an <c>a:blip</c>; the scalable graphic is an <c>svgBlip</c> read from the raw element. Pure.</remarks>
    private static IEnumerable<string> EmbedIds(Xdr.Picture picture)
    {
        var ids = new List<string>();
        foreach (var blip in picture.Descendants<D.Blip>())
        {
            if (blip.Embed?.Value is { Length: > 0 } embed)
            {
                ids.Add(embed);
            }
        }

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
    /// <param name="accumulator">The accumulated part with its referring worksheets.</param>
    /// <returns>The image reference carrying the exact bytes, naming, and the full referrer set.</returns>
    /// <remarks>The media part name is appended by the factory as the final naming fallback. Read-only I/O.</remarks>
    private static EmbeddedImage BuildImage(PartAccumulator accumulator)
    {
        var imagePart = accumulator.Part;
        var bytes = ReadPartBytes(imagePart);
        var sourcePages = accumulator.Sheets.Count > 0 ? accumulator.Sheets.ToArray() : null;
        return EmbeddedImage.Create(
            bytes, imagePart.ContentType, imagePart.Uri.ToString(), accumulator.Candidates,
            NameFromUri(imagePart.Uri), sourcePage: null, sourcePages: sourcePages);
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
    ///     candidates, and every worksheet that references it.
    /// </summary>
    /// <remarks>Mutable scratch state used only while walking the package; never leaves this unit.</remarks>
    private sealed class PartAccumulator(ImagePart part)
    {
        /// <summary>Gets the image part whose bytes and content type are read once at the end.</summary>
        public ImagePart Part { get; } = part;

        /// <summary>Gets or sets the naming candidates from the first picture that referenced the part.</summary>
        public IReadOnlyList<ImageTextCandidate> Candidates { get; set; } = [];

        /// <summary>Gets the sorted, distinct 1-based worksheet tab indices that reference the part.</summary>
        public SortedSet<int> Sheets { get; } = [];
    }
}

/// <summary>
///     The result of collecting a workbook's embedded images: the distinct images with their full
///     referrer sets, and the per-worksheet ordered image references used for inline linking.
/// </summary>
/// <param name="Images">The distinct embedded images, in worksheet order, each carrying every referrer.</param>
/// <param name="SheetImageRefs">The map from a 1-based worksheet tab index to the images that sheet shows, in drawing order.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelImageCollection(
    IReadOnlyList<EmbeddedImage> Images,
    IReadOnlyDictionary<int, IReadOnlyList<ExcelSheetImageRef>> SheetImageRefs);
