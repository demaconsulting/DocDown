using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DemaConsulting.DocDown.Office.Tests.Word.TestData;

/// <summary>
///     Synthetic Word documents for the tracked-change location and table-of-contents tests.
/// </summary>
/// <remarks>
///     Every document is built in code from invented text; none is derived from a real document.
///     Shares the private helpers of the main fixture class.
/// </remarks>
public static partial class DocxFixtures
{
    /// <summary>
    ///     Builds a document with a table of four rows: a header row, a tracked-deleted row, a plain
    ///     row, and a plain row with a tracked insertion in its second cell.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove row numbers agree with the rendered table.</remarks>
    public static byte[] DocumentWithDeletedRowBeforeEdit() => BuildDocx((document, mainPart) =>
    {
        W.TableRow Row(string a, W.TableCell second, W.Deleted? tracked)
        {
            var row = new W.TableRow();
            if (tracked is not null)
            {
                var properties = new W.TableRowProperties();
                properties.AppendChild(tracked);
                row.AppendChild(properties);
            }

            row.AppendChild(Cell(a));
            row.AppendChild(second);
            return row;
        }

        var edited = new W.TableCell();
        var editedParagraph = new W.Paragraph();
        editedParagraph.AppendChild(Run("Ready "));
        editedParagraph.AppendChild(Revision<W.InsertedRun>("90", "Lena", "2026-09-01T00:00:00Z", Run("soon")));
        edited.AppendChild(editedParagraph);

        var deletion = new W.Deleted
        {
            Id = "91",
            Author = "Lena",
            Date = DateTime.Parse("2026-09-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)
        };

        var table = new W.Table();
        table.AppendChild(Row("Part", Cell("State"), null));
        table.AppendChild(Row("Widget", Cell("retired"), deletion));
        table.AppendChild(Row("Sprocket", Cell("stable"), null));
        table.AppendChild(Row("Gear", edited, null));
        var body = new W.Body();
        body.AppendChild(table);
        SetBody(mainPart, body);
        SetProperties(document, "Row Numbering", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a table whose only row is tracked-deleted, followed by a second table
    ///     with a tracked insertion in a cell.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. Used to prove the accepted view omits the first table and so numbers
    ///     the second as table 1.
    /// </remarks>
    public static byte[] DocumentWithFullyDeletedTableBeforeEditedTable() => BuildDocx((document, mainPart) =>
    {
        var deletedRow = new W.TableRow();
        var rowProperties = new W.TableRowProperties();
        rowProperties.AppendChild(new W.Deleted
        {
            Id = "95",
            Author = "Nia",
            Date = DateTime.Parse("2026-09-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)
        });
        deletedRow.AppendChild(rowProperties);
        deletedRow.AppendChild(Cell("Obsolete entry"));
        var first = new W.Table();
        first.AppendChild(deletedRow);

        var edited = new W.TableCell();
        var editedParagraph = new W.Paragraph();
        editedParagraph.AppendChild(Run("Kept "));
        editedParagraph.AppendChild(Revision<W.InsertedRun>("96", "Nia", "2026-09-06T00:00:00Z", Run("addition")));
        edited.AppendChild(editedParagraph);
        var secondRow = new W.TableRow();
        secondRow.AppendChild(edited);
        var second = new W.Table();
        second.AppendChild(secondRow);

        var body = new W.Body();
        body.AppendChild(first);
        body.AppendChild(TextParagraph("Separator text."));
        body.AppendChild(second);
        SetBody(mainPart, body);
        SetProperties(document, "Deleted Table", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a deleted paragraph mark directly before a table, followed by a
    ///     paragraph carrying a tracked insertion.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. Used to prove the carried paragraph stands alone before the table, as
    ///     the reader emits it, rather than merging with the paragraph after the table.
    /// </remarks>
    public static byte[] DocumentWithDeletedMarkBeforeTable() => BuildDocx((document, mainPart) =>
    {
        var row = new W.TableRow();
        row.AppendChild(Cell("Cell text"));
        var table = new W.Table();
        table.AppendChild(row);

        var after = new W.Paragraph();
        after.AppendChild(Run("After the table "));
        after.AppendChild(Revision<W.InsertedRun>("97", "Omar", "2026-09-07T00:00:00Z", Run("addition")));

        var body = new W.Body();
        body.AppendChild(ParagraphWithTrackedMark(true, null, Run("Lead-in words")));
        body.AppendChild(table);
        body.AppendChild(after);
        SetBody(mainPart, body);
        SetProperties(document, "Mark Before Table", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with an image-only table followed by a text table with a tracked insertion.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. Used to prove the image-only table, which the reader omits, does not
    ///     consume a table ordinal.
    /// </remarks>
    public static byte[] DocumentWithImageOnlyTableBeforeEditedTable() => BuildDocx((document, mainPart) =>
    {
        var imageCell = new W.TableCell();
        imageCell.AppendChild(ImageParagraph(mainPart, "figure-cell"));
        var imageRow = new W.TableRow();
        imageRow.AppendChild(imageCell);
        var first = new W.Table();
        first.AppendChild(imageRow);

        var edited = new W.TableCell();
        var editedParagraph = new W.Paragraph();
        editedParagraph.AppendChild(Run("Kept "));
        editedParagraph.AppendChild(Revision<W.InsertedRun>("98", "Omar", "2026-09-08T00:00:00Z", Run("addition")));
        edited.AppendChild(editedParagraph);
        var secondRow = new W.TableRow();
        secondRow.AppendChild(edited);
        var second = new W.Table();
        second.AppendChild(secondRow);

        var body = new W.Body();
        body.AppendChild(first);
        body.AppendChild(TextParagraph("Separator text."));
        body.AppendChild(second);
        SetBody(mainPart, body);
        SetProperties(document, "Image Table", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose footer has two paragraphs, the first with a tracked-deleted
    ///     paragraph mark, so accepting the change merges them.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove header and footer parts merge paragraphs like the body.</remarks>
    public static byte[] DocumentWithMergedFooterParagraphs() => BuildDocx((document, mainPart) =>
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new W.Footer();
        footer.AppendChild(ParagraphWithTrackedMark(true, null, Run("Left part ")));
        footer.AppendChild(TextParagraph("right part"));
        footerPart.Footer = footer;
        var footerId = mainPart.GetIdOfPart(footerPart);

        var body = new W.Body();
        body.AppendChild(TextParagraph("Body text."));
        body.AppendChild(SectionProperties(null, footerId));
        SetBody(mainPart, body);
        SetProperties(document, "Merged Footer", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose footer opens with two page-numbering-only paragraphs (one complex
    ///     <c>PAGE</c> field, one simple <c>NUMPAGES</c> field) before a paragraph with a tracked insertion.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove the scanner ignores the furniture the reader strips.</remarks>
    public static byte[] DocumentWithPageNumberFooterBeforeRevision() => BuildDocx((document, mainPart) =>
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new W.Footer();

        var pageParagraph = new W.Paragraph();
        pageParagraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
        pageParagraph.AppendChild(FieldRun(new W.FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }));
        pageParagraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        pageParagraph.AppendChild(Run("7"));
        pageParagraph.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        footer.AppendChild(pageParagraph);

        var countParagraph = new W.Paragraph();
        var simple = new W.SimpleField { Instruction = " NUMPAGES " };
        simple.AppendChild(Run("9"));
        countParagraph.AppendChild(simple);
        footer.AppendChild(countParagraph);

        var revised = new W.Paragraph();
        revised.AppendChild(Run("Draft footer "));
        revised.AppendChild(Revision<W.InsertedRun>("94", "Mona", "2026-09-05T00:00:00Z", Run("note")));
        footer.AppendChild(revised);
        footerPart.Footer = footer;
        var footerId = mainPart.GetIdOfPart(footerPart);

        var body = new W.Body();
        body.AppendChild(TextParagraph("Body text."));
        body.AppendChild(SectionProperties(null, footerId));
        SetBody(mainPart, body);
        SetProperties(document, "Page Number Footer", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with two different footers on two sections, each carrying a tracked change.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove header and footer locations use content.md labels.</remarks>
    public static byte[] DocumentWithTwoRevisedFooters() => BuildDocx((document, mainPart) =>
    {
        var firstFooter = new W.Paragraph();
        firstFooter.AppendChild(Run("Draft footer "));
        firstFooter.AppendChild(Revision<W.InsertedRun>("92", "Mona", "2026-09-03T00:00:00Z", Run("one")));
        var firstId = AddFooter(mainPart, firstFooter);

        var secondFooter = new W.Paragraph();
        secondFooter.AppendChild(Run("Final footer "));
        secondFooter.AppendChild(Revision<W.InsertedRun>("93", "Mona", "2026-09-04T00:00:00Z", Run("two")));
        var secondId = AddFooter(mainPart, secondFooter);

        var body = new W.Body();
        var first = TextParagraph("First section text.");
        var firstProperties = new W.ParagraphProperties();
        firstProperties.AppendChild(SectionProperties(null, firstId));
        first.PrependChild(firstProperties);
        body.AppendChild(first);
        body.AppendChild(TextParagraph("Second section text."));
        body.AppendChild(SectionProperties(null, secondId));
        SetBody(mainPart, body);
        SetProperties(document, "Two Footers", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a tracked change in every kind of place a location must describe:
    ///     before the first heading, under nested headings, in a table cell, under a deleted heading,
    ///     as a formatting-only change, in a footer, and in a footnote.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove each revision gets a useful location.</remarks>
    public static byte[] DocumentWithLocatedRevisions() => BuildDocx((document, mainPart) =>
    {
        var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
        var footnotes = new W.Footnotes();
        var footnote = new W.Footnote { Id = 1 };
        var footnoteParagraph = new W.Paragraph();
        footnoteParagraph.AppendChild(Revision<W.InsertedRun>("70", "Gus", "2026-07-02T00:00:00Z", Run("note addition")));
        footnote.AppendChild(footnoteParagraph);
        footnotes.AppendChild(footnote);
        footnotesPart.Footnotes = footnotes;

        var footerParagraph = new W.Paragraph();
        footerParagraph.AppendChild(Revision<W.DeletedRun>("71", "Gus", "2026-07-03T00:00:00Z", DeletedTextRun("old footer")));
        var footerId = AddFooter(mainPart, footerParagraph);

        var body = new W.Body();

        var preface = new W.Paragraph();
        preface.AppendChild(Run("Preface "));
        preface.AppendChild(Revision<W.InsertedRun>("60", "Hana", "2026-07-01T08:00:00Z", Run("early note")));
        body.AppendChild(preface);

        body.AppendChild(StyledParagraph("Alpha", "Heading1"));
        body.AppendChild(TextParagraph("Alpha first."));
        var second = new W.Paragraph();
        second.AppendChild(Run("Alpha second "));
        second.AppendChild(Revision<W.DeletedRun>("61", "Hana", "2026-07-01T09:00:00Z", DeletedTextRun("gone")));
        second.AppendChild(Run(" claim"));
        var reference = new W.Run();
        reference.AppendChild(new W.FootnoteReference { Id = 1 });
        second.AppendChild(reference);
        body.AppendChild(second);

        body.AppendChild(StyledParagraph("Beta", "Heading2"));
        var beta = new W.Paragraph();
        beta.AppendChild(Run("Beta text "));
        beta.AppendChild(Revision<W.InsertedRun>("62", "Ivan", "2026-07-01T10:00:00Z", Run("new")));
        body.AppendChild(beta);

        var cell = new W.TableCell();
        var cellParagraph = new W.Paragraph();
        cellParagraph.AppendChild(Run("Cell "));
        cellParagraph.AppendChild(Revision<W.InsertedRun>("63", "Ivan", "2026-07-01T11:00:00Z", Run("edit")));
        cell.AppendChild(cellParagraph);
        var row = new W.TableRow();
        row.AppendChild(Cell("Head"));
        row.AppendChild(cell);
        var table = new W.Table();
        table.AppendChild(row);
        body.AppendChild(table);

        var retired = new W.Paragraph();
        var retiredProperties = new W.ParagraphProperties();
        retiredProperties.AppendChild(new W.ParagraphStyleId { Val = "Heading1" });
        retired.AppendChild(retiredProperties);
        retired.AppendChild(Revision<W.DeletedRun>("64", "Hana", "2026-07-01T12:00:00Z", DeletedTextRun("Retired heading")));
        body.AppendChild(retired);

        var formatted = new W.Paragraph();
        var formattedRun = Run("Formatted words");
        var runProperties = new W.RunProperties();
        runProperties.AppendChild(new W.Bold());
        var propertiesChange = new W.RunPropertiesChange
        {
            Id = "65",
            Author = "Jo",
            Date = DateTime.Parse("2026-07-01T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)
        };
        propertiesChange.AppendChild(new W.PreviousRunProperties());
        runProperties.AppendChild(propertiesChange);
        formattedRun.PrependChild(runProperties);
        formatted.AppendChild(formattedRun);
        body.AppendChild(formatted);

        body.AppendChild(SectionProperties(null, footerId));
        SetBody(mainPart, body);
        SetProperties(document, "Located Revisions", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose table of contents (a block content control holding <c>TOC1</c>
    ///     paragraphs inside a <c>TOC</c> field) carries two tracked changes, followed by a heading
    ///     with one tracked change of its own.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove the contents are omitted, reported, and counted.</remarks>
    public static byte[] DocumentWithTrackedTableOfContents() => BuildDocx((document, mainPart) =>
    {
        var content = new W.SdtContentBlock();
        content.AppendChild(StyledParagraph("Contents", "TOCHeading"));

        var first = StyledParagraph(string.Empty, "TOC1");
        first.RemoveChild(first.GetFirstChild<W.Run>()!);
        first.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
        first.AppendChild(FieldRun(new W.FieldCode(" TOC \\o \"1-3\" ") { Space = SpaceProcessingModeValues.Preserve }));
        first.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        first.AppendChild(Revision<W.InsertedRun>("80", "Kim", "2026-08-01T00:00:00Z", Run("Overview entry")));
        content.AppendChild(first);

        var second = StyledParagraph(string.Empty, "TOC1");
        second.RemoveChild(second.GetFirstChild<W.Run>()!);
        second.AppendChild(Revision<W.DeletedRun>("81", "Kim", "2026-08-02T00:00:00Z", DeletedTextRun("Retired entry")));
        content.AppendChild(second);

        var last = StyledParagraph("Closing entry", "TOC1");
        last.AppendChild(FieldRun(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        content.AppendChild(last);

        var sdt = new W.SdtBlock();
        var docPartObject = new W.SdtContentDocPartObject();
        docPartObject.AppendChild(new W.DocPartGallery { Val = "Table of Contents" });
        var sdtProperties = new W.SdtProperties();
        sdtProperties.AppendChild(docPartObject);
        sdt.AppendChild(sdtProperties);
        sdt.AppendChild(content);

        var body = new W.Body();
        body.AppendChild(sdt);
        body.AppendChild(StyledParagraph("Overview", "Heading1"));
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(Run("Real text "));
        paragraph.AppendChild(Revision<W.InsertedRun>("82", "Lee", "2026-08-03T00:00:00Z", Run("added")));
        body.AppendChild(paragraph);
        SetBody(mainPart, body);
        SetProperties(document, "Contents Redline", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a table of contents made only of <c>TOC1</c>-styled paragraphs (no
    ///     content control, no field) and an ordinary content control holding a real paragraph.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Used to prove style-based detection and that other content controls are read.</remarks>
    public static byte[] DocumentWithStyleOnlyTocAndContentControl() => BuildDocx((document, mainPart) =>
    {
        var body = new W.Body();
        body.AppendChild(StyledParagraph("Alpha entry", "TOC1"));
        body.AppendChild(StyledParagraph("Beta entry", "TOC2"));
        body.AppendChild(StyledParagraph("Alpha", "Heading1"));

        var content = new W.SdtContentBlock();
        content.AppendChild(TextParagraph("Inside a content control."));
        var sdt = new W.SdtBlock();
        sdt.AppendChild(new W.SdtProperties());
        sdt.AppendChild(content);
        body.AppendChild(sdt);

        SetBody(mainPart, body);
        SetProperties(document, "Style Contents", "DocDown Test Suite");
    });
}
