package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordOpenXmlExtractor {
        doc /* The managed backend the engine selects and invokes: reads the document, emits content through the shared emitter as one continuous flow, and records the content inventory and the notes only it can explain. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordOpenXmlExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-open-xml-extractor.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-open-xml-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-open-xml-extractor.yaml");
        }
    }
}
