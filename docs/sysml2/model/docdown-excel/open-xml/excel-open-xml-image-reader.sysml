package DocDownExcel {
    part def ExcelOpenXmlImageReader {
        doc /* Resolves a workbook's embedded images to their bytes and provenance from each worksheet's drawing part, yields each image unchanged deduplicated by package part, and records the tab index of every worksheet that references an image so the sheet association survives extraction. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/OpenXml/ExcelOpenXmlImageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/OpenXml/ExcelOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/open-xml/excel-open-xml-image-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/open-xml/excel-open-xml-image-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/open-xml/excel-open-xml-image-reader.yaml */
    }
}
