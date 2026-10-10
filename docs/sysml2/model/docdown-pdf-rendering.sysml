package DocDownPdfRendering {
    doc
    /*
     * DemaConsulting.DocDown.Pdf.Rendering is the optional .NET library that rasterizes PDF pages to PNG
     * images for the DocDown output contract. It rasterizes through DemaConsulting.CanvasNet.Pdf,
     * a fully-managed rasterizer whose only dependency is DemaConsulting.CanvasNet, so the
     * package carries no native asset for any runtime identifier. It is a full superset
     * extractor: it delegates text, embedded-image, and metadata extraction to the managed
     * DemaConsulting.DocDown.Pdf backend and adds rendered pages, so the single-backend engine selection can
     * choose it when page rendering is requested.
     */

    part def DocDownPdfRenderingSystem {
        doc /* The DemaConsulting.DocDown.Pdf.Rendering page-rendering system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Pdf.Rendering.Tests/DocDownPdfRenderingTests.cs */
        comment designRef /* Design: docs/design/docdown-pdf-rendering.md */
        comment verificationRef /* Verification: docs/verification/docdown-pdf-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-pdf-rendering.yaml */

        part pdfPageRenderingExtractor : PdfPageRenderingExtractor;
        part pageRenderer : PageRenderer;
        part pdfRenderingDocDownBuilderExtensions : PdfRenderingDocDownBuilderExtensions;
    }
}
