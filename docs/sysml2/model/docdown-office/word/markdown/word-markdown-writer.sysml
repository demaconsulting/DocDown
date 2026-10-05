package DocDownOffice {
    part def WordMarkdownWriter {
        doc /* Renders the backend-neutral document model to one markdown flow: headings, lists, inline formatting, image links, footnotes, and the Document Control section, escaping markdown metacharacters. It also renders inline runs as plain text, unescaped, for the reviewer comments that travel to the core as data. Reviewer comments are carried on the model but deliberately not rendered into the content. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/Markdown/WordMarkdownWriter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordMarkdownWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/markdown/word-markdown-writer.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/markdown/word-markdown-writer.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/markdown/word-markdown-writer.yaml */
    }
}
