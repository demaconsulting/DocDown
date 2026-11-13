package DocDownPdfRendering {
    private import SoftwareItemMetadata::*;

    part def PageRenderer {
        doc /* Rasterizes one page to PNG through CanvasNet.Pdf, a fully-managed PDF rasterizer; reports the page count. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf.Rendering/PageRenderer.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Rendering.Tests/PageRendererTests.cs");
            designFiles = ("docs/design/docdown-pdf-rendering/page-renderer.md");
            verificationFiles = ("docs/verification/docdown-pdf-rendering/page-renderer.md");
            requirementFiles = ("docs/reqstream/docdown-pdf-rendering/page-renderer.yaml");
        }
    }
}
