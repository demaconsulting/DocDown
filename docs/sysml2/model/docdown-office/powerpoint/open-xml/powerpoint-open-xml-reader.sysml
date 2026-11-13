package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointOpenXmlReader {
        doc /* The one place that touches the Open XML object model: turns a .pptx package into the backend-neutral deck model, walking slides in presentation order, separating the title placeholder from the body shapes, reading the notes slide's body placeholder so the narration is recovered from the file, counting embedded charts, reading each slide's legacy comments and resolving their authors through the presentation's comment-author list while leaving the modern persona comment grammar out of scope — counting those comments and the slides carrying them so their presence is reported rather than invisible — and wrapping an unopenable package as a structured failure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/PowerPoint/OpenXml/PowerPointOpenXmlReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/OpenXml/PowerPointOpenXmlReaderTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/open-xml/powerpoint-open-xml-reader.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/open-xml/powerpoint-open-xml-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/open-xml/powerpoint-open-xml-reader.yaml");
        }
    }
}
