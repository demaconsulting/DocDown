package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ExtractionSink {
        doc /* The concrete IExtractionSink; allocates paths, writes image and page bytes, buffers content and parts, and records notes and the content inventory. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ExtractionSink.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ExtractionSinkTests.cs");
            designFiles = ("docs/design/docdown-core/output/extraction-sink.md");
            verificationFiles = ("docs/verification/docdown-core/output/extraction-sink.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/extraction-sink.yaml");
        }
    }
}
