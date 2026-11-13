package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordTableWriter {
        doc /* Renders a table model as a GitHub-Flavored-Markdown table: header and delimiter rows, padded short rows, escaped cell content, skipped empty tables, and flattened merged and nested cells with a flatten count. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/Markdown/WordTableWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordTableWriterTests.cs");
            designFiles = ("docs/design/docdown-office/word/markdown/word-table-writer.md");
            verificationFiles = ("docs/verification/docdown-office/word/markdown/word-table-writer.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/markdown/word-table-writer.yaml");
        }
    }
}
