package DocDownPdf {
    private import SoftwareItemMetadata::*;

    part def PdfDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds the PDF extractor to a DocDown builder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf/PdfDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Tests/PdfDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-pdf/pdf-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-pdf/pdf-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-pdf/pdf-doc-down-builder-extensions.yaml");
        }
    }
}
