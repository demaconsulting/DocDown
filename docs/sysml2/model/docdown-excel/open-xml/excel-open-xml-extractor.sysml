package DocDownExcel {
    part def ExcelOpenXmlExtractor {
        doc /* The managed backend the engine selects and invokes: declares its capabilities and priority, declares page rendering not applicable to a non-paginated workbook, probes unconditionally, buffers and opens the workbook, reads it into the model, delegates emission to the shared emitter, and contributes the self-tests. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/OpenXml/ExcelOpenXmlExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/OpenXml/ExcelOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/open-xml/excel-open-xml-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/open-xml/excel-open-xml-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/open-xml/excel-open-xml-extractor.yaml */
    }
}
