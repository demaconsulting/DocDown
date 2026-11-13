package DocDownPdfRendering {
    private import SoftwareItemMetadata::*;

    part def PdfRenderingDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds the page-rendering extractor to a DocDown builder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf.Rendering/PdfRenderingDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Rendering.Tests/PdfRenderingDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-pdf-rendering/pdf-rendering-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-pdf-rendering/pdf-rendering-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-pdf-rendering/pdf-rendering-doc-down-builder-extensions.yaml");
        }
    }
}
