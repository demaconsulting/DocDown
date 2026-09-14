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
}
