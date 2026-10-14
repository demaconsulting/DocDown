using DemaConsulting.DocDown.Excel.OpenXml;
using DemaConsulting.DocDown.Office.Tests.Excel.TestData;
using DocumentFormat.OpenXml.Packaging;

namespace DemaConsulting.DocDown.Office.Tests.Excel.OpenXml;

/// <summary>
///     Unit tests for <see cref="ExcelOpenXmlImageReader"/>, collecting images across worksheets from
///     workbooks synthesized at test time.
/// </summary>
public class ExcelOpenXmlImageReaderTests
{
    /// <summary>
    ///     Proves a workbook whose sheets each show a picture yields each image once, attributed to its own
    ///     1-based worksheet tab index, with a per-sheet reference for inline linking.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlImageReader_Collect_TwoSheetsWithPictures_AttributesEachImageToItsSheet()
    {
        // Arrange: a workbook with a distinct picture on each of two sheets
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetImageWorkbook());
        using var document = SpreadsheetDocument.Open(stream, false);

        // Act: collect the images
        var collection = ExcelOpenXmlImageReader.Collect(document.WorkbookPart!);

        // Assert: two images, one per sheet, each referenced from its own sheet only
        Assert.Equal(2, collection.Images.Count);
        Assert.Equal([1], collection.Images[0].SourcePages);
        Assert.Equal([2], collection.Images[1].SourcePages);
        Assert.Equal([1, 2], collection.SheetImageRefs.Keys.Order());
        Assert.Equal(collection.Images[0].SourceRef, Assert.Single(collection.SheetImageRefs[1]).SourceRef);
        Assert.Equal(collection.Images[1].SourceRef, Assert.Single(collection.SheetImageRefs[2]).SourceRef);
    }

    /// <summary>
    ///     Proves one image part shown on two sheets is yielded once, and that both sheets are recorded
    ///     as its referrers and each keeps its own inline reference.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlImageReader_Collect_ImagePartSharedByTwoSheets_YieldsOneImageWithBothSheets()
    {
        // Arrange: two sheets whose drawings relate to the same image part
        using var stream = new MemoryStream(XlsxFixtures.SharedImagePartWorkbook());
        using var document = SpreadsheetDocument.Open(stream, false);

        // Act
        var collection = ExcelOpenXmlImageReader.Collect(document.WorkbookPart!);

        // Assert: one image referenced from both sheets, and each sheet links it
        var image = Assert.Single(collection.Images);
        Assert.Equal([1, 2], image.SourcePages);
        Assert.Equal(image.SourceRef, Assert.Single(collection.SheetImageRefs[1]).SourceRef);
        Assert.Equal(image.SourceRef, Assert.Single(collection.SheetImageRefs[2]).SourceRef);
    }

    /// <summary>
    ///     Proves a workbook with no drawings yields no images, and a null workbook part is rejected.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlImageReader_Collect_NoDrawingsOrNullPart_YieldsNothingOrThrows()
    {
        // Arrange: a workbook with no pictures
        using var stream = new MemoryStream(XlsxFixtures.TwoSheetWorkbook());
        using var document = SpreadsheetDocument.Open(stream, false);

        // Act
        var collection = ExcelOpenXmlImageReader.Collect(document.WorkbookPart!);

        // Assert: nothing collected, and a null part is a caller error
        Assert.Empty(collection.Images);
        Assert.Empty(collection.SheetImageRefs);
        Assert.Throws<ArgumentNullException>(() => ExcelOpenXmlImageReader.Collect(null!));
    }
}
