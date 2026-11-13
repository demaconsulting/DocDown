package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ImageDimensions {
        doc /* Reads pixel dimensions from PNG, JPEG, GIF and BMP headers without decoding the image. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageDimensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ImageDimensionsTests.cs");
            designFiles = ("docs/design/docdown-core/output/image-dimensions.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-dimensions.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-dimensions.yaml");
        }
    }
}
