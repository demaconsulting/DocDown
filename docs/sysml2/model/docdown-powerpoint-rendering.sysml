package DocDownPowerPointRendering {
    doc
    /*
     * DemaConsulting.DocDown.PowerPoint.Rendering is the optional .NET library that rasterizes PowerPoint slides
     * to PNG images for the DocDown output contract. It rasterizes through
     * DemaConsulting.CanvasNet.Pptx, a fully-managed rasterizer whose dependencies are
     * DemaConsulting.CanvasNet and DemaConsulting.CanvasNet.Charts, so the package carries no
     * native asset for any runtime identifier. It is a full superset extractor: it delegates
     * text, embedded-image, and metadata extraction to the managed DemaConsulting.DocDown.Office PowerPoint
     * backend and adds rendered slides, so the single-backend engine selection can choose it when
     * page rendering is requested.
     */

    part def DocDownPowerPointRenderingSystem {
        doc /* The DemaConsulting.DocDown.PowerPoint.Rendering slide-rendering system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Rendering.Tests/DocDownPowerPointRenderingTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint-rendering.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint-rendering.yaml */

        part powerPointPageRenderingExtractor : PowerPointPageRenderingExtractor;
        part slideRenderer : SlideRenderer;
        part powerPointRenderingDocDownBuilderExtensions : PowerPointRenderingDocDownBuilderExtensions;
    }
}
