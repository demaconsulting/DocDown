package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ImageLinkText {
        doc /* Builds the link-safe alt text of an inline markdown image link from the selected image text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageLinkText.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ImageLinkTextTests.cs");
            designFiles = ("docs/design/docdown-core/output/image-link-text.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-link-text.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-link-text.yaml");
        }
    }
}
