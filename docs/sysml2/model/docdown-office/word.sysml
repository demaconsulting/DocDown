package DocDownOffice {
    doc
    /*
     * DemaConsulting.DocDown.Word is the Word extraction subsystem of DemaConsulting.DocDown.Office: a backend that reads a
     * Word document's text, real tables, embedded images, document control, and metadata into the
     * DocDown output contract. It has two child subsystems (Markdown, OpenXml) and one direct
     * unit (WordDocDownBuilderExtensions).
     */

    part def Word {
        doc /* The DemaConsulting.DocDown.Word subsystem as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/DocDownWordTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word.yaml */

        part wordDocDownBuilderExtensions : WordDocDownBuilderExtensions;
        part markdown : WordMarkdown;
        part openXml : WordOpenXml;
    }
}
