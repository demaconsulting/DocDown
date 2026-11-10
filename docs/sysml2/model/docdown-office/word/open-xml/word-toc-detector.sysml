package DocDownOffice {
    part def WordTocDetector {
        doc /* Finds the paragraphs of a Word table of contents (content control, contents styles, or TOC field) so the reader can omit them from content.md and report the omission while their tracked changes are still counted. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/OpenXml/WordTocDetector.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordTrackedChangesTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/open-xml/word-toc-detector.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/open-xml/word-toc-detector.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/open-xml/word-toc-detector.yaml */
    }
}
