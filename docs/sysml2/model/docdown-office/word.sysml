package DocDownOffice {
    doc
    /*
     * DocDown.Word is the Word extraction package: a separately distributed backend that reads a
     * Word document's text, real tables, embedded images, document control, and metadata into the
     * DocDown output contract. It has two subsystems (Markdown, OpenXml) and one direct
     * unit (WordDocDownBuilderExtensions).
     */

    part def Word {
        doc /* The DocDown.Word system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/DocDownWordTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word.yaml */

        part wordDocDownBuilderExtensions : WordDocDownBuilderExtensions;
        part markdown : WordMarkdown;
        part openXml : WordOpenXml;
    }
}
