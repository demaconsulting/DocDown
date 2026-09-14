using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DemaConsulting.DocDown.Office.Tests.Excel.TestData;

/// <summary>
///     Builds every workbook the test suite needs, at test time, from the Open XML SDK.
/// </summary>
/// <remarks>
///     No binary <c>.xlsx</c> is committed to this repository. Each fixture is synthesized here when
///     the test that needs it runs, which keeps the repository text-only and makes the exact content
///     of every fixture readable in the same place it is asserted against. All members are static and
///     pure apart from their allocations.
/// </remarks>
public static class XlsxFixtures
{
    /// <summary>
    ///     A deliberately long prose block, longer than any spreadsheet display column, used to prove
    ///     the extractor preserves cell text at full length and never truncates it.
    /// </summary>
    public const string LongProse =
        "The coolant loop shall maintain a regulated supply pressure of no less than 40 psi at the "
        + "inlet under all documented operating conditions, including the worst-case simultaneous "
        + "draw of both the primary and secondary outlets, and shall recover to nominal within two "
        + "seconds of a transient, as recorded in the review transcript pasted verbatim into this "
        + "cell so that the requirement quotation survives extraction without being clipped.";

    /// <summary>
    ///     Builds arbitrary bytes for a legacy <c>.xls</c> fixture whose content is never read.
    /// </summary>
    /// <returns>Placeholder bytes.</returns>
    /// <remarks>Used where selection fails before any extractor opens the workbook.</remarks>
    public static byte[] LegacyXlsBytes() => "This is a placeholder for a legacy binary Excel workbook."u8.ToArray();

    /// <summary>
    ///     Builds a workbook with two worksheets: a prose sheet carrying a long requirement quotation
    ///     and a calculations sheet carrying numeric inputs and a formula with its cached value.
    /// </summary>
    /// <returns>The workbook bytes.</returns>
    public static byte[] TwoSheetWorkbook() => Build(workbookPart =>
    {
        var sheets = new S.Sheets();

        AddSheet(workbookPart, sheets, "Requirements", 1U,
        [
            InlineStringCell("A1", "Coolant Pressure Requirement"),
            InlineStringCell("A2", LongProse)
        ]);

        AddSheet(workbookPart, sheets, "Calculations", 2U,
        [
            NumberCell("A1", "2"),
            NumberCell("A2", "3"),
            FormulaCell("A3", "A1+A2", "5")
        ]);

        workbookPart.Workbook!.AppendChild(sheets);
    });

    /// <summary>
    ///     Builds a workbook with a single empty worksheet, to exercise the empty-sheet path.
    /// </summary>
    /// <returns>The workbook bytes.</returns>
    public static byte[] EmptySheetWorkbook() => Build(workbookPart =>
    {
        var sheets = new S.Sheets();
        AddSheet(workbookPart, sheets, "Blank", 1U, []);
        workbookPart.Workbook!.AppendChild(sheets);
    });

