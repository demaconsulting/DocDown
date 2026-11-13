package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ImageTextSelector {
        doc /* The shared, format-agnostic policy that ranks image text candidates, discards empty and auto-generated names, and reports how confidently the chosen text describes the picture. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ImageTextSelectorTests.cs");
            designFiles = ("docs/design/docdown-core/output/image-text-selector.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-text-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-text-selector.yaml");
        }
    }
}
