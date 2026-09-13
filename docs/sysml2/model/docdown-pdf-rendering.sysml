package DocDownPdfRendering {
    doc
    /*
     * DocDown.Pdf.Rendering is the optional .NET library that rasterizes PDF pages to PNG
     * images for the DocDown output contract. It is the first and only DocDown package that
     * carries native binaries (PDFtoImage, wrapping PDFium and SkiaSharp). It is a full
     * superset extractor: it delegates text, embedded-image, and metadata extraction to the
     * managed DocDown.Pdf backend and adds rendered pages, so the single-backend engine
     * selection can choose it when page rendering is requested.
     */

    part def DocDownPdfRenderingSystem {
        doc /* The DocDown.Pdf.Rendering page-rendering system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Pdf.Rendering.Tests/DocDownPdfRenderingTests.cs */
        comment designRef /* Design: docs/design/docdown-pdf-rendering.md */
        comment verificationRef /* Verification: docs/verification/docdown-pdf-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-pdf-rendering.yaml */

        part pdfPageRenderingExtractor : PdfPageRenderingExtractor;
        part pageRenderer : PageRenderer;
        part pdfRenderingDocDownBuilderExtensions : PdfRenderingDocDownBuilderExtensions;
    }
}
