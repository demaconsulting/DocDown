package DocDownPdf {
    private import SoftwareItemMetadata::*;

    part def PdfTextExtractor {
        doc /* Renders a PDF page's glyphs into markdown paragraphs in reading order, with image links and page markers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf/PdfTextExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/PdfTextExtractorTests.cs");
            designFiles = ("docs/design/docdown-pdf/pdf-text-extractor.md");
            verificationFiles = ("docs/verification/docdown-pdf/pdf-text-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-pdf/pdf-text-extractor.yaml");
        }
    }
}
