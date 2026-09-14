using System.Globalization;
using System.IO.Packaging;
using System.Text;
using System.Xml.Linq;

namespace DemaConsulting.DocDown.Office.Tests.Visio.TestData;

/// <summary>
///     Synthesizes a minimal but well-formed Visio Open Packaging drawing in memory so the test suite
///     can drive the reader through cases a single committed fixture cannot cover.
/// </summary>
/// <remarks>
///     The builder writes exactly the parts the reader reads — <c>visio/pages/pages.xml</c>, one
///     <c>visio/pages/pageN.xml</c> per page, and, when masters are declared,
///     <c>visio/masters/masters.xml</c> — plus the minimal document part and the package
///     relationships, letting <see cref="System.IO.Packaging"/> generate the content-types and
///     relationship parts. It is not a general Visio writer; it produces the narrow shape the reader
///     consumes. Internal so the test project can reuse it through <c>InternalsVisibleTo</c>. Pure
///     apart from its allocations.
/// </remarks>
internal static class VisioPackageBuilder
{
    /// <summary>The Visio 2012 main XML namespace every drawing part uses.</summary>
    private static readonly XNamespace VisioNamespace = "http://schemas.microsoft.com/office/visio/2012/main";

    /// <summary>The Open Packaging relationships namespace used for the <c>r:id</c> attribute.</summary>
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The content type of the main document part.</summary>
    private const string DocumentContentType = "application/vnd.ms-visio.drawing.main+xml";

    /// <summary>The content type of the pages-collection part.</summary>
    private const string PagesContentType = "application/vnd.ms-visio.pages+xml";

    /// <summary>The content type of a single page-contents part.</summary>
    private const string PageContentType = "application/vnd.ms-visio.page+xml";

    /// <summary>The content type of the masters-collection part.</summary>
    private const string MastersContentType = "application/vnd.ms-visio.masters+xml";

    /// <summary>The relationship type from the package to the main document part.</summary>
    private const string DocumentRelationship = "http://schemas.microsoft.com/visio/2010/relationships/document";

    /// <summary>The relationship type from the document to the pages-collection part.</summary>
    private const string PagesRelationship = "http://schemas.microsoft.com/visio/2010/relationships/pages";

    /// <summary>The relationship type from the pages collection to a page-contents part.</summary>
    private const string PageRelationship = "http://schemas.microsoft.com/visio/2010/relationships/page";

    /// <summary>The relationship type from the document to the masters-collection part.</summary>
    private const string MastersRelationship = "http://schemas.microsoft.com/visio/2010/relationships/masters";

    /// <summary>The Open Packaging relationship type of an embedded image.</summary>
    private const string ImageRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>The content type of an embedded EMF vector metafile media part.</summary>
    private const string EmfContentType = "image/x-emf";

    /// <summary>The eight-byte EMF magic prefix a synthesized metafile media part carries.</summary>
    private static readonly byte[] EmfBytes = [0x01, 0x00, 0x00, 0x00, 0x45, 0x4D, 0x46, 0x20];

