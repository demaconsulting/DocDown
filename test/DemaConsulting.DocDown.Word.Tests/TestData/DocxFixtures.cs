using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DemaConsulting.DocDown.Word.Tests.TestData;

/// <summary>
///     Builds every Word document the test suite needs, at test time, from the Open XML SDK.
/// </summary>
/// <remarks>
///     <para>
///         No binary <c>.docx</c> is committed to this repository. Each fixture is synthesized here
///         when the test that needs it runs, which keeps the repository text-only, removes any
///         question about the provenance of a checked-in sample document, and makes the exact content
///         of every fixture readable in the same place it is asserted against.
///     </para>
///     <para>
///         The one raster payload is held as a base64 constant for the same reason, and is exposed
///         through <see cref="PngBytes"/> so a byte-identity assertion can compare an extracted file
///         against the true source. This helper lives in the Word test project so the Open XML SDK
///         never becomes a dependency of the Core tests. All members are static and pure apart from
///         their allocations.
///     </para>
/// </remarks>
public static class DocxFixtures
{
    /// <summary>An 8x8 PNG embedded verbatim as an image part.</summary>
    /// <remarks>Held as base64 so the repository stays text-only while the bytes remain exact.</remarks>
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJ"
        + "cEhZcwAADsMAAA7DAcdvqGQAAACFSURBVChTFcoxEQBBCMDAU4ISlKCEMipQghIM5ee33vcexsN8WA/74Tzch/"
        + "fwvcAIzMAK7MAJ3MCLPyRGYiZWYidO4iZe/qEwCrOwCrtwCrfw6g+N0ZiN1diN07iN138YjMEcrMEenMEdvPnD"
        + "YizmYi324izu4u0fDuMwD+uwD+dwD+/wA4oilEGmIAUwAAAAAElFTkSuQmCC";

    /// <summary>
    ///     Gets the exact bytes of the PNG the embedded-image fixtures use.
    /// </summary>
    /// <returns>A fresh copy of the source PNG bytes.</returns>
    /// <remarks>Returns a copy so a test cannot mutate the shared fixture.</remarks>
    public static byte[] PngBytes() => Convert.FromBase64String(PngBase64);

    /// <summary>
    ///     Gets the bytes used for the vector-metafile (EMF) fixture.
    /// </summary>
    /// <returns>A short placeholder byte sequence.</returns>
    /// <remarks>
    ///     The extractor writes an EMF part's bytes unchanged and describes them by their media type,
    ///     never decoding them, so the exact content is immaterial to the behavior under test.
    /// </remarks>
    public static byte[] EmfBytes() => [0x01, 0x00, 0x00, 0x00, 0x45, 0x4D, 0x46, 0x20];

    /// <summary>
    ///     Builds the OLE compound-file header of a password-protected document.
    /// </summary>
    /// <returns>The bytes of a document that begins with the OLE compound-file signature.</returns>
    /// <remarks>A password-protected <c>.docx</c> is wrapped in an OLE compound file; only the leading signature matters here.</remarks>
    public static byte[] PasswordProtected() =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    ///     Builds arbitrary bytes for a legacy <c>.doc</c> fixture whose content is never read.
    /// </summary>
    /// <returns>Placeholder bytes.</returns>
    /// <remarks>Used where selection fails before any extractor opens the document.</remarks>
    public static byte[] LegacyDocBytes() => "This is a placeholder for a legacy binary Word document."u8.ToArray();

