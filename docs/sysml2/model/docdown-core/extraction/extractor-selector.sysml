package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ExtractorSelector {
        doc /* A pure ranking function that chooses the best available extractor for a format and records a complete, auditable decision. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractorSelector.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/ExtractorSelectorTests.cs");
            designFiles = ("docs/design/docdown-core/extraction/extractor-selector.md");
            verificationFiles = ("docs/verification/docdown-core/extraction/extractor-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction/extractor-selector.yaml");
        }
    }
}
