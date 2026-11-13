package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def EmbeddedImageWriter {
        doc /* The shared sink-write pipeline of the Open XML backends: deduplicates image content, applies size limits, merges referrers, and returns the accounting each backend reports. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/EmbeddedImageWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/EmbeddedImageWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output/embedded-image-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/embedded-image-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/embedded-image-writer.yaml");
        }
    }
}
