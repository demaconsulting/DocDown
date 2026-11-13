package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioOpenXmlExtractor {
        doc /* The managed backend the engine selects for a content extraction: declares the modern Visio formats and the higher priority; probes available unconditionally with no I/O and does not render pages; records its environment facts; buffers and opens the drawing; delegates reading to VisioPackageReader and emission to VisioContentEmitter; and contributes a topology round-trip self-test. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Visio/OpenXml/VisioOpenXmlExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/visio/open-xml/visio-open-xml-extractor.md");
            verificationFiles = ("docs/verification/docdown-office/visio/open-xml/visio-open-xml-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/open-xml/visio-open-xml-extractor.yaml");
        }
    }
}
