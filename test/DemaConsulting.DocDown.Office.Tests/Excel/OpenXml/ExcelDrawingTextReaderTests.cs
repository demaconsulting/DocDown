using DemaConsulting.DocDown.Excel.OpenXml;
using DemaConsulting.DocDown.Office.Tests.Excel.TestData;
using DocumentFormat.OpenXml.Packaging;

namespace DemaConsulting.DocDown.Office.Tests.Excel.OpenXml;

/// <summary>
///     Unit tests for <see cref="ExcelDrawingTextReader"/>, reading real drawing parts from workbooks
///     synthesized at test time.
/// </summary>
public class ExcelDrawingTextReaderTests
{
    /// <summary>
    ///     Proves shape text is read from a real drawing part in drawing order, a line break reads as a
    ///     space so words do not fuse, paragraphs join with a space, an empty shape contributes nothing,
    ///     and a shape nested in a group is read.
    /// </summary>
    [Fact]
    public void ExcelDrawingTextReader_Collect_RealDrawing_ReadsShapesInOrderWithBreaksAsSpaces()
    {
        // Arrange: a workbook whose drawing holds a broken shape, an empty shape, and a grouped shape
        using var stream = new MemoryStream(XlsxFixtures.ShapeTextWorkbook());
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();

        // Act: collect the shape text
        var texts = ExcelDrawingTextReader.Collect(worksheetPart);

        // Assert: two paragraphs join, the break is a space, the empty shape is skipped, the group is read
        Assert.Equal(["Bearing retainer Part LX-4120", "Grouped callout"], texts);
    }

    /// <summary>
    ///     Proves a missing worksheet part and a worksheet without a drawing both yield no text.
    /// </summary>
    [Fact]
    public void ExcelDrawingTextReader_Collect_NoPartOrNoDrawing_ReturnsEmpty()
    {
        // Arrange: a workbook whose single worksheet has no drawing
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.First();

        // Act and assert: neither case yields text
        Assert.Empty(ExcelDrawingTextReader.Collect(null));
        Assert.Empty(ExcelDrawingTextReader.Collect(worksheetPart));
    }
}
