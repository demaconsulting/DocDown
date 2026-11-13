package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ReviewCommentsWriter {
        doc /* Finalizes review-comments.md from the reviewer comments the sink recorded, escaping inline markdown metacharacters as it writes so a reviewer's words read literally in the file while the recorded text stays exactly as the document had it, and writing the file only when the document carries any. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ReviewCommentsWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ReviewCommentsWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output/review-comments-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/review-comments-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/review-comments-writer.yaml");
        }
    }
}