    /// <summary>
    ///     Builds a workbook whose single worksheet carries a cell and one embedded PNG picture with
    ///     an authored description, so the image reader and the sink-write pipeline can be exercised.
    /// </summary>
    /// <returns>The workbook bytes.</returns>
    public static byte[] ImageWorkbook() => Build(workbookPart =>
    {
        var sheets = new S.Sheets();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new S.SheetData();
        var row = new S.Row();
        row.AppendChild(InlineStringCell("A1", "Chart data"));
        sheetData.AppendChild(row);
        var worksheet = new S.Worksheet();
        worksheet.AppendChild(sheetData);

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
        using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            stream.Write([10, 20, 30, 40, 50], 0, 5);
        }

        var relationshipId = drawingsPart.GetIdOfPart(imagePart);
        drawingsPart.WorksheetDrawing = BuildWorksheetDrawing(relationshipId);

        worksheet.AppendChild(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        worksheetPart.Worksheet = worksheet;

        sheets.AppendChild(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Charts"
        });

        workbookPart.Workbook!.AppendChild(sheets);
    });

    /// <summary>
    ///     Builds a workbook whose single worksheet carries a cell and one embedded EMF vector
    ///     metafile picture with an authored description, so the vector-passthrough caveat can be
    ///     exercised end to end.
    /// </summary>
    /// <returns>The workbook bytes.</returns>
    public static byte[] VectorImageWorkbook() => Build(workbookPart =>
    {
        var sheets = new S.Sheets();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new S.SheetData();
        var row = new S.Row();
        row.AppendChild(InlineStringCell("A1", "Schematic data"));
        sheetData.AppendChild(row);
        var worksheet = new S.Worksheet();
        worksheet.AppendChild(sheetData);

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var imagePart = drawingsPart.AddImagePart(ImagePartType.Emf);
        using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            stream.Write([0x01, 0x00, 0x00, 0x00, 0x45, 0x4D, 0x46, 0x20], 0, 8);
        }

        var relationshipId = drawingsPart.GetIdOfPart(imagePart);
        drawingsPart.WorksheetDrawing = BuildWorksheetDrawing(relationshipId);

        worksheet.AppendChild(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        worksheetPart.Worksheet = worksheet;

        sheets.AppendChild(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Schematics"
        });

        workbookPart.Workbook!.AppendChild(sheets);
    });

    /// <summary>
    ///     Builds a workbook whose single worksheet anchors one line chart with cached category and
    ///     value data, so the chart reader's package walk can be exercised end to end.
    /// </summary>
    /// <returns>The workbook bytes.</returns>
    /// <remarks>
    ///     The chart part XML is written directly rather than assembled from the typed chart classes,
    ///     because what this fixture must pin is the exact on-disk grammar a real chart uses — the
    ///     cached <c>c:numCache</c> points the reader recovers. Every value is invented: a fictional
    ///     vessel's pressure over time, belonging to no real document.
    /// </remarks>
    public static byte[] ChartWorkbook() => Build(workbookPart =>
    {
        var sheets = new S.Sheets();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new S.SheetData();
        var row = new S.Row();
        row.AppendChild(InlineStringCell("A1", "Pressure log"));
        sheetData.AppendChild(row);
        var worksheet = new S.Worksheet();
        worksheet.AppendChild(sheetData);

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        using (var stream = chartPart.GetStream(FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(ChartPartXml);
        }

        drawingsPart.WorksheetDrawing = BuildChartDrawing(drawingsPart.GetIdOfPart(chartPart));

        worksheet.AppendChild(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        worksheetPart.Worksheet = worksheet;

        sheets.AppendChild(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Pressure"
        });

        workbookPart.Workbook!.AppendChild(sheets);
    });

    /// <summary>
    ///     The chart part XML of <see cref="ChartWorkbook"/>: an authored title, both axis titles, and
    ///     three cached points.
    /// </summary>
    /// <remarks>
    ///     Entirely invented content describing a fictional pressure vessel, so no confidential
    ///     material from any real workbook can reach this repository.
    /// </remarks>
    public const string ChartPartXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" "
        + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><c:chart>"
        + "<c:title><c:tx><c:rich><a:p><a:r><a:t>Tank Pressure Trend</a:t></a:r></a:p></c:rich></c:tx></c:title>"
        + "<c:plotArea><c:lineChart><c:ser><c:tx><c:v>Vessel A</c:v></c:tx>"
        + "<c:cat><c:numRef><c:f>Pressure!$A$2:$A$4</c:f><c:numCache><c:formatCode>General</c:formatCode>"
        + "<c:ptCount val=\"3\"/><c:pt idx=\"0\"><c:v>0</c:v></c:pt><c:pt idx=\"1\"><c:v>5</c:v></c:pt>"
        + "<c:pt idx=\"2\"><c:v>10</c:v></c:pt></c:numCache></c:numRef></c:cat>"
        + "<c:val><c:numRef><c:f>Pressure!$B$2:$B$4</c:f><c:numCache><c:formatCode>0.0</c:formatCode>"
        + "<c:ptCount val=\"3\"/><c:pt idx=\"0\"><c:v>101.3</c:v></c:pt><c:pt idx=\"1\"><c:v>104.8</c:v></c:pt>"
        + "<c:pt idx=\"2\"><c:v>109.2</c:v></c:pt></c:numCache></c:numRef></c:val></c:ser></c:lineChart>"
        + "<c:catAx><c:title><c:tx><c:rich><a:p><a:r><a:t>Elapsed time (min)</a:t></a:r></a:p></c:rich></c:tx>"
        + "</c:title></c:catAx>"
        + "<c:valAx><c:title><c:tx><c:rich><a:p><a:r><a:t>Pressure (kPa)</a:t></a:r></a:p></c:rich></c:tx>"
        + "</c:title></c:valAx></c:plotArea></c:chart></c:chartSpace>";

    /// <summary>
    ///     Builds a worksheet drawing anchoring one graphic frame that references the chart part.
    /// </summary>
    /// <param name="relationshipId">The relationship id of the chart part.</param>
    /// <returns>The worksheet drawing.</returns>
    /// <remarks>
    ///     The frame is written with the same graphic-data grammar a spreadsheet application emits, so
    ///     the reader's frame walk is exercised rather than only its relationship sweep.
    /// </remarks>
    private static Xdr.WorksheetDrawing BuildChartDrawing(string relationshipId)
    {
        var chartReference = new DocumentFormat.OpenXml.Drawing.Charts.ChartReference { Id = relationshipId };
        var graphicData = new D.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };
        graphicData.AppendChild(chartReference);
        var graphic = new D.Graphic();
        graphic.AppendChild(graphicData);

        var frame = new Xdr.GraphicFrame();
        frame.AppendChild(new Xdr.NonVisualGraphicFrameProperties(
            new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Chart 1" },
            new Xdr.NonVisualGraphicFrameDrawingProperties()));
        frame.AppendChild(new Xdr.Transform());
        frame.AppendChild(graphic);

        var anchor = new Xdr.OneCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId("1"), new Xdr.ColumnOffset("0"),
                new Xdr.RowId("1"), new Xdr.RowOffset("0")),
            new Xdr.Extent { Cx = 100L, Cy = 100L },
            frame,
            new Xdr.ClientData());

        var worksheetDrawing = new Xdr.WorksheetDrawing();
        worksheetDrawing.AppendChild(anchor);
        return worksheetDrawing;
    }

    /// <summary>
    ///     Builds a worksheet drawing anchoring one picture that references the embedded image part.
    /// </summary>
    /// <param name="relationshipId">The relationship id of the embedded image part.</param>
    /// <returns>The worksheet drawing.</returns>
    private static Xdr.WorksheetDrawing BuildWorksheetDrawing(string relationshipId)
    {
        var nonVisualPictureProperties = new Xdr.NonVisualPictureProperties(
            new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Chart", Description = "Sales chart" },
            new Xdr.NonVisualPictureDrawingProperties());

        var blipFill = new Xdr.BlipFill();
        blipFill.AppendChild(new D.Blip { Embed = relationshipId });
        var stretch = new D.Stretch();
        stretch.AppendChild(new D.FillRectangle());
        blipFill.AppendChild(stretch);

        var picture = new Xdr.Picture();
        picture.AppendChild(nonVisualPictureProperties);
        picture.AppendChild(blipFill);
        picture.AppendChild(new Xdr.ShapeProperties());

        var anchor = new Xdr.OneCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId("2"), new Xdr.ColumnOffset("0"),
                new Xdr.RowId("2"), new Xdr.RowOffset("0")),
            new Xdr.Extent { Cx = 100L, Cy = 100L },
            picture,
            new Xdr.ClientData());

        var worksheetDrawing = new Xdr.WorksheetDrawing();
        worksheetDrawing.AppendChild(anchor);
        return worksheetDrawing;
    }

    /// <summary>
    ///     Builds a workbook and returns its bytes, applying the supplied population action to the
    ///     workbook part.
    /// </summary>
    /// <param name="populate">The action that adds worksheets to the workbook part.</param>
    /// <returns>The workbook bytes.</returns>
    private static byte[] Build(Action<WorkbookPart> populate)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();
            populate(workbookPart);
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Adds a worksheet carrying the supplied cells and registers it in the sheets collection.
    /// </summary>
    /// <param name="workbookPart">The workbook part.</param>
    /// <param name="sheets">The sheets collection to register the sheet in.</param>
    /// <param name="name">The worksheet name.</param>
    /// <param name="sheetId">The worksheet identifier.</param>
    /// <param name="cells">The cells to place in the worksheet's single row.</param>
    private static void AddSheet(
        WorkbookPart workbookPart, S.Sheets sheets, string name, uint sheetId, IReadOnlyList<S.Cell> cells)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new S.SheetData();
        foreach (var cell in cells)
        {
            var row = new S.Row();
            row.AppendChild(cell);
            sheetData.AppendChild(row);
        }

        var worksheet = new S.Worksheet();
        worksheet.AppendChild(sheetData);
        worksheetPart.Worksheet = worksheet;

        sheets.AppendChild(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = name
        });
    }

    /// <summary>
    ///     Builds an inline-string cell carrying verbatim text.
    /// </summary>
    /// <param name="reference">The A1-style cell address.</param>
    /// <param name="text">The verbatim text.</param>
    /// <returns>The cell.</returns>
    private static S.Cell InlineStringCell(string reference, string text)
    {
        var inline = new S.InlineString();
        inline.AppendChild(new S.Text(text));
        var cell = new S.Cell
        {
            CellReference = reference,
            DataType = S.CellValues.InlineString
        };
        cell.AppendChild(inline);
        return cell;
    }

    /// <summary>
    ///     Builds a numeric cell carrying its value verbatim.
    /// </summary>
    /// <param name="reference">The A1-style cell address.</param>
    /// <param name="value">The numeric value as stored.</param>
    /// <returns>The cell.</returns>
    private static S.Cell NumberCell(string reference, string value) =>
        new()
        {
            CellReference = reference,
            CellValue = new S.CellValue(value)
        };

    /// <summary>
    ///     Builds a formula cell carrying both the formula and its cached computed value.
    /// </summary>
    /// <param name="reference">The A1-style cell address.</param>
    /// <param name="formula">The formula without a leading equals sign.</param>
    /// <param name="cachedValue">The cached computed value.</param>
    /// <returns>The cell.</returns>
    private static S.Cell FormulaCell(string reference, string formula, string cachedValue)
    {
        var cell = new S.Cell { CellReference = reference };
        cell.AppendChild(new S.CellFormula(formula));
        cell.AppendChild(new S.CellValue(cachedValue));
        return cell;
    }
}
