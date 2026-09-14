package DocDownExcel {
    part def ExcelOpenXmlExtractor {
        doc /* The managed backend the engine selects and invokes: declares its priority, declares page rendering not applicable to a non-paginated workbook, probes unconditionally, buffers and opens the workbook, reads it into the model, delegates emission to the shared emitter, and contributes the self-tests. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelOpenXmlExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/open-xml/excel-open-xml-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/open-xml/excel-open-xml-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/open-xml/excel-open-xml-extractor.yaml */
    }
}
