package DocDownExcel {
    part def ExcelOpenXmlReader {
        doc /* The one place that touches the Open XML spreadsheet object model: resolves shared strings, walks each worksheet in workbook order, keeps only cells that carry a value or a formula, never truncates a value, keeps a formula alongside its value and address, and delegates the drawing layer to the image, chart, and drawing-text readers. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/OpenXml/ExcelOpenXmlReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/OpenXml/ExcelOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/open-xml/excel-open-xml-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/open-xml/excel-open-xml-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/open-xml/excel-open-xml-reader.yaml */
    }
}
