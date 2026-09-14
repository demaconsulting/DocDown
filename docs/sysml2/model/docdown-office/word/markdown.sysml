package DocDownWord {
    part def Markdown {
        doc /* The Markdown subsystem: the backend-neutral document model and its projection onto markdown — the model renderer, the GFM table writer, and the shared content emitter that produces every output the model implies. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordMarkdownWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/markdown.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/markdown.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/markdown.yaml */

        part wordMarkdownWriter : WordMarkdownWriter;
        part wordTableWriter : WordTableWriter;
        part wordContentEmitter : WordContentEmitter;
    }
}
