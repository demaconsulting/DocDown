package DocDownVisioRendering {
    private import SoftwareItemMetadata::*;

    part def VisioRenderingDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds the page-rendering extractor to a DocDown builder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Visio.Rendering/VisioRenderingDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Visio.Rendering.Tests/VisioRenderingDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-visio-rendering/visio-rendering-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-visio-rendering/visio-rendering-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-visio-rendering/visio-rendering-doc-down-builder-extensions.yaml");
        }
    }
}
