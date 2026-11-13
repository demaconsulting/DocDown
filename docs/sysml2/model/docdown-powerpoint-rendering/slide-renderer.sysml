package DocDownPowerPointRendering {
    private import SoftwareItemMetadata::*;

    part def SlideRenderer {
        doc /* Rasterizes one slide to PNG through CanvasNet.Pptx, a fully-managed PowerPoint rasterizer; reports the slide count. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.PowerPoint.Rendering/SlideRenderer.cs");
            testFiles = ("test/DemaConsulting.DocDown.PowerPoint.Rendering.Tests/SlideRendererTests.cs");
            designFiles = ("docs/design/docdown-powerpoint-rendering/slide-renderer.md");
            verificationFiles = ("docs/verification/docdown-powerpoint-rendering/slide-renderer.md");
            requirementFiles = ("docs/reqstream/docdown-powerpoint-rendering/slide-renderer.yaml");
        }
    }
}
