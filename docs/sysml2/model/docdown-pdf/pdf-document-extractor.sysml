package DocDownPdf {
    private import SoftwareItemMetadata::*;

    part def PdfDocumentExtractor {
        doc /* Orchestrates a PDF extraction: availability, metadata, delegation, the content inventory including the comment and comment-author counts taken from a single annotation pass shared with the review comments, and notes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf/PdfDocumentExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/PdfDocumentExtractorTests.cs");
            designFiles = ("docs/design/docdown-pdf/pdf-document-extractor.md");
            verificationFiles = ("docs/verification/docdown-pdf/pdf-document-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-pdf/pdf-document-extractor.yaml");
        }
    }
}
