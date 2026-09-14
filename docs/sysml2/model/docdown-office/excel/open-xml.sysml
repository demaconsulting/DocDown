package DocDownOffice {
    part def ExcelOpenXml {
        doc /* The OpenXml subsystem: the managed backend — the extractor the engine selects, the reader that turns the spreadsheet package into the backend-neutral model, the image reader that yields each embedded image's bytes and sheet association, the chart reader that recovers each chart's cached data, and the drawing-text reader that recovers each shape's annotation. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/open-xml.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/open-xml.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/open-xml.yaml */

        part excelOpenXmlExtractor : ExcelOpenXmlExtractor;
        part excelOpenXmlReader : ExcelOpenXmlReader;
        part excelOpenXmlImageReader : ExcelOpenXmlImageReader;
        part excelChartReader : ExcelChartReader;
        part excelDrawingTextReader : ExcelDrawingTextReader;
    }

    part def ExcelDocumentModel {
        doc /* The reader-neutral model of a workbook: its sheets, their cells, and the charts drawn on them. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelDocumentModel.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/open-xml.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/open-xml.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/open-xml.yaml (documented as a supporting type) */
    }
}
