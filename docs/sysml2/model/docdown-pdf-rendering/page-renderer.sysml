package DocDownPdfRendering {
    part def PageRenderer {
        doc /* The single native-interop seam: rasterizes one page to PNG behind a process-wide lock, probes native availability. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Pdf.Rendering/PageRenderer.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Pdf.Rendering.Tests/PageRendererTests.cs */
        comment designRef /* Design: docs/design/docdown-pdf-rendering/page-renderer.md */
        comment verificationRef /* Verification: docs/verification/docdown-pdf-rendering/page-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-pdf-rendering/page-renderer.yaml */
    }
}
