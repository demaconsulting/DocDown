package DocDownCore {
    part def ReviewCommentsWriter {
        doc /* Finalizes review-comments.md from the reviewer comments the sink recorded, escaping inline markdown metacharacters as it writes so a reviewer's words read literally in the file while the recorded text stays exactly as the document had it, and writing the file only when the document carries any. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ReviewCommentsWriter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Output/ReviewCommentsWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output/review-comments-writer.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/review-comments-writer.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/review-comments-writer.yaml */
    }
}
