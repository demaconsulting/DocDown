package DocDownExcel {
    part def OpenXml {
        doc /* The OpenXml subsystem: the managed backend — the extractor the engine selects, the reader that turns the spreadsheet package into the backend-neutral model, the image reader that yields each embedded image's bytes and sheet association, the chart reader that recovers each chart's cached data, and the drawing-text reader that recovers each shape's annotation. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/OpenXml/ExcelOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/open-xml.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/open-xml.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/open-xml.yaml */

        part excelOpenXmlExtractor : ExcelOpenXmlExtractor;
        part excelOpenXmlReader : ExcelOpenXmlReader;
        part excelOpenXmlImageReader : ExcelOpenXmlImageReader;
        part excelChartReader : ExcelChartReader;
        part excelDrawingTextReader : ExcelDrawingTextReader;
    }
}
