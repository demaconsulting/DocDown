package DocDownPowerPointRendering {
    private import SoftwareItemMetadata::*;

    part def PowerPointRenderingDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds the slide-rendering extractor to a DocDown builder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.PowerPoint.Rendering/PowerPointRenderingDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.PowerPoint.Rendering.Tests/PowerPointRenderingDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-powerpoint-rendering/powerpoint-rendering-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-powerpoint-rendering/powerpoint-rendering-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-powerpoint-rendering/powerpoint-rendering-doc-down-builder-extensions.yaml");
        }
    }
}
