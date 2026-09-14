package DocDownOffice {
    part def ExcelOpenXmlImageReader {
        doc /* Resolves a workbook's embedded images to their bytes and provenance from each worksheet's drawing part, yields each image unchanged deduplicated by package part, and records the tab index of every worksheet that references an image so the sheet association survives extraction. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelOpenXmlImageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/open-xml/excel-open-xml-image-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/open-xml/excel-open-xml-image-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/open-xml/excel-open-xml-image-reader.yaml */
    }
}
