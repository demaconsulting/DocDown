package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordTocDetector {
        doc /* Finds the paragraphs of a Word table of contents (content control, contents styles, or TOC field) so the reader can omit them from content.md and report the omission while their tracked changes are still counted. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordTocDetector.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordTrackedChangesTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-toc-detector.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-toc-detector.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-toc-detector.yaml");
        }
    }
}
