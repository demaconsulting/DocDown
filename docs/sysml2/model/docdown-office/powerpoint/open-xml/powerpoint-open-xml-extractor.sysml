package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointOpenXmlExtractor {
        doc /* The managed backend the engine selects and invokes for a .pptx content extraction: declares its priority, declares that this extractor does not provide rendering, probes unconditionally, buffers and opens the deck, reads it into the model, delegates emission to the shared emitter, and contributes the self-tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/PowerPoint/OpenXml/PowerPointOpenXmlExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/OpenXml/PowerPointOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/open-xml/powerpoint-open-xml-extractor.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/open-xml/powerpoint-open-xml-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/open-xml/powerpoint-open-xml-extractor.yaml");
        }
    }
}
