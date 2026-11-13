package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def DocDownEngine {
        doc /* The public facade that runs the fixed extraction pipeline end to end and returns a structured result rather than throwing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocDownEngine.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/DocDownEngineTests.cs");
            designFiles = ("docs/design/docdown-core/extraction/doc-down-engine.md");
            verificationFiles = ("docs/verification/docdown-core/extraction/doc-down-engine.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction/doc-down-engine.yaml");
        }
    }
}