    /// <summary>
    ///     Builds a clean document exercising the full contract: title, heading, paragraph, a bulleted
    ///     list, a real table with a header row, an embedded image, and a header carrying a revision.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Produces no gaps, so it is the contract-layout and end-to-end fixture.</remarks>
    public static byte[] CleanDocument() => BuildDocx((document, mainPart) =>
    {
        AddNumbering(mainPart);
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Quarterly Report", "Title"));
        body.AppendChild(StyledParagraph("Overview", "Heading1"));
        body.AppendChild(TextParagraph("The quick brown fox summary paragraph."));
        body.AppendChild(ListItem("First point", numberingId: 1, level: 0));
        body.AppendChild(ListItem("Second point", numberingId: 1, level: 0));
        body.AppendChild(TableWithHeader());
        body.AppendChild(ImageParagraph(mainPart, "diagram"));
        body.AppendChild(SectionWithHeader(mainPart, "Document Number: DOC-001 Revision 3.0"));
        SetBody(mainPart, body);
        SetProperties(document, "Quarterly Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document embedding a single PNG image.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove images are written and linked as passthroughs.</remarks>
    public static byte[] DocumentWithImage() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Illustrated", "Heading1"));
        body.AppendChild(ImageParagraph(mainPart, "figure-1"));
        SetBody(mainPart, body);
        SetProperties(document, "Illustrated Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document embedding one DrawingML chart, anchored inline as a spreadsheet
    ///     application writes it.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     Used to prove the Word backend reports an embedded chart in a plain note rather than
    ///     dropping it without a word: a chart carries no image blip, so nothing else in the backend
    ///     would ever notice it. The chart's content is invented and belongs to no real document.
    /// </remarks>
    public static byte[] DocumentWithChart() => BuildDocx((document, mainPart) =>
    {
        var chartPart = mainPart.AddNewPart<ChartPart>();
        using (var stream = chartPart.GetStream(FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(MinimalChartXml);
        }

        var body = new W.Body();
        body.AppendChild(StyledParagraph("Test results", "Heading1"));
        body.AppendChild(ChartParagraph(mainPart.GetIdOfPart(chartPart)));
        SetBody(mainPart, body);
        SetProperties(document, "Charted Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     A minimal chart part body: one series of two invented points.
    /// </summary>
    /// <remarks>Invented content only, so no confidential material can reach this repository.</remarks>
    private const string MinimalChartXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">"
        + "<c:chart><c:plotArea><c:lineChart><c:ser><c:val><c:numLit><c:ptCount val=\"2\"/>"
        + "<c:pt idx=\"0\"><c:v>3</c:v></c:pt><c:pt idx=\"1\"><c:v>7</c:v></c:pt>"
        + "</c:numLit></c:val></c:ser></c:lineChart></c:plotArea></c:chart></c:chartSpace>";

    /// <summary>
    ///     Builds a paragraph anchoring a chart inline through a graphic frame.
    /// </summary>
    /// <param name="relationshipId">The relationship id of the chart part.</param>
    /// <returns>The paragraph.</returns>
    /// <remarks>The graphic data carries a chart reference and no blip, exactly as a real chart anchor does.</remarks>
    private static W.Paragraph ChartParagraph(string relationshipId)
    {
        var graphicData = new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };
        graphicData.AppendChild(new DocumentFormat.OpenXml.Drawing.Charts.ChartReference { Id = relationshipId });

        var inline = new WP.Inline(
            new WP.Extent { Cx = 100L, Cy = 100L },
            new WP.DocProperties { Id = 3U, Name = "Chart 1" },
            Compose(new A.Graphic(), graphicData));

        var run = new W.Run();
        run.AppendChild(Compose(new W.Drawing(), inline));
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(run);
        return paragraph;
    }

    /// <summary>
    ///     Builds a document embedding an EMF vector metafile image.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove EMF images are written as-is with a readability caveat.</remarks>
    public static byte[] DocumentWithVectorImage() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Diagram", "Heading1"));
        body.AppendChild(VectorImageParagraph(mainPart, "schematic"));
        SetBody(mainPart, body);
        SetProperties(document, "Diagram Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document embedding one PNG image whose text sources are configured individually,
    ///     for exercising the image-text selection policy source by source.
    /// </summary>
    /// <param name="description">The <c>wp:docPr/@descr</c> value, or <see langword="null"/> to omit it.</param>
    /// <param name="title">The <c>wp:docPr/@title</c> value, or <see langword="null"/> to omit it.</param>
    /// <param name="pictureName">The <c>pic:cNvPr/@name</c> value, or <see langword="null"/> to omit the object properties.</param>
    /// <param name="withCaption">Whether to follow the image with a <c>Caption</c>-styled paragraph carrying a <c>SEQ</c> field.</param>
    /// <param name="heading">A preceding <c>Heading 1</c> paragraph's text, or <see langword="null"/> to omit it.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     The <c>wp:docPr/@name</c> is deliberately set to the auto-generated placeholder
    ///     <c>Picture 1</c> so a test proves the policy never reads that attribute. Side effect: adds
    ///     an image part to the main document part.
    /// </remarks>
    public static byte[] DocumentWithConfiguredImage(
        string? description = null,
        string? title = null,
        string? pictureName = null,
        bool withCaption = false,
        string? heading = null) => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        if (heading is not null)
        {
            body.AppendChild(StyledParagraph(heading, "Heading1"));
        }

        var relId = AddImagePart(mainPart, PngBytes(), ImagePartType.Png);
        var imageParagraph = new W.Paragraph();
        imageParagraph.AppendChild(ConfiguredDrawingRun(relId, description, title, pictureName));
        body.AppendChild(imageParagraph);

        if (withCaption)
        {
            body.AppendChild(CaptionParagraphWithSeq());
        }

        SetBody(mainPart, body);
        SetProperties(document, "Configured Image", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document containing a table with merged (grid-span) cells.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove merged cells are flattened and counted.</remarks>
    public static byte[] DocumentWithMergedCells() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Merged", "Heading1"));

        var table = new W.Table();
        var headerRow = Compose(new W.TableRow(),
            Compose(new W.TableRowProperties(), new W.TableHeader()));
        headerRow.AppendChild(Cell("Name"));
        headerRow.AppendChild(Cell("Value"));
        table.AppendChild(headerRow);

        var spanRow = new W.TableRow();
        var spannedCell = Cell("Spans two columns");
        var cellProps = Compose(new W.TableCellProperties(), new W.GridSpan { Val = 2 });
        spannedCell.InsertAt(cellProps, 0);
        spanRow.AppendChild(spannedCell);
        table.AppendChild(spanRow);

        body.AppendChild(table);
        SetBody(mainPart, body);
        SetProperties(document, "Merged Cells Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a header carrying a revision and a footer carrying only a page number.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove document control survives while page furniture is omitted silently.</remarks>
    public static byte[] EngineeringStyleDocument() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Control Board Resources", "Title"));
        body.AppendChild(TextParagraph("Body content for the engineering document."));

        var headerId = AddHeader(mainPart, TextParagraph("Document DOC-42 Revision 2.1 CONFIDENTIAL"));
        var footerId = AddFooter(mainPart, PageNumberParagraph());
        body.AppendChild(SectionProperties(headerId, footerId));

        SetBody(mainPart, body);
        SetProperties(document, "Control Board Resources", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose only header carries nothing but a page-number field.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove a pure-furniture header is omitted and the document-control section is not emitted.</remarks>
    public static byte[] DocumentWithPageNumberFooterOnly() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Report", "Title"));
        body.AppendChild(TextParagraph("Body content."));
        var footerId = AddFooter(mainPart, PageNumberParagraph());
        body.AppendChild(SectionProperties(null, footerId));
        SetBody(mainPart, body);
        SetProperties(document, "Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a two-section document that references the identical header from both sections.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove an identical header across sections is emitted once.</remarks>
    public static byte[] DocumentWithIdenticalHeaderAcrossSections() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        var headerId = AddHeader(mainPart, TextParagraph("Shared Revision A.1"));

        var firstParagraph = TextParagraph("First section body.");
        firstParagraph.InsertAt(Compose(new W.ParagraphProperties(), SectionProperties(headerId, null)), 0);
        body.AppendChild(firstParagraph);

        body.AppendChild(TextParagraph("Second section body."));
        body.AppendChild(SectionProperties(headerId, null));

        SetBody(mainPart, body);
        SetProperties(document, "Two Section Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose header contains a logo image identical to a body image.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove a header logo is deduplicated against the same image in the body.</remarks>
    public static byte[] DocumentWithHeaderLogo() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("With Logo", "Title"));
        body.AppendChild(ImageParagraph(mainPart, "body-logo"));

        var headerPart = mainPart.AddNewPart<HeaderPart>();
        var header = new W.Header();
        header.AppendChild(TextParagraph("Header text"));
        header.AppendChild(HeaderImageParagraph(headerPart, "header-logo"));
        headerPart.Header = header;
        var headerId = mainPart.GetIdOfPart(headerPart);

        body.AppendChild(SectionProperties(headerId, null));
        SetBody(mainPart, body);
        SetProperties(document, "Logo Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with tracked insertions and deletions.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove the accepted view (insertions kept, deletions dropped) and the revision count.</remarks>
    public static byte[] DocumentWithTrackedChanges() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(Run("Kept "));

        var inserted = new W.InsertedRun { Author = "Editor", Id = "1" };
        inserted.AppendChild(Run("inserted-text"));
        paragraph.AppendChild(inserted);

        var deleted = new W.DeletedRun { Author = "Editor", Id = "2" };
        var deletedRun = new W.Run();
        deletedRun.AppendChild(new W.DeletedText("removed-text"));
        deleted.AppendChild(deletedRun);
        paragraph.AppendChild(deleted);

        body.AppendChild(paragraph);
        SetBody(mainPart, body);
        SetProperties(document, "Tracked Changes Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document carrying a single author-attributed comment.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove comments are rendered into a comments section with the author.</remarks>
    public static byte[] DocumentWithComment() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(TextParagraph("Body with a comment anchor."));
        SetBody(mainPart, body);

        var commentsPart = mainPart.AddNewPart<WordprocessingCommentsPart>();
        var comments = new W.Comments();
        var comment = new W.Comment { Id = "1", Author = "Reviewer" };
        comment.AppendChild(TextParagraph("Please clarify this section."));
        comments.AppendChild(comment);
        commentsPart.Comments = comments;

        SetProperties(document, "Commented Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with two top-level headings for per-part splitting.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove per-part mode splits at every Heading 1.</remarks>
    public static byte[] DocumentWithTwoSections() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Introduction", "Heading1"));
        body.AppendChild(TextParagraph("Introduction body."));
        body.AppendChild(StyledParagraph("Details", "Heading1"));
        body.AppendChild(TextParagraph("Details body."));
        SetBody(mainPart, body);
        SetProperties(document, "Two Part Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a title-only document with an empty body.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove an empty document degrades with an honest no-text gap while staying self-identifying.</remarks>
    public static byte[] EmptyDocument() => BuildDocx((document, mainPart) =>
    {
        SetBody(mainPart, new W.Body());
        SetProperties(document, "Empty Report", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document declaring a producer page count in its extended properties.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>Used to prove the producer-reported page count is surfaced and never computed.</remarks>
    public static byte[] DocumentWithPageCount() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(TextParagraph("Body content."));
        SetBody(mainPart, body);
        SetProperties(document, "Paged Report", "DocDown Test Suite");

        var appPart = document.AddExtendedFilePropertiesPart();
        var properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties();
        properties.AppendChild(new DocumentFormat.OpenXml.ExtendedProperties.Pages("7"));
        appPart.Properties = properties;
    });

    /// <summary>
    ///     Builds a table element with a marked header row and two body rows.
    /// </summary>
    /// <returns>The table element.</returns>
    /// <remarks>Header-marked so no assumed-header diagnostic is raised. Pure.</remarks>
    private static W.Table TableWithHeader()
    {
        var table = new W.Table();
        var header = Compose(new W.TableRow(), Compose(new W.TableRowProperties(), new W.TableHeader()));
        header.AppendChild(Cell("Component"));
        header.AppendChild(Cell("Status"));
        table.AppendChild(header);

        var row1 = new W.TableRow();
        row1.AppendChild(Cell("Alpha"));
        row1.AppendChild(Cell("Ready"));
        table.AppendChild(row1);

        var row2 = new W.TableRow();
        row2.AppendChild(Cell("Beta"));
        row2.AppendChild(Cell("Pending"));
        table.AppendChild(row2);

        return table;
    }

    /// <summary>
    ///     Builds a table cell holding a single paragraph of text.
    /// </summary>
    /// <param name="text">The cell text.</param>
    /// <returns>The cell element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.TableCell Cell(string text)
    {
        var cell = new W.TableCell();
        cell.AppendChild(TextParagraph(text));
        return cell;
    }

    /// <summary>
    ///     Builds a paragraph carrying a paragraph style id.
    /// </summary>
    /// <param name="text">The paragraph text.</param>
    /// <param name="styleId">The style id (for example <c>Heading1</c> or <c>Title</c>).</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Paragraph StyledParagraph(string text, string styleId)
    {
        var properties = new W.ParagraphProperties();
        properties.AppendChild(new W.ParagraphStyleId { Val = styleId });
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(properties);
        paragraph.AppendChild(Run(text));
        return paragraph;
    }

    /// <summary>
    ///     Builds a plain text paragraph.
    /// </summary>
    /// <param name="text">The paragraph text.</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Paragraph TextParagraph(string text)
    {
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(Run(text));
        return paragraph;
    }

    /// <summary>
    ///     Builds a numbered or bulleted list item paragraph.
    /// </summary>
    /// <param name="text">The item text.</param>
    /// <param name="numberingId">The numbering id referencing the numbering definitions.</param>
    /// <param name="level">The list nesting level.</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Paragraph ListItem(string text, int numberingId, int level)
    {
        var numbering = new W.NumberingProperties();
        numbering.AppendChild(new W.NumberingLevelReference { Val = level });
        numbering.AppendChild(new W.NumberingId { Val = numberingId });
        var properties = new W.ParagraphProperties();
        properties.AppendChild(numbering);
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(properties);
        paragraph.AppendChild(Run(text));
        return paragraph;
    }

    /// <summary>
    ///     Builds a run carrying a single run of preserved text.
    /// </summary>
    /// <param name="text">The run text.</param>
    /// <returns>The run element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Run Run(string text)
    {
        var run = new W.Run();
        run.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>
    ///     Builds a paragraph carrying a page-number field, which is document furniture.
    /// </summary>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Uses a simple field with the <c>PAGE</c> instruction. Pure.</remarks>
    private static W.Paragraph PageNumberParagraph()
    {
        var field = new W.SimpleField { Instruction = " PAGE " };
        field.AppendChild(Run("1"));
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(field);
        return paragraph;
    }

    /// <summary>
    ///     Builds a paragraph embedding a PNG image referenced from the main part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="name">The drawing name.</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Side effect: adds an image part to <paramref name="mainPart"/>.</remarks>
    private static W.Paragraph ImageParagraph(MainDocumentPart mainPart, string name)
    {
        var relId = AddImagePart(mainPart, PngBytes(), ImagePartType.Png);
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(BuildDrawingRun(relId, name));
        return paragraph;
    }

    /// <summary>
    ///     Builds a paragraph embedding an EMF image referenced from the main part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="name">The drawing name.</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Side effect: adds an EMF image part to <paramref name="mainPart"/>.</remarks>
    private static W.Paragraph VectorImageParagraph(MainDocumentPart mainPart, string name)
    {
        var relId = AddImagePart(mainPart, EmfBytes(), ImagePartType.Emf);
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(BuildDrawingRun(relId, name));
        return paragraph;
    }

    /// <summary>
    ///     Builds a paragraph embedding a PNG image referenced from a header part.
    /// </summary>
    /// <param name="headerPart">The header part.</param>
    /// <param name="name">The drawing name.</param>
    /// <returns>The paragraph element.</returns>
    /// <remarks>Side effect: adds an image part to <paramref name="headerPart"/>.</remarks>
    private static W.Paragraph HeaderImageParagraph(HeaderPart headerPart, string name)
    {
        var imagePart = headerPart.AddImagePart(ImagePartType.Png);
        WritePart(imagePart, PngBytes());
        var relId = headerPart.GetIdOfPart(imagePart);
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(BuildDrawingRun(relId, name));
        return paragraph;
    }

    /// <summary>
    ///     Adds an image part to the main document part and returns its relationship id.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="type">The image part type.</param>
    /// <returns>The relationship id of the added part.</returns>
    /// <remarks>Side effect: adds a part to <paramref name="mainPart"/>.</remarks>
    private static string AddImagePart(MainDocumentPart mainPart, byte[] bytes, PartTypeInfo type)
    {
        var imagePart = mainPart.AddImagePart(type);
        WritePart(imagePart, bytes);
        return mainPart.GetIdOfPart(imagePart);
    }

    /// <summary>
    ///     Writes bytes into an image part's stream.
    /// </summary>
    /// <param name="imagePart">The image part.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <remarks>Side effect: writes the part content.</remarks>
    private static void WritePart(ImagePart imagePart, byte[] bytes)
    {
        using var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    ///     Builds a run containing an inline drawing that references an image by relationship id.
    /// </summary>
    /// <param name="relId">The image relationship id.</param>
    /// <param name="name">The drawing name.</param>
    /// <returns>The run element.</returns>
    /// <remarks>
    ///     Carries the minimum a reader needs — a <c>wp:docPr</c> name and description plus an
    ///     <c>a:blip</c> embed — so the reader resolves it and selects the authored description as the
    ///     image's naming and alt-text source without a full, schema-complete picture shape. Pure.
    /// </remarks>
    private static W.Run BuildDrawingRun(string relId, string name)
    {
        var blip = new A.Blip { Embed = relId };
        var blipFill = new PIC.BlipFill();
        blipFill.AppendChild(blip);
        var picture = new PIC.Picture();
        picture.AppendChild(blipFill);
        var graphicData = new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        graphicData.AppendChild(picture);
        var graphic = new A.Graphic();
        graphic.AppendChild(graphicData);

        var inline = new WP.Inline();
        inline.AppendChild(new WP.Extent { Cx = 100000L, Cy = 100000L });
        inline.AppendChild(new WP.DocProperties { Id = 1U, Name = name, Description = name });
        inline.AppendChild(graphic);

        var drawing = new W.Drawing();
        drawing.AppendChild(inline);
        var run = new W.Run();
        run.AppendChild(drawing);
        return run;
    }

    /// <summary>
    ///     Builds a drawing run whose image-text sources are configured individually.
    /// </summary>
    /// <param name="relId">The image relationship id.</param>
    /// <param name="description">The <c>wp:docPr/@descr</c> value, or <see langword="null"/> to omit it.</param>
    /// <param name="title">The <c>wp:docPr/@title</c> value, or <see langword="null"/> to omit it.</param>
    /// <param name="pictureName">The <c>pic:cNvPr/@name</c> value, or <see langword="null"/> to omit the object properties.</param>
    /// <returns>The run element.</returns>
    /// <remarks>
    ///     Sets <c>wp:docPr/@name</c> to the auto-generated placeholder <c>Picture 1</c> so a test can
    ///     prove the policy ignores that attribute, and attaches <c>pic:cNvPr</c> only when an object
    ///     name is requested so the object-name source can be exercised in isolation. Pure.
    /// </remarks>
    private static W.Run ConfiguredDrawingRun(string relId, string? description, string? title, string? pictureName)
    {
        var blip = new A.Blip { Embed = relId };
        var blipFill = new PIC.BlipFill();
        blipFill.AppendChild(blip);
        var picture = new PIC.Picture();
        if (pictureName is not null)
        {
            picture.AppendChild(new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0U, Name = pictureName },
                new PIC.NonVisualPictureDrawingProperties()));
        }

        picture.AppendChild(blipFill);
        var graphicData = new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        graphicData.AppendChild(picture);
        var graphic = new A.Graphic();
        graphic.AppendChild(graphicData);

        var docProperties = new WP.DocProperties { Id = 1U, Name = "Picture 1" };
        if (description is not null)
        {
            docProperties.Description = description;
        }

        if (title is not null)
        {
            docProperties.Title = title;
        }

        var inline = new WP.Inline();
        inline.AppendChild(new WP.Extent { Cx = 100000L, Cy = 100000L });
        inline.AppendChild(docProperties);
        inline.AppendChild(graphic);

        var drawing = new W.Drawing();
        drawing.AppendChild(inline);
        var run = new W.Run();
        run.AppendChild(drawing);
        return run;
    }

    /// <summary>
    ///     Builds a <c>Caption</c>-styled paragraph whose figure number comes from a <c>SEQ</c> field.
    /// </summary>
    /// <returns>The caption paragraph element.</returns>
    /// <remarks>
    ///     The number is carried as a complex-field <em>result</em> (between the field separator and
    ///     end), not as literal digits, so a reader that reads the number structurally sees
    ///     <c>Figure 1: Widget assembly</c> while a reader that pattern-matched digits would be
    ///     testing the wrong thing. Pure.
    /// </remarks>
    private static W.Paragraph CaptionParagraphWithSeq()
    {
        var properties = new W.ParagraphProperties();
        properties.AppendChild(new W.ParagraphStyleId { Val = "Caption" });
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(properties);
        paragraph.AppendChild(Run("Figure "));
        paragraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
        paragraph.AppendChild(FieldRun(new W.FieldCode(" SEQ Figure \\* ARABIC ")
        { Space = SpaceProcessingModeValues.Preserve }));
        paragraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        paragraph.AppendChild(Run("1"));
        paragraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        paragraph.AppendChild(Run(": Widget assembly"));
        return paragraph;
    }

    /// <summary>
    ///     Wraps a single field-related child in a run.
    /// </summary>
    /// <param name="child">The field character or field code to carry.</param>
    /// <returns>The run element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Run FieldRun(OpenXmlElement child)
    {
        var run = new W.Run();
        run.AppendChild(child);
        return run;
    }

    /// <summary>
    ///     Builds a section-properties element with optional header and footer references.
    /// </summary>
    /// <param name="headerId">The header relationship id, or <see langword="null"/>.</param>
    /// <param name="footerId">The footer relationship id, or <see langword="null"/>.</param>
    /// <returns>The section-properties element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.SectionProperties SectionProperties(string? headerId, string? footerId)
    {
        var section = new W.SectionProperties();
        if (headerId is not null)
        {
            section.AppendChild(new W.HeaderReference { Id = headerId, Type = W.HeaderFooterValues.Default });
        }

        if (footerId is not null)
        {
            section.AppendChild(new W.FooterReference { Id = footerId, Type = W.HeaderFooterValues.Default });
        }

        return section;
    }

    /// <summary>
    ///     Builds a paragraph followed by a single-section body with a header, returning the paragraph
    ///     whose properties carry the section reference.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="headerText">The header text.</param>
    /// <returns>The section-properties element referencing the created header.</returns>
    /// <remarks>Side effect: adds a header part. Pure otherwise.</remarks>
    private static W.SectionProperties SectionWithHeader(MainDocumentPart mainPart, string headerText)
    {
        var headerId = AddHeader(mainPart, TextParagraph(headerText));
        return SectionProperties(headerId, null);
    }

    /// <summary>
    ///     Adds a header part with the given content and returns its relationship id.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="content">The header content paragraph.</param>
    /// <returns>The relationship id of the added header part.</returns>
    /// <remarks>Side effect: adds a part to <paramref name="mainPart"/>.</remarks>
    private static string AddHeader(MainDocumentPart mainPart, W.Paragraph content)
    {
        var headerPart = mainPart.AddNewPart<HeaderPart>();
        var header = new W.Header();
        header.AppendChild(content);
        headerPart.Header = header;
        return mainPart.GetIdOfPart(headerPart);
    }

    /// <summary>
    ///     Adds a footer part with the given content and returns its relationship id.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="content">The footer content paragraph.</param>
    /// <returns>The relationship id of the added footer part.</returns>
    /// <remarks>Side effect: adds a part to <paramref name="mainPart"/>.</remarks>
    private static string AddFooter(MainDocumentPart mainPart, W.Paragraph content)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new W.Footer();
        footer.AppendChild(content);
        footerPart.Footer = footer;
        return mainPart.GetIdOfPart(footerPart);
    }

    /// <summary>
    ///     Adds a numbering definitions part with a bulleted list (id 1) and an ordered list (id 2).
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <remarks>Side effect: adds the numbering part to <paramref name="mainPart"/>.</remarks>
    private static void AddNumbering(MainDocumentPart mainPart)
    {
        var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new W.Numbering();

        numbering.AppendChild(AbstractNum(0, W.NumberFormatValues.Bullet));
        numbering.AppendChild(AbstractNum(1, W.NumberFormatValues.Decimal));
        numbering.AppendChild(NumInstance(1, 0));
        numbering.AppendChild(NumInstance(2, 1));

        part.Numbering = numbering;
    }

    /// <summary>
    ///     Builds an abstract numbering definition with a single level of the given format.
    /// </summary>
    /// <param name="abstractId">The abstract numbering id.</param>
    /// <param name="format">The numbering format for level zero.</param>
    /// <returns>The abstract numbering element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.AbstractNum AbstractNum(int abstractId, W.NumberFormatValues format)
    {
        var level = new W.Level { LevelIndex = 0 };
        level.AppendChild(new W.NumberingFormat { Val = format });
        var abstractNum = new W.AbstractNum { AbstractNumberId = abstractId };
        abstractNum.AppendChild(level);
        return abstractNum;
    }

    /// <summary>
    ///     Builds a numbering instance mapping a numbering id to an abstract numbering id.
    /// </summary>
    /// <param name="numberingId">The numbering id referenced by paragraphs.</param>
    /// <param name="abstractId">The abstract numbering id it maps to.</param>
    /// <returns>The numbering instance element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.NumberingInstance NumInstance(int numberingId, int abstractId)
    {
        var instance = new W.NumberingInstance { NumberID = numberingId };
        instance.AppendChild(new W.AbstractNumId { Val = abstractId });
        return instance;
    }

    /// <summary>
    ///     Sets the body on the main document part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="body">The body element.</param>
    /// <remarks>Side effect: assigns the document root.</remarks>
    private static void SetBody(MainDocumentPart mainPart, W.Body body)
    {
        var document = new W.Document();
        document.AppendChild(body);
        mainPart.Document = document;
    }

    /// <summary>
    ///     Sets the document title and author on the package properties.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="title">The title, or <see langword="null"/>.</param>
    /// <param name="author">The author, or <see langword="null"/>.</param>
    /// <remarks>Side effect: assigns package core properties.</remarks>
    private static void SetProperties(WordprocessingDocument document, string? title, string? author)
    {
        document.PackageProperties.Title = title;
        document.PackageProperties.Creator = author;
    }

    /// <summary>
    ///     Builds a document package into memory and returns its bytes.
    /// </summary>
    /// <param name="configure">The configuration applied to the document and its main part.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>Side effect: none beyond allocation; the package is built in a memory stream.</remarks>
    private static byte[] BuildDocx(Action<WordprocessingDocument, MainDocumentPart> configure)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            configure(document, mainPart);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Appends children to an element in order and returns it.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="element">The element to compose.</param>
    /// <param name="children">The children to append.</param>
    /// <returns>The composed element.</returns>
    /// <remarks>
    ///     Uses explicit appends rather than the SDK's multi-argument constructors, which have both a
    ///     <c>params</c> and an <c>IEnumerable</c> overload that an analyzer flags as an ambiguous
    ///     partial match. Pure apart from mutating <paramref name="element"/>.
    /// </remarks>
    private static T Compose<T>(T element, params OpenXmlElement[] children)
        where T : OpenXmlElement
    {
        foreach (var child in children)
        {
            element.AppendChild(child);
        }

        return element;
    }
}
