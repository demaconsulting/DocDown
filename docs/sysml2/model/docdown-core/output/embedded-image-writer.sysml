package DocDownCore {
    part def EmbeddedImageWriter {
        doc /* The shared sink-write pipeline of the Open XML backends: deduplicates image content, applies size limits, merges referrers, and returns the accounting each backend reports. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/EmbeddedImageWriter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Output/EmbeddedImageWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output/embedded-image-writer.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/embedded-image-writer.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/embedded-image-writer.yaml */
    }
}
