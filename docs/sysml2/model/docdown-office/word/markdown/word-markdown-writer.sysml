package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordMarkdownWriter {
        doc /* Renders the backend-neutral document model to one markdown flow: headings, lists, inline formatting, image links, footnotes, and the Document Control section, escaping markdown metacharacters. It also renders inline runs as plain text, unescaped, for the reviewer comments that travel to the core as data. Reviewer comments are carried on the model but deliberately not rendered into the content. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/Markdown/WordMarkdownWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordMarkdownWriterTests.cs");
            designFiles = ("docs/design/docdown-office/word/markdown/word-markdown-writer.md");
            verificationFiles = ("docs/verification/docdown-office/word/markdown/word-markdown-writer.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/markdown/word-markdown-writer.yaml");
        }
    }
}
