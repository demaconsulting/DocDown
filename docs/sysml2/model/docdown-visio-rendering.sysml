package DocDownVisioRendering {
    doc
    /*
     * DemaConsulting.DocDown.Visio.Rendering is the optional .NET library that rasterizes Visio pages
     * to PNG images for the DocDown output contract. It rasterizes through
     * DemaConsulting.CanvasNet.Vsdx, a fully-managed rasterizer whose only dependency is
     * DemaConsulting.CanvasNet, so the package carries no native asset for any runtime identifier.
     * It is a full superset extractor: it delegates page-name, shape-text, directed-topology, and
     * metadata extraction to the managed DemaConsulting.DocDown.Office Visio backend and adds
     * rendered pages, so the single-backend engine selection can choose it when page rendering is
     * requested.
     */

    part def DocDownVisioRenderingSystem {
        doc /* The DemaConsulting.DocDown.Visio.Rendering page-rendering system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Rendering.Tests/DocDownVisioRenderingTests.cs */
        comment designRef /* Design: docs/design/docdown-visio-rendering.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio-rendering.yaml */

        part visioPageRenderingExtractor : VisioPageRenderingExtractor;
        part pageRenderer : PageRenderer;
        part visioRenderingDocDownBuilderExtensions : VisioRenderingDocDownBuilderExtensions;
    }
}
