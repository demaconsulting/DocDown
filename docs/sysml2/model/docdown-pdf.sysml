package DocDownPdf {
    doc
    /*
     * DocDown.Pdf is a .NET library that extracts text, embedded images, and document
     * metadata from PDF documents into the DocDown output contract. It is 100% managed
     * and ships no native assets, so it deliberately provides no page rendering.
     */

    part def DocDownPdfSystem {
        doc /* The DocDown.Pdf extraction system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Pdf.Tests/DocDownPdfTests.cs */
        comment designRef /* Design: docs/design/docdown-pdf.md */
        comment verificationRef /* Verification: docs/verification/docdown-pdf.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-pdf.yaml */

        part pdfDocumentExtractor : PdfDocumentExtractor;
        part pdfTextExtractor : PdfTextExtractor;
        part pdfImageExtractor : PdfImageExtractor;
        part pdfDocDownBuilderExtensions : PdfDocDownBuilderExtensions;
    }
}
