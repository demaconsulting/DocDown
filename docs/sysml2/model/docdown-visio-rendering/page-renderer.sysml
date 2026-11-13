package DocDownVisioRendering {
    private import SoftwareItemMetadata::*;

    part def PageRenderer {
        doc /* Rasterizes one page to PNG through CanvasNet.Vsdx, a fully-managed Visio rasterizer; reports the page count. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Visio.Rendering/PageRenderer.cs");
            testFiles = ("test/DemaConsulting.DocDown.Visio.Rendering.Tests/PageRendererTests.cs");
            designFiles = ("docs/design/docdown-visio-rendering/page-renderer.md");
            verificationFiles = ("docs/verification/docdown-visio-rendering/page-renderer.md");
            requirementFiles = ("docs/reqstream/docdown-visio-rendering/page-renderer.yaml");
        }
    }
}
