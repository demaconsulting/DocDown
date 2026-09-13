package DocDownWord {
    doc
    /*
     * DocDown.Word is the Word extraction package: a separately distributed backend that reads a
     * Word document's text, real tables, embedded images, document control, and metadata into the
     * DocDown output contract. It has two subsystems (Markdown, OpenXml) and one direct
     * unit (WordDocDownBuilderExtensions).
     */

    part def DocDownWordSystem {
        doc /* The DocDown.Word system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Word.Tests/DocDownWordTests.cs */
        comment designRef /* Design: docs/design/docdown-word.md */
        comment verificationRef /* Verification: docs/verification/docdown-word.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-word.yaml */

        part wordDocDownBuilderExtensions : WordDocDownBuilderExtensions;
        part markdown : Markdown;
        part openXml : OpenXml;
    }
}
