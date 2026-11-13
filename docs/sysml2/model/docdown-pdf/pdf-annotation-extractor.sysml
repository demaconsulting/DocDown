package DocDownPdf {
    private import SoftwareItemMetadata::*;

    part def PdfAnnotationExtractor {
        doc /* Reads the reviewer commentary a PDF carries as annotations, applying a documented list of annotation types that count as commentary. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf/PdfAnnotationExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/PdfAnnotationExtractorTests.cs");
            designFiles = ("docs/design/docdown-pdf/pdf-annotation-extractor.md");
            verificationFiles = ("docs/verification/docdown-pdf/pdf-annotation-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-pdf/pdf-annotation-extractor.yaml");
        }
    }
}
