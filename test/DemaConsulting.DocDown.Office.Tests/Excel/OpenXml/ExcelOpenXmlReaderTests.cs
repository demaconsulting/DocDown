using DemaConsulting.DocDown.Office.Tests.Excel.TestData;
using DocDown.Excel.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Excel.OpenXml;

/// <summary>
///     Unit tests for <see cref="ExcelOpenXmlReader"/>, exercising worksheet, cell, value, and
///     formula reading against workbooks synthesized at test time.
/// </summary>
public class ExcelOpenXmlReaderTests
{
    /// <summary>
    ///     Proves the reader resolves an embedded worksheet picture to an image part carrying its
    ///     bytes, media type, and the authored description as its naming source.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_ImageWorkbook_ExtractsEmbeddedImage()
    {
        using var stream = new MemoryStream(XlsxFixtures.ImageWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var image = Assert.Single(model.Images);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(5, image.Bytes.Length);
        Assert.Equal("Sales chart", image.Description);
    }

    /// <summary>
    ///     Proves the reader records the 1-based worksheet tab index (not the arbitrary part order) as
    ///     the image's referrer, and exposes the image as a per-sheet reference for inline linking. A
    ///     workbook has no template concept, so the image is never flagged template-referenced.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_ImageWorkbook_RecordsSheetAssociation()
    {
        using var stream = new MemoryStream(XlsxFixtures.ImageWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var image = Assert.Single(model.Images);
        Assert.Equal([1], image.SourcePages);
        Assert.Equal(1, image.SourcePage);
        Assert.False(image.ReferencedByTemplate);

        var sheet = Assert.Single(model.Sheets);
        var reference = Assert.Single(sheet.Images);
        Assert.Equal(image.SourceRef, reference.SourceRef);
    }

    /// <summary>
    ///     Proves the reader returns the worksheets in workbook order with their tab names.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_TwoSheetWorkbook_ReturnsSheetsInOrderWithNames()
    {
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        Assert.Equal(2, model.Sheets.Count);
        Assert.Equal("Requirements", model.Sheets[0].Name);
        Assert.Equal("Calculations", model.Sheets[1].Name);
    }

    /// <summary>
    ///     Proves the reader preserves a long prose cell verbatim at full length, never truncating it.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_LongProseCell_PreservedVerbatimAtFullLength()
    {
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var prose = model.Sheets[0].Cells.Single(cell => cell.Reference == "A2");
        Assert.Equal(XlsxFixtures.LongProse, prose.Value);
    }

    /// <summary>
    ///     Proves the reader records a formula alongside its cached computed value, and cites the cell
    ///     by address.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_FormulaCell_KeepsFormulaAndValueAndAddress()
    {
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var formulaCell = model.Sheets[1].Cells.Single(cell => cell.Reference == "A3");
        Assert.Equal("A1+A2", formulaCell.Formula);
        Assert.Equal("5", formulaCell.Value);
    }

    /// <summary>
    ///     Proves numeric cells are read verbatim as stored.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_NumericCells_ReadVerbatim()
    {
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var calc = model.Sheets[1];
        Assert.Equal("2", calc.Cells.Single(cell => cell.Reference == "A1").Value);
        Assert.Equal("3", calc.Cells.Single(cell => cell.Reference == "A2").Value);
    }

    /// <summary>
    ///     Proves an empty worksheet yields a sheet with no cells rather than an error.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_EmptySheet_YieldsSheetWithNoCells()
    {
        using var stream = new MemoryStream(XlsxFixtures.EmptySheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var sheet = Assert.Single(model.Sheets);
        Assert.Empty(sheet.Cells);
    }

    /// <summary>
    ///     Proves a worksheet whose only comment is a legacy one is read, with its author resolved
    ///     through the comments part's own author list.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_LegacyCommentWorkbook_ReadsCommentWithAuthor()
    {
        using var stream = new MemoryStream(XlsxFixtures.LegacyCommentWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var comment = Assert.Single(Assert.Single(model.Sheets).Comments);
        Assert.Equal("B7", comment.Reference);
        Assert.Equal("Dana Reyes", comment.Author);
        Assert.Equal("Confirm this against the test report.", comment.Text);
    }

    /// <summary>
    ///     Proves a worksheet whose only comment is a modern threaded one is read, with its author
    ///     resolved through the workbook's person list rather than left as a raw identifier.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_ThreadedCommentWorkbook_ReadsCommentWithResolvedPerson()
    {
        using var stream = new MemoryStream(XlsxFixtures.ThreadedCommentWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var comment = Assert.Single(Assert.Single(model.Sheets).Comments);
        Assert.Equal("C3", comment.Reference);
        Assert.Equal("Morgan Patel", comment.Author);
        Assert.Equal("This mass excludes the bracket.", comment.Text);
    }

    /// <summary>
    ///     Proves a cell carrying both a threaded comment and the backward-compatibility legacy
    ///     comment beside it reports the threaded remark once, not both: a reader shown the same
    ///     remark twice would read it as two reviewers raising the same point.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_ThreadedAndLegacyOnSameCell_KeepsOnlyThreadedComment()
    {
        using var stream = new MemoryStream(XlsxFixtures.ThreadedAndLegacyCommentWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        var comment = Assert.Single(Assert.Single(model.Sheets).Comments);
        Assert.Equal("D5", comment.Reference);
        Assert.Equal("Threaded remark that supersedes the placeholder.", comment.Text);
    }

    /// <summary>
    ///     Proves each comment stays with the worksheet that carries it, so a remark on one sheet is
    ///     never reported against another.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_MultiSheetCommentWorkbook_AttributesCommentsToOwningSheet()
    {
        using var stream = new MemoryStream(XlsxFixtures.MultiSheetCommentWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        Assert.Equal(2, model.Sheets.Count);

        var first = Assert.Single(model.Sheets[0].Comments);
        Assert.Equal("Inputs", model.Sheets[0].Name);
        Assert.Equal("A1", first.Reference);
        Assert.Equal("Dana Reyes", first.Author);

        var second = Assert.Single(model.Sheets[1].Comments);
        Assert.Equal("Results", model.Sheets[1].Name);
        Assert.Equal("B2", second.Reference);
        Assert.Equal("Sam Whitfield", second.Author);
    }

    /// <summary>
    ///     Proves a workbook with no comment parts at all yields no comments rather than an error.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlReader_Read_WorkbookWithoutComments_YieldsNoComments()
    {
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());

        var model = ExcelOpenXmlReader.Read(stream);

        Assert.All(model.Sheets, sheet => Assert.Empty(sheet.Comments));
    }
}
