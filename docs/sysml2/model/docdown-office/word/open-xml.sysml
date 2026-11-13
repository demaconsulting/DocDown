package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordOpenXml {
        doc /* The OpenXml subsystem: the managed backend — the extractor the engine selects, the reader that turns the Open XML DOM into the backend-neutral model, the scanner that finds tracked changes, and the image reader that yields each embedded image's bytes and provenance. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml.yaml");
        }

        part wordOpenXmlExtractor : WordOpenXmlExtractor;
        part wordOpenXmlReader : WordOpenXmlReader;
        part wordRevisionScanner : WordRevisionScanner;
        part wordTocDetector : WordTocDetector;
        part wordSymbolMap : WordSymbolMap;
        part wordOpenXmlImageReader : WordOpenXmlImageReader;
    }
}
