package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ExtractorRegistry {
        doc /* The immutable snapshot of registered extractors, their descriptors, and their cached availability. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractorRegistry.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/ExtractorRegistryTests.cs");
            designFiles = ("docs/design/docdown-core/extraction/extractor-registry.md");
            verificationFiles = ("docs/verification/docdown-core/extraction/extractor-registry.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction/extractor-registry.yaml");
        }
    }
}
