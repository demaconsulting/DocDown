package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointOpenXml {
        doc /* The OpenXml subsystem: the guaranteed managed content backend — the extractor the engine selects, the reader that turns the presentation package into the backend-neutral model preserving slide order, titles, body text, and speaker notes, and the image reader that yields each embedded image's bytes and slide association. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/OpenXml/PowerPointOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/open-xml.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/open-xml.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/open-xml.yaml");
        }

        part powerPointOpenXmlExtractor : PowerPointOpenXmlExtractor;
        part powerPointOpenXmlReader : PowerPointOpenXmlReader;
        part powerPointOpenXmlImageReader : PowerPointOpenXmlImageReader;
    }
}
