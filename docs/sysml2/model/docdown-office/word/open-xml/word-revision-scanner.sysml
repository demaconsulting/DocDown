package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordRevisionScanner {
        doc /* Scans a Word document for tracked changes across the body, headers, footers, and footnotes, recording each change's kind, author, date, location, and a short snippet so the count, authors, and date range can be reported truthfully. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordRevisionScanner.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordTrackedChangesTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-revision-scanner.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-revision-scanner.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-revision-scanner.yaml");
        }
    }
}
