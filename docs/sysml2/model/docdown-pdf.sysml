package DocDownPdf {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Pdf is a .NET library that extracts text, embedded images, document
     * metadata, and reviewer annotations from PDF documents into the DocDown output
     * contract. It is 100% managed and ships no native assets, so it deliberately
     * provides no page rendering.
     */

    part def DocDownPdfSystem {
        doc /* The DemaConsulting.DocDown.Pdf extraction system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/DocDownPdfTests.cs");
            designFiles = ("docs/design/docdown-pdf.md");
            verificationFiles = ("docs/verification/docdown-pdf.md");
            requirementFiles = ("docs/reqstream/docdown-pdf.yaml");
        }

        part pdfDocumentExtractor : PdfDocumentExtractor;
        part pdfTextExtractor : PdfTextExtractor;
        part pdfImageExtractor : PdfImageExtractor;
        part pdfAnnotationExtractor : PdfAnnotationExtractor;
        part pdfDocDownBuilderExtensions : PdfDocDownBuilderExtensions;
    }
}
