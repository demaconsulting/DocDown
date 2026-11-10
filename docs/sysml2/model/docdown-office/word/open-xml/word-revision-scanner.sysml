package DocDownOffice {
    part def WordRevisionScanner {
        doc /* Scans a Word document for tracked changes across the body, headers, footers, and footnotes, recording each change's kind, author, date, location, and a short snippet so the count, authors, and date range can be reported truthfully. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/OpenXml/WordRevisionScanner.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordTrackedChangesTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/open-xml/word-revision-scanner.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/open-xml/word-revision-scanner.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/open-xml/word-revision-scanner.yaml */
    }
}
