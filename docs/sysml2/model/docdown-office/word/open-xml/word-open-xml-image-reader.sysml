package DocDownWord {
    part def WordOpenXmlImageReader {
        doc /* Resolves a document's embedded image parts and yields each one's bytes with an honest hint: a passthrough transform, unstated pixel dimensions because the stored extent is a display size, and the part's own media type. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/OpenXml/WordOpenXmlImageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlImageReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/open-xml/word-open-xml-image-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/open-xml/word-open-xml-image-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/open-xml/word-open-xml-image-reader.yaml */
    }
}
