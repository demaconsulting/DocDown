package DocDownPdf {
    private import SoftwareItemMetadata::*;

    part def PdfImageExtractor {
        doc /* Writes a PDF's embedded images, choosing an encoding per image and accounting honestly for every one it cannot deliver. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf/PdfImageExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/PdfImageExtractorTests.cs");
            designFiles = ("docs/design/docdown-pdf/pdf-image-extractor.md");
            verificationFiles = ("docs/verification/docdown-pdf/pdf-image-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-pdf/pdf-image-extractor.yaml");
        }
    }
}