    /// <summary>
    ///     Builds a Visio drawing package carrying the supplied pages.
    /// </summary>
    /// <param name="pages">The pages to write, each with its name, shapes, and directed edges.</param>
    /// <param name="masters">
    ///     The masters to declare in <c>visio/masters/masters.xml</c>, or <see langword="null"/> for
    ///     a drawing that declares no masters part at all — which a real drawing may legitimately do.
    /// </param>
    /// <returns>The bytes of a well-formed Visio Open Packaging drawing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pages"/> is <see langword="null"/>.</exception>
    public static byte[] Build(IReadOnlyList<VisioBuildPage> pages, IReadOnlyList<VisioBuildMaster>? masters = null)
    {
        ArgumentNullException.ThrowIfNull(pages);

        using var stream = new MemoryStream();
        using (var package = Package.Open(stream, FileMode.Create))
        {
            var documentPart = package.CreatePart(new Uri("/visio/document.xml", UriKind.Relative), DocumentContentType);
            WriteXml(documentPart, new XDocument(new XElement(VisioNamespace + "VisioDocument")));
            package.CreateRelationship(documentPart.Uri, TargetMode.Internal, DocumentRelationship, "rId1");

            // Write the masters collection only when the caller declares masters, so the no-masters
            // drawing the reader must tolerate can actually be built
            if (masters is { Count: > 0 })
            {
                WriteMasters(package, documentPart, masters);
            }

            var pagesPart = package.CreatePart(new Uri("/visio/pages/pages.xml", UriKind.Relative), PagesContentType);
            documentPart.CreateRelationship(pagesPart.Uri, TargetMode.Internal, PagesRelationship, "rId1");

            var pagesRoot = new XElement(
                VisioNamespace + "Pages",
                new XAttribute(XNamespace.Xmlns + "r", RelationshipNamespace.NamespaceName));

            var index = 1;
            foreach (var page in pages)
            {
                var pageUri = new Uri($"/visio/pages/page{index.ToString(CultureInfo.InvariantCulture)}.xml", UriKind.Relative);
                var pagePart = package.CreatePart(pageUri, PageContentType);
                WriteXml(pagePart, BuildPageContents(page));

                // A page that asks for an embedded vector image gets a real EMF media part under
                // visio/media, referenced from the page by an Open Packaging image relationship, so
                // the reader resolves it exactly as it would for a genuine ForeignData image
                if (page.EmbedVectorImage)
                {
                    var mediaUri = new Uri(
                        $"/visio/media/image{index.ToString(CultureInfo.InvariantCulture)}.emf", UriKind.Relative);
                    var mediaPart = package.CreatePart(mediaUri, EmfContentType);
                    WriteBytes(mediaPart, EmfBytes);
                    pagePart.CreateRelationship(mediaPart.Uri, TargetMode.Internal, ImageRelationshipType);
                }

                var relationship = pagesPart.CreateRelationship(pagePart.Uri, TargetMode.Internal, PageRelationship);
                pagesRoot.Add(new XElement(
                    VisioNamespace + "Page",
                    new XAttribute("ID", (index - 1).ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("Name", page.Name),
                    new XElement(VisioNamespace + "Rel", new XAttribute(RelationshipNamespace + "id", relationship.Id))));
                index++;
            }

            WriteXml(pagesPart, new XDocument(pagesRoot));
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Writes the masters collection and relates it to the document part.
    /// </summary>
    /// <param name="package">The package being built.</param>
    /// <param name="documentPart">The main document part the masters collection hangs from.</param>
    /// <param name="masters">The masters to declare.</param>
    /// <remarks>
    ///     Only the attributes the reader consults are written — the master id and its
    ///     <c>Name</c>/<c>NameU</c> — because the builder's purpose is to exercise the reader, not to
    ///     reproduce a stencil. A master with a null name simply omits that attribute, so the
    ///     fallback from <c>Name</c> to <c>NameU</c> can be exercised as a real package shape rather
    ///     than a hand-built model.
    /// </remarks>
    private static void WriteMasters(Package package, PackagePart documentPart, IReadOnlyList<VisioBuildMaster> masters)
    {
        var mastersPart = package.CreatePart(
            new Uri("/visio/masters/masters.xml", UriKind.Relative), MastersContentType);
        documentPart.CreateRelationship(mastersPart.Uri, TargetMode.Internal, MastersRelationship, "rId2");

        var root = new XElement(
            VisioNamespace + "Masters",
            new XAttribute(XNamespace.Xmlns + "r", RelationshipNamespace.NamespaceName));
        foreach (var master in masters)
        {
            var element = new XElement(VisioNamespace + "Master", new XAttribute("ID", master.Id));
            if (master.NameU is not null)
            {
                element.Add(new XAttribute("NameU", master.NameU));
            }

            if (master.Name is not null)
            {
                element.Add(new XAttribute("Name", master.Name));
            }

            root.Add(element);
        }

        WriteXml(mastersPart, new XDocument(root));
    }

    /// <summary>
    ///     Builds the page-contents XML for a page: its shapes and the connector records that encode
    ///     its directed edges.
    /// </summary>
    /// <param name="page">The page to build.</param>
    /// <returns>The page-contents document.</returns>
    private static XDocument BuildPageContents(VisioBuildPage page)
    {
        var shapes = new XElement(VisioNamespace + "Shapes");
        foreach (var shape in page.Shapes)
        {
            var shapeElement = new XElement(
                VisioNamespace + "Shape",
                new XAttribute("ID", shape.Id));
            if (shape.MasterId is not null)
            {
                shapeElement.Add(new XAttribute("Master", shape.MasterId));
            }

            if (shape.Runs is { Count: > 0 })
            {
                shapeElement.Add(BuildCharacterSection(shape.Runs));
                shapeElement.Add(BuildRunText(shape.Runs));
            }
            else if (shape.Text is not null)
            {
                shapeElement.Add(new XElement(VisioNamespace + "Text", shape.Text));
            }

            shapes.Add(shapeElement);
        }

        var connects = new XElement(VisioNamespace + "Connects");
        var connectorId = 1000;
        foreach (var (from, to) in page.Edges)
        {
            var connector = connectorId.ToString(CultureInfo.InvariantCulture);
            if (page.EndRecordFirst)
            {
                // Emit the End record before the Begin record so a fixture can exercise the
                // End-before-Begin document ordering that a real drawing can produce.
                connects.Add(ConnectRecord(connector, to, "EndX"));
                connects.Add(ConnectRecord(connector, from, "BeginX"));
            }
            else
            {
                connects.Add(ConnectRecord(connector, from, "BeginX"));
                connects.Add(ConnectRecord(connector, to, "EndX"));
            }

            connectorId++;
        }

        return new XDocument(new XElement(
            VisioNamespace + "PageContents",
            new XAttribute(XNamespace.Xmlns + "r", RelationshipNamespace.NamespaceName),
            shapes,
            connects));
    }

    /// <summary>
    ///     Builds the <c>Character</c> section declaring one formatting row per text run.
    /// </summary>
    /// <param name="runs">The runs, whose positions become the row indexes the text markers select.</param>
    /// <returns>The <c>&lt;Section N="Character"&gt;</c> element.</returns>
    /// <remarks>
    ///     A row names a font only when the run declares one, mirroring a real drawing where an
    ///     unstyled run inherits from the theme. This is what lets a fixture reproduce the one piece
    ///     of evidence that distinguishes a symbol-font glyph from the Latin letter it decodes to.
    /// </remarks>
    private static XElement BuildCharacterSection(IReadOnlyList<VisioBuildTextRun> runs)
    {
        var section = new XElement(VisioNamespace + "Section", new XAttribute("N", "Character"));
        for (var index = 0; index < runs.Count; index++)
        {
            var row = new XElement(
                VisioNamespace + "Row",
                new XAttribute("IX", index.ToString(CultureInfo.InvariantCulture)));
            if (runs[index].Font is { } font)
            {
                row.Add(new XElement(VisioNamespace + "Cell", new XAttribute("N", "Font"), new XAttribute("V", font)));
            }

            section.Add(row);
        }

        return section;
    }

    /// <summary>
    ///     Builds the <c>Text</c> element as a run marker followed by its text, for each run.
    /// </summary>
    /// <param name="runs">The runs in document order.</param>
    /// <returns>The <c>&lt;Text&gt;</c> element.</returns>
    /// <remarks>Matches Visio's own shape: a <c>&lt;cp IX="n"/&gt;</c> marker selects the formatting for the text that follows it.</remarks>
    private static XElement BuildRunText(IReadOnlyList<VisioBuildTextRun> runs)
    {
        var text = new XElement(VisioNamespace + "Text");
        for (var index = 0; index < runs.Count; index++)
        {
            text.Add(new XElement(
                VisioNamespace + "cp",
                new XAttribute("IX", index.ToString(CultureInfo.InvariantCulture))));
            text.Add(new XText(runs[index].Text));
        }

        return text;
    }

    /// <summary>
    ///     Builds one connector record naming its connector, its endpoint shape, and which end it is.
    /// </summary>
    /// <param name="connectorId">The connector shape id (shared by the begin and end records).</param>
    /// <param name="endpointId">The endpoint shape id this record connects to.</param>
    /// <param name="cell">The <c>FromCell</c> value (<c>BeginX</c> or <c>EndX</c>) that carries the direction.</param>
    /// <returns>The <c>&lt;Connect&gt;</c> element.</returns>
    private static XElement ConnectRecord(string connectorId, string endpointId, string cell) =>
        new(VisioNamespace + "Connect",
            new XAttribute("FromSheet", connectorId),
            new XAttribute("ToSheet", endpointId),
            new XAttribute("FromCell", cell));

    /// <summary>
    ///     Writes an XML document into a package part in UTF-8.
    /// </summary>
    /// <param name="part">The part to write into.</param>
    /// <param name="document">The document to write.</param>
    private static void WriteXml(PackagePart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        document.Save(writer);
    }

    /// <summary>
    ///     Writes raw bytes into a freshly created package part.
    /// </summary>
    /// <param name="part">The part to write into.</param>
    /// <param name="bytes">The bytes to write.</param>
    private static void WriteBytes(PackagePart part, byte[] bytes)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        stream.Write(bytes, 0, bytes.Length);
    }
}

/// <summary>
///     A page to synthesize: its name, its shapes, and its directed edges.
/// </summary>
/// <param name="Name">The page name.</param>
/// <param name="Shapes">The page's shapes.</param>
/// <param name="Edges">The directed edges as source-shape-id to target-shape-id pairs.</param>
/// <param name="EndRecordFirst">
///     When <see langword="true"/>, each connector's <c>EndX</c> <c>&lt;Connect&gt;</c> record is
///     written before its <c>BeginX</c> record, so a fixture can reproduce the End-before-Begin
///     document ordering a real drawing can carry. Defaults to <see langword="false"/> (the
///     conventional Begin-then-End order), keeping every existing call site unchanged.
/// </param>
/// <param name="EmbedVectorImage">
///     When <see langword="true"/>, the page embeds one EMF vector metafile media part under
///     <c>visio/media</c>, referenced from the page by an Open Packaging image relationship, so a
///     fixture can exercise the vector-passthrough caveat end to end. Defaults to <see
///     langword="false"/>, keeping every existing call site unchanged.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioBuildPage(
    string Name,
    IReadOnlyList<VisioBuildShape> Shapes,
    IReadOnlyList<(string From, string To)> Edges,
    bool EndRecordFirst = false,
    bool EmbedVectorImage = false);

/// <summary>
///     A shape to synthesize: its id, its text, and the master it is instantiated from.
/// </summary>
/// <param name="Id">The shape id.</param>
/// <param name="Text">The shape text, or <see langword="null"/> for a text-less shape.</param>
/// <param name="MasterId">
///     The id of the master this shape is instantiated from, or <see langword="null"/> for a shape
///     that names no master. Defaulted so every existing call site stays unchanged.
/// </param>
/// <param name="Runs">
///     The formatted text runs to synthesize instead of a single unformatted <c>Text</c> element, or
///     <see langword="null"/> to emit <paramref name="Text"/> as one plain run. Supplying runs is the
///     only way to reproduce a symbol-font glyph, because the font lives in a <c>Character</c> row
///     that a run marker selects, not in the text itself.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioBuildShape(
    string Id, string? Text, string? MasterId = null, IReadOnlyList<VisioBuildTextRun>? Runs = null);

/// <summary>
///     One formatted text run within a shape's text: what it says and what font says it.
/// </summary>
/// <param name="Text">The run's text, exactly as the drawing stores it.</param>
/// <param name="Font">The font the run's <c>Character</c> row declares, or <see langword="null"/> to declare none.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioBuildTextRun(string Text, string? Font = null);

/// <summary>
///     A master to synthesize: its id and the names it declares.
/// </summary>
/// <param name="Id">The master id that a shape's <c>Master</c> attribute refers to.</param>
/// <param name="Name">The localized master name, or <see langword="null"/> to omit the attribute.</param>
/// <param name="NameU">The invariant master name, or <see langword="null"/> to omit the attribute.</param>
/// <remarks>
///     Both names are separately optional so a fixture can pin the reader's preference for
///     <c>Name</c>, its fallback to <c>NameU</c>, and its refusal to name a master that declares
///     neither. Immutable and thread-safe.
/// </remarks>
internal sealed record VisioBuildMaster(string Id, string? Name = null, string? NameU = null);
