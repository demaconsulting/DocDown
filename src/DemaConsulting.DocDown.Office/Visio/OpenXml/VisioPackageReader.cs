using System.IO.Packaging;
using System.Xml.Linq;
using DocDown.Core;

namespace DocDown.Visio.OpenXml;

/// <summary>
///     Reads a Visio drawing's Open Packaging container directly with <see cref="System.IO.Packaging"/>
///     — because <c>DocumentFormat.OpenXml</c> has zero Visio types — and produces the backend-neutral
///     <see cref="VisioDocumentModel"/>: page names, shape text, and the directed connector topology.
/// </summary>
/// <remarks>
///     <para>
///         A Visio drawing is an engineering schematic whose meaning is carried by which shapes
///         exist, what they are labeled, which page they belong to, and — above all — how they are
///         connected. This reader recovers exactly that. It reads <c>visio/pages/pages.xml</c> for
///         each page's name and its relationship to a <c>visio/pages/pageN.xml</c> part, then reads
///         that part's shapes and their text, and resolves each connector's <c>&lt;Connect&gt;</c>
///         records — whose <c>FromCell</c> of <c>BeginX</c> or <c>EndX</c> carries the direction —
///         into real directed edges between shapes. It also reads <c>visio/masters/masters.xml</c>
///         so a shape that carries no text can still be reported by the master it was instantiated
///         from, which is the drawing's own classification of that shape.
///     </para>
///     <para>
///         The Visio namespace is <c>http://schemas.microsoft.com/office/visio/2012/main</c>. The
///         reader performs no filesystem I/O; the caller hands it a seekable stream. Stateless and
///         safe to reuse.
///     </para>
/// </remarks>
internal static class VisioPackageReader
{
    /// <summary>The Visio 2012 main XML namespace every drawing part uses.</summary>
    private static readonly XNamespace VisioNamespace = "http://schemas.microsoft.com/office/visio/2012/main";

    /// <summary>The Open Packaging relationships namespace used for the <c>r:id</c> attribute.</summary>
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The content type of the pages-collection part.</summary>
    private const string PagesContentType = "application/vnd.ms-visio.pages+xml";

    /// <summary>The conventional part URI of the pages collection.</summary>
    private const string PagesPartUri = "/visio/pages/pages.xml";

    /// <summary>The content type of the masters-collection part.</summary>
    private const string MastersContentType = "application/vnd.ms-visio.masters+xml";

    /// <summary>The conventional part URI of the masters collection.</summary>
    private const string MastersPartUri = "/visio/masters/masters.xml";

