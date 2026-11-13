package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordOpenXmlImageReader {
        doc /* Resolves a document's embedded image parts and yields each one's bytes with an honest hint: a passthrough transform, unstated pixel dimensions because the stored extent is a display size, and the part's own media type. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordOpenXmlImageReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlImageReaderTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-open-xml-image-reader.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-open-xml-image-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-open-xml-image-reader.yaml");
        }
    }
}
