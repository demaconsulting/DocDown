package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ContentWriter {
        doc /* Finalizes content.md and any parts/ files, choosing between the single-flow and part-index layouts. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ContentWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ContentWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output/content-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/content-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/content-writer.yaml");
        }
    }
}
