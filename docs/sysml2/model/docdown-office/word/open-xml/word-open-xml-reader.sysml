package DocDownWord {
    part def WordOpenXmlReader {
        doc /* Turns the Open XML document into the backend-neutral model: maps headings, lists, tables, fields, comments, footnotes, and Document Control, renders tracked changes in the accepted view, detects the protected container, and surfaces the document metadata. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/OpenXml/WordOpenXmlReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/open-xml/word-open-xml-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/open-xml/word-open-xml-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/open-xml/word-open-xml-reader.yaml */
    }
}
