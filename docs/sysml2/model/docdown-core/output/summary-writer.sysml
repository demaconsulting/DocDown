package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def SummaryWriter {
        doc /* Serializes the human-readable summary.txt. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/SummaryWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/SummaryWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output/summary-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/summary-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/summary-writer.yaml");
        }
    }
}