    /// <summary>
    ///     The symbol-font code points this reader is prepared to recover, keyed by font name.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Visio stores a Wingdings arrow as the <em>byte</em> <c>0xE0</c> in a run tagged with
    ///         <c>Font="Wingdings"</c>. Read as text that byte decodes to U+00E0, the Latin letter
    ///         <c>à</c>, so a valve-positioning table reads <c>IN à OUT</c> — a faithful decode of a
    ///         meaningless character. The recovery is real (the drawing says the glyph is an arrow)
    ///         but only because the font says so, which is why the mapping is gated on the run's font
    ///         and never applied to text: a blind <c>à</c> replacement would corrupt genuine French.
    ///     </para>
    ///     <para>
    ///         The table is deliberately minimal. Wingdings <c>0xE0</c> is the one code point whose
    ///         symbol-font provenance was confirmed in a real drawing (50 occurrences, every one in a
    ///         run whose <c>Character</c> row declares <c>Wingdings</c>) and whose Unicode equivalent
    ///         — U+2794 HEAVY WIDE-HEADED RIGHTWARDS ARROW — is documented. Every other code point is
    ///         left exactly as the drawing decoded it, because a wrong mapping is worse than a
    ///         faithful oddity. Extending the table requires the same two pieces of evidence.
    ///     </para>
    /// </remarks>
    private static readonly Dictionary<string, Dictionary<char, char>> SymbolFontMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Wingdings"] = new Dictionary<char, char> { ['\u00E0'] = '\u2794' }
        };

    /// <summary>
    ///     Reads a drawing from a seekable stream into the model.
    /// </summary>
    /// <param name="stream">A readable, seekable stream over the <c>.vsdx</c> or <c>.vsdm</c> bytes.</param>
    /// <returns>The document model with its pages, shapes, and directed connections in document order.</returns>
    /// <exception cref="VisioExtractionException">Thrown when the package is not a valid Visio drawing.</exception>
    /// <remarks>Read-only over the stream. Any packaging or XML fault is wrapped so Core sees a structured failure.</remarks>
    public static VisioDocumentModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Package package;
        try
        {
            package = Package.Open(stream, FileMode.Open, FileAccess.Read);
        }
        catch (Exception exception) when (exception is FileFormatException or InvalidDataException or IOException)
        {
            throw new VisioExtractionException(
                "The drawing could not be opened; it may be malformed or not a valid Visio Open Packaging container.",
                exception);
        }

        try
        {
            var pagesPart = FindPagesPart(package)
                ?? throw new VisioExtractionException("The drawing contains no pages part and cannot be read.");

            // Resolve the drawing's masters once, before any page is read: every page's shapes refer
            // to the same document-level master collection, so reading it per page would be waste
            var masterNames = ReadMasterNames(package);

            var pagesXml = LoadXml(pagesPart);
            var pages = new List<VisioPageModel>();
            var pageIndexByContentsUri = new Dictionary<string, int>(StringComparer.Ordinal);
            var pageIndex = 1;
            foreach (var pageElement in pagesXml.Descendants(VisioNamespace + "Page"))
            {
                var (pageModel, contentsUri) = ReadPage(package, pagesPart, pageElement, masterNames);
                if (contentsUri is { } uri)
                {
                    pageIndexByContentsUri[uri] = pageIndex;
                }

                pages.Add(pageModel);
                pageIndex++;
            }

            var imageCollection = VisioImageReader.Collect(package, pageIndexByContentsUri);

            // Attach each page's inline image references so the emitter can link them at occurrence
            for (var index = 0; index < pages.Count; index++)
            {
                if (imageCollection.PageImageRefs.TryGetValue(index + 1, out var refs))
                {
                    pages[index] = pages[index] with { Images = refs };
                }
            }

            return new VisioDocumentModel(pages, imageCollection.Images, OpcMetadataMapper.From(ToCoreProperties(package.PackageProperties)));
        }
        finally
        {
            package.Close();
        }
    }

    /// <summary>
    ///     Copies the package's core properties into the backend-neutral snapshot the shared metadata
    ///     mapper consumes.
    /// </summary>
    /// <param name="properties">The package's core properties.</param>
    /// <returns>The neutral core-property snapshot.</returns>
    /// <remarks>
    ///     A field-for-field copy is the only place <c>System.IO.Packaging.PackageProperties</c> is
    ///     touched; the honesty rules (omit-empty, provenance, date normalization) live once in
    ///     <see cref="OpcMetadataMapper"/>. Pure.
    /// </remarks>
    private static OpcCoreProperties ToCoreProperties(PackageProperties properties) => new(
        properties.Creator,
        properties.LastModifiedBy,
        properties.Created,
        properties.Modified,
        properties.Revision,
        properties.Title,
        properties.Subject,
        properties.Keywords,
        properties.Category,
        properties.ContentStatus,
        properties.Description,
        properties.LastPrinted,
        properties.Version,
        properties.Language,
        properties.Identifier);

    /// <summary>
    ///     Finds the pages-collection part, by its conventional URI and then by content type.
    /// </summary>
    /// <param name="package">The opened package.</param>
    /// <returns>The pages part, or <see langword="null"/> when the drawing declares no pages part.</returns>
    /// <remarks>The conventional URI is tried first because it is the common case; the content-type scan is the robust fallback.</remarks>
    private static PackagePart? FindPagesPart(Package package)
    {
        var uri = new Uri(PagesPartUri, UriKind.Relative);
        if (package.PartExists(uri))
        {
            return package.GetPart(uri);
        }

        return package.GetParts().FirstOrDefault(part =>
            string.Equals(part.ContentType, PagesContentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Reads the drawing's master-id-to-master-name map from its masters collection.
    /// </summary>
    /// <param name="package">The opened package.</param>
    /// <returns>
    ///     The map from master id to master name, empty when the drawing declares no masters part —
    ///     a drawing need not have one, and its absence is an ordinary case, not a fault.
    /// </returns>
    /// <remarks>
    ///     A master is the stencil shape a page shape was instantiated from, and its name is the
    ///     drawing's own classification of that shape ("Tank", "3-way Plug Valve"). Recovering it
    ///     lets a text-less shape be reported by its type instead of by a bare id. <c>Name</c> is
    ///     preferred over <c>NameU</c> because <c>Name</c> is the localized, author-facing name the
    ///     drawing shows, while <c>NameU</c> is the invariant fallback that is present even when a
    ///     master carries no localized name. A master with neither is skipped rather than mapped to
    ///     an empty string.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> ReadMasterNames(Package package)
    {
        var mastersPart = FindMastersPart(package);
        if (mastersPart is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var master in LoadXml(mastersPart).Descendants(VisioNamespace + "Master"))
        {
            var id = master.Attribute("ID")?.Value;
            var name = master.Attribute("Name")?.Value ?? master.Attribute("NameU")?.Value;
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrWhiteSpace(name))
            {
                names[id] = name;
            }
        }

        return names;
    }

    /// <summary>
    ///     Finds the masters-collection part, by its conventional URI and then by content type.
    /// </summary>
    /// <param name="package">The opened package.</param>
    /// <returns>The masters part, or <see langword="null"/> when the drawing declares none.</returns>
    /// <remarks>Mirrors <see cref="FindPagesPart"/>: the conventional URI is the common case and the content-type scan is the robust fallback.</remarks>
    private static PackagePart? FindMastersPart(Package package)
    {
        var uri = new Uri(MastersPartUri, UriKind.Relative);
        if (package.PartExists(uri))
        {
            return package.GetPart(uri);
        }

        return package.GetParts().FirstOrDefault(part =>
            string.Equals(part.ContentType, MastersContentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Reads one page: its name and, when it references a page-contents part, its shapes and
    ///     directed connections.
    /// </summary>
    /// <param name="package">The opened package.</param>
    /// <param name="pagesPart">The pages-collection part, whose relationships resolve the page-contents part.</param>
    /// <param name="pageElement">The <c>&lt;Page&gt;</c> element from the pages collection.</param>
    /// <param name="masterNames">The drawing's master-id-to-name map, used to classify text-less shapes.</param>
    /// <returns>The page model and the URI of its page-contents part, or <see langword="null"/> when it references none.</returns>
    private static (VisioPageModel Model, string? ContentsUri) ReadPage(
        Package package, PackagePart pagesPart, XElement pageElement, IReadOnlyDictionary<string, string> masterNames)
    {
        var name = pageElement.Attribute("Name")?.Value
            ?? pageElement.Attribute("NameU")?.Value
            ?? "Page";

        var relationshipId = pageElement.Element(VisioNamespace + "Rel")?.Attribute(RelationshipNamespace + "id")?.Value;
        if (relationshipId is null)
        {
            return (new VisioPageModel(name, [], []), null);
        }

        var contentsPart = ResolvePart(package, pagesPart, relationshipId);
        if (contentsPart is null)
        {
            return (new VisioPageModel(name, [], []), null);
        }

        var contentsXml = LoadXml(contentsPart);
        var shapes = ReadShapes(contentsXml, masterNames);
        var connections = ResolveConnections(contentsXml, shapes);
        return (new VisioPageModel(name, shapes, connections), contentsPart.Uri.ToString());
    }

    /// <summary>
    ///     Reads every shape on a page-contents part, with its id, its text, and the name of the
    ///     master it was instantiated from.
    /// </summary>
    /// <param name="contentsXml">The page-contents XML.</param>
    /// <param name="masterNames">The drawing's master-id-to-name map.</param>
    /// <returns>The shapes, in document order.</returns>
    /// <remarks>
    ///     Every <c>&lt;Shape&gt;</c>, including those nested inside a group, is read; a shape's own
    ///     direct text is taken. A shape's <c>Master</c> attribute names the stencil master it came
    ///     from; a shape with no such attribute, or one naming a master the drawing does not declare,
    ///     simply carries no master name rather than a guessed one.
    /// </remarks>
    private static IReadOnlyList<VisioShapeModel> ReadShapes(
        XDocument contentsXml, IReadOnlyDictionary<string, string> masterNames)
    {
        var shapes = new List<VisioShapeModel>();
        foreach (var shape in contentsXml.Descendants(VisioNamespace + "Shape"))
        {
            var id = shape.Attribute("ID")?.Value;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var masterId = shape.Attribute("Master")?.Value;
            var masterName = masterId is not null && masterNames.TryGetValue(masterId, out var resolved)
                ? resolved
                : null;

            shapes.Add(new VisioShapeModel(id, TextOf(shape), masterName));
        }

        return shapes;
    }

    /// <summary>
    ///     Reads a shape's own text, ignoring text belonging to nested shapes, recovering any
    ///     symbol-font glyphs the drawing encoded as ordinary characters.
    /// </summary>
    /// <param name="shape">The shape element.</param>
    /// <returns>The shape's text, or <see langword="null"/> when it carries none.</returns>
    /// <remarks>
    ///     Only the shape's direct <c>&lt;Text&gt;</c> child is read, so a group's label is not
    ///     conflated with its members' labels. Within that element the run markers
    ///     (<c>&lt;cp IX="n"/&gt;</c>) select a row of the shape's <c>Character</c> section, and that
    ///     row names the run's font — the only evidence that distinguishes a symbol-font arrow from
    ///     the Latin letter it decodes to. Runs are walked in document order so each character is
    ///     mapped, or left alone, according to the font actually in force for it. Pure.
    /// </remarks>
    private static string? TextOf(XElement shape)
    {
        var textElement = shape.Element(VisioNamespace + "Text");
        if (textElement is null)
        {
            return null;
        }

        // Resolve the shape's own character-formatting rows so a run marker can name its font
        var fontsByRow = CharacterFonts(shape);

        // Walk the element's nodes in document order, tracking the run marker currently in force so
        // each text node is decoded under the font that actually applies to it
        var builder = new System.Text.StringBuilder();
        string? activeRow = null;
        foreach (var node in textElement.DescendantNodes())
        {
            if (node is XElement element)
            {
                if (string.Equals(element.Name.LocalName, "cp", StringComparison.Ordinal))
                {
                    activeRow = element.Attribute("IX")?.Value;
                }

                continue;
            }

            if (node is XText text)
            {
                AppendDecoded(builder, text.Value, FontFor(fontsByRow, activeRow));
            }
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    /// <summary>
    ///     Indexes a shape's <c>Character</c> section rows by index to the font each declares.
    /// </summary>
    /// <param name="shape">The shape whose character formatting is indexed.</param>
    /// <returns>The row-index-to-font-name map; empty when the shape declares no character rows.</returns>
    /// <remarks>
    ///     Only rows that name a font explicitly are recorded. A themed or inherited font is left out
    ///     rather than resolved through the theme, because the mapping this feeds must act only on
    ///     evidence the shape itself states. Pure.
    /// </remarks>
    private static Dictionary<string, string> CharacterFonts(XElement shape)
    {
        var fonts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in shape.Elements(VisioNamespace + "Section"))
        {
            if (!string.Equals(section.Attribute("N")?.Value, "Character", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var row in section.Elements(VisioNamespace + "Row"))
            {
                var index = row.Attribute("IX")?.Value;
                var font = row.Elements(VisioNamespace + "Cell")
                    .FirstOrDefault(cell => string.Equals(cell.Attribute("N")?.Value, "Font", StringComparison.Ordinal))
                    ?.Attribute("V")?.Value;
                if (index is not null && !string.IsNullOrEmpty(font))
                {
                    fonts[index] = font;
                }
            }
        }

        return fonts;
    }

    /// <summary>
    ///     Resolves the font name in force for a run marker.
    /// </summary>
    /// <param name="fontsByRow">The shape's row-index-to-font map.</param>
    /// <param name="rowIndex">The active run marker's row index, or <see langword="null"/> when none is in force.</param>
    /// <returns>The font name, or <see langword="null"/> when the drawing states none for that run.</returns>
    /// <remarks>Returning <see langword="null"/> leaves the run's text untouched, which is the safe default. Pure.</remarks>
    private static string? FontFor(Dictionary<string, string> fontsByRow, string? rowIndex) =>
        rowIndex is not null && fontsByRow.TryGetValue(rowIndex, out var font) ? font : null;

    /// <summary>
    ///     Appends a run's text, substituting the Unicode equivalent of any symbol-font code point
    ///     the reader has evidence for.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="text">The run's decoded text.</param>
    /// <param name="font">The font named for the run, or <see langword="null"/> when the drawing names none.</param>
    /// <remarks>
    ///     A run in an ordinary or unstated font is appended verbatim, so the mapping can never reach
    ///     text that merely happens to contain the same character. Side effect: appends to
    ///     <paramref name="builder"/>.
    /// </remarks>
    private static void AppendDecoded(System.Text.StringBuilder builder, string text, string? font)
    {
        // Without a named font there is no evidence of a symbol encoding, so the text stands as read
        if (font is null || !SymbolFontMappings.TryGetValue(font, out var mapping))
        {
            builder.Append(text);
            return;
        }

        foreach (var character in text)
        {
            builder.Append(mapping.TryGetValue(character, out var replacement) ? replacement : character);
        }
    }

    /// <summary>
    ///     Resolves a page's connector records into directed edges between shapes.
    /// </summary>
    /// <param name="contentsXml">The page-contents XML.</param>
    /// <param name="shapes">The page's shapes, used to keep only edges whose endpoints are real shapes.</param>
    /// <returns>The directed connections, source shape to target shape.</returns>
    /// <remarks>
    ///     Each connector shape contributes two <c>&lt;Connect&gt;</c> records sharing a
    ///     <c>FromSheet</c> (the connector's id): the one whose <c>FromCell</c> begins with
    ///     <c>Begin</c> names the source shape in its <c>ToSheet</c>, and the one whose
    ///     <c>FromCell</c> begins with <c>End</c> names the target. Pairing them yields the directed
    ///     edge. A connector missing either end, or naming a shape that is not on the page, is dropped
    ///     from the topology. Pure.
    /// </remarks>
    private static IReadOnlyList<VisioConnectionModel> ResolveConnections(
        XDocument contentsXml, IReadOnlyList<VisioShapeModel> shapes)
    {
        var shapeIds = new HashSet<string>(shapes.Select(shape => shape.Id), StringComparer.Ordinal);
        var begins = new Dictionary<string, string>(StringComparer.Ordinal);
        var ends = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var connectors = new List<string>();

        foreach (var connect in contentsXml.Descendants(VisioNamespace + "Connect"))
        {
            var connectorId = connect.Attribute("FromSheet")?.Value;
            var endpointId = connect.Attribute("ToSheet")?.Value;
            var cell = connect.Attribute("FromCell")?.Value;
            if (string.IsNullOrEmpty(connectorId) || string.IsNullOrEmpty(endpointId) || string.IsNullOrEmpty(cell))
            {
                continue;
            }

            var isBegin = cell.StartsWith("Begin", StringComparison.OrdinalIgnoreCase);
            var isEnd = cell.StartsWith("End", StringComparison.OrdinalIgnoreCase);
            if (!isBegin && !isEnd)
            {
                continue;
            }

            // Record each connector sheet exactly once in document order, regardless of whether
            // its Begin or End <Connect> record appears first. A single membership guard here is
            // essential: guarding the two branches with separate dictionaries would add the same
            // connector twice when its End record precedes its Begin record, inflating the topology
            // with a duplicate edge per such connector.
            if (seen.Add(connectorId))
            {
                connectors.Add(connectorId);
            }

            if (isBegin)
            {
                begins[connectorId] = endpointId;
            }
            else
            {
                ends[connectorId] = endpointId;
            }
        }

        var connections = new List<VisioConnectionModel>();
        foreach (var connectorId in connectors)
        {
            if (begins.TryGetValue(connectorId, out var from)
                && ends.TryGetValue(connectorId, out var to)
                && shapeIds.Contains(from)
                && shapeIds.Contains(to))
            {
                connections.Add(new VisioConnectionModel(from, to));
            }
        }

        return connections;
    }

    /// <summary>
    ///     Resolves a relationship id on a part to the target part it names.
    /// </summary>
    /// <param name="package">The opened package.</param>
    /// <param name="part">The part whose relationships are consulted.</param>
    /// <param name="relationshipId">The relationship id to resolve.</param>
    /// <returns>The target part, or <see langword="null"/> when the relationship or part is absent.</returns>
    private static PackagePart? ResolvePart(Package package, PackagePart part, string relationshipId)
    {
        if (!part.RelationshipExists(relationshipId))
        {
            return null;
        }

        var relationship = part.GetRelationship(relationshipId);
        var target = PackUriHelper.ResolvePartUri(part.Uri, relationship.TargetUri);
        return package.PartExists(target) ? package.GetPart(target) : null;
    }

    /// <summary>
    ///     Loads a package part's XML into an <see cref="XDocument"/>.
    /// </summary>
    /// <param name="part">The part to load.</param>
    /// <returns>The parsed XML document.</returns>
    /// <exception cref="VisioExtractionException">Thrown when the part is not well-formed XML.</exception>
    private static XDocument LoadXml(PackagePart part)
    {
        try
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            return XDocument.Load(stream);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new VisioExtractionException(
                $"A part of the drawing ('{part.Uri}') is not well-formed XML and could not be read.", exception);
        }
    }
}
