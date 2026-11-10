using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DemaConsulting.DocDown.Office.Tests.Word.TestData;

/// <summary>
///     Synthetic Word documents carrying symbol characters (<c>w:sym</c>) for the symbol tests.
/// </summary>
/// <remarks>
///     Every document is built in code from invented text; none is derived from a real document.
///     Shares the private helpers of the main fixture class.
/// </remarks>
public static partial class DocxFixtures
{
    /// <summary>
    ///     Builds a run holding one symbol character.
    /// </summary>
    /// <param name="font">The symbol font name.</param>
    /// <param name="charHex">The hexadecimal character code.</param>
    /// <returns>The run element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.Run SymbolRun(string font, string charHex)
    {
        var run = new W.Run();
        run.AppendChild(new W.SymbolChar { Font = font, Char = charHex });
        return run;
    }

    /// <summary>
    ///     Builds a table cell holding a paragraph made of the given runs.
    /// </summary>
    /// <param name="runs">The runs of the cell paragraph.</param>
    /// <returns>The cell element.</returns>
    /// <remarks>Pure.</remarks>
    private static W.TableCell CellOf(params W.Run[] runs)
    {
        var paragraph = new W.Paragraph();
        foreach (var run in runs)
        {
            paragraph.AppendChild(run);
        }

        var cell = new W.TableCell();
        cell.AppendChild(paragraph);
        return cell;
    }

    /// <summary>
    ///     Builds a three-column table whose body cells are bullet dots drawn from the Symbol font.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. Every body cell but one holds only a dot, so the table proves that a
    ///     symbol-only cell is neither blank nor skipped as an empty table.
    /// </remarks>
    public static byte[] DocumentWithSymbolDotMatrix() => BuildDocx((document, mainPart) =>
    {
        W.TableRow Row(params W.TableCell[] cells)
        {
            var row = new W.TableRow();
            foreach (var cell in cells)
            {
                row.AppendChild(cell);
            }

            return row;
        }

        var table = new W.Table();
        table.AppendChild(Row(Cell("Item"), Cell("Low"), Cell("High")));
        table.AppendChild(Row(Cell("Widget"), CellOf(SymbolRun("Symbol", "F0B7")), CellOf(SymbolRun("Symbol", "F0B7"))));
        table.AppendChild(Row(Cell("Gadget"), CellOf(SymbolRun("Symbol", "F0B7")), CellOf(SymbolRun("Symbol", "F0B7"), Run(" Alpha"))));
        var body = new W.Body();
        body.AppendChild(table);
        SetBody(mainPart, body);
        SetProperties(document, "Symbol Matrix", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a table in which every cell holds only a single Symbol bullet dot.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented. Proves a table made wholly of symbols is not dropped as empty.</remarks>
    public static byte[] DocumentWithSymbolOnlyTable() => BuildDocx((document, mainPart) =>
    {
        var table = new W.Table();
        for (var rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = new W.TableRow();
            row.AppendChild(CellOf(SymbolRun("Symbol", "F0B7")));
            row.AppendChild(CellOf(SymbolRun("Symbol", "F0B7")));
            table.AppendChild(row);
        }

        var body = new W.Body();
        body.AppendChild(table);
        SetBody(mainPart, body);
        SetProperties(document, "Symbol Only Table", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with Wingdings checkbox and check-mark symbols, one written as a bare code.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented.</remarks>
    public static byte[] DocumentWithWingdingsCheckbox() => BuildDocx((document, mainPart) =>
    {
        var checkbox = new W.Paragraph();
        checkbox.AppendChild(Run("Approved "));
        checkbox.AppendChild(SymbolRun("Wingdings", "F0FE"));
        var bare = new W.Paragraph();
        bare.AppendChild(Run("Verified "));
        bare.AppendChild(SymbolRun("wingdings", "00FC"));
        var body = new W.Body();
        body.AppendChild(checkbox);
        body.AppendChild(bare);
        SetBody(mainPart, body);
        SetProperties(document, "Wingdings Checkbox", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with two unmappable symbols (one in a table cell) and one mappable symbol.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. One symbol names an unrecognized font and one an unlisted Wingdings
    ///     code; the table-cell symbol is read while the reader decides whether the table is empty and
    ///     again while it renders, so it proves each source symbol is counted once.
    /// </remarks>
    public static byte[] DocumentWithUnmappedSymbols() => BuildDocx((document, mainPart) =>
    {
        var first = new W.Paragraph();
        first.AppendChild(Run("Alpha "));
        first.AppendChild(SymbolRun("Invented Glyphs", "F041"));
        first.AppendChild(Run(" Beta "));
        first.AppendChild(SymbolRun("Symbol", "F0B7"));
        var table = new W.Table();
        var row = new W.TableRow();
        row.AppendChild(CellOf(SymbolRun("Wingdings", "F0FF"), Run(" Gamma")));
        table.AppendChild(row);
        var body = new W.Body();
        body.AppendChild(first);
        body.AppendChild(table);
        SetBody(mainPart, body);
        SetProperties(document, "Unmapped Symbols", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document with a Symbol bullet inside a tracked insertion and a tracked deletion.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>All text is invented.</remarks>
    public static byte[] DocumentWithSymbolInTrackedChanges() => BuildDocx((document, mainPart) =>
    {
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(Run("Start "));
        paragraph.AppendChild(Revision<W.InsertedRun>("1", "Dana", "2026-02-03T00:00:00Z", SymbolRun("Symbol", "F0B7")));
        paragraph.AppendChild(Run(" middle "));
        paragraph.AppendChild(Revision<W.DeletedRun>("2", "Eli", "2026-02-04T00:00:00Z", SymbolRun("Wingdings", "F0FC")));
        var body = new W.Body();
        body.AppendChild(paragraph);
        SetBody(mainPart, body);
        SetProperties(document, "Symbol Revisions", "DocDown Test Suite");
    });

    /// <summary>
    ///     Builds a document whose header holds a Symbol bullet and whose footer holds a page-number
    ///     field with an unmappable symbol in its result.
    /// </summary>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     All text is invented. The header symbol must render; the footer field result is page-number
    ///     furniture, so its symbol must be skipped and must not be counted as unmapped.
    /// </remarks>
    public static byte[] DocumentWithSymbolInHeaderAndFooterField() => BuildDocx((document, mainPart) =>
    {
        var headerParagraph = new W.Paragraph();
        headerParagraph.AppendChild(Run("Header "));
        headerParagraph.AppendChild(SymbolRun("Symbol", "F0B7"));

        var footerParagraph = new W.Paragraph();
        footerParagraph.AppendChild(Compose(new W.Run(), new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
        footerParagraph.AppendChild(Compose(new W.Run(), new W.FieldCode(" PAGE ")));
        footerParagraph.AppendChild(Compose(new W.Run(), new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        footerParagraph.AppendChild(SymbolRun("Invented Glyphs", "F041"));
        footerParagraph.AppendChild(Compose(new W.Run(), new W.FieldChar { FieldCharType = W.FieldCharValues.End }));

        var body = new W.Body();
        body.AppendChild(TextParagraph("Body content."));
        var headerId = AddHeader(mainPart, headerParagraph);
        var footerId = AddFooter(mainPart, footerParagraph);
        body.AppendChild(SectionProperties(headerId, footerId));
        SetBody(mainPart, body);
        SetProperties(document, "Symbol Header", "DocDown Test Suite");
    });
}
