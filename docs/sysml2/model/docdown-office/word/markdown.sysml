package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordMarkdown {
        doc /* The Markdown subsystem: the backend-neutral document model and its projection onto markdown — the model renderer, the GFM table writer, and the shared content emitter that produces every output the model implies. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordMarkdownWriterTests.cs");
            designFiles = ("docs/design/docdown-office/word/markdown.md");
            verificationFiles = ("docs/verification/docdown-office/word/markdown.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/markdown.yaml");
        }

        part wordMarkdownWriter : WordMarkdownWriter;
        part wordTableWriter : WordTableWriter;
        part wordContentEmitter : WordContentEmitter;
    }

    part def WordDocumentModel {
        doc /* The reader-neutral model of a Word document: its blocks, inlines, lists, and tables (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/Markdown/WordDocumentModel.cs");
            designFiles = ("docs/design/docdown-office/word/markdown.md");
            verificationFiles = ("docs/verification/docdown-office/word/markdown.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/markdown.yaml");
        }
    }
}
