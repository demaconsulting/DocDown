package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ManifestWriter {
        doc /* Serializes manifest.json, the machine-readable twin of the summary, from the recorded content, inventory, and notes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ManifestWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output/manifest-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/manifest-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/manifest-writer.yaml");
        }
    }
}
