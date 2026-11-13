package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordOpenXmlReader {
        doc /* Turns the Open XML document into the backend-neutral model: maps headings, lists, tables, fields, comments, footnotes, and Document Control, drops comments carrying no text while counting those whose only content is a picture or ink, resolves each comment's anchor into a heading-and-snippet location hint, renders tracked changes as redline markup (or the accepted view on request), extracts the preview pictures of legacy objects, detects the protected container, and surfaces the document metadata. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordOpenXmlReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlReaderTests.cs", "test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordTrackedChangesTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-open-xml-reader.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-open-xml-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-open-xml-reader.yaml");
        }
    }
}
