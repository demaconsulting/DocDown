package DocDownPdfRendering_PdfPageRenderingExtractor {
    private import SoftwareItemMetadata::*;

    part def PdfPageRenderingExtractor {
        doc /* The superset backend: delegates the managed aspects, rasterizes the pages, isolates page faults. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Pdf.Rendering/PdfPageRenderingExtractor.cs", "src/Shared/PageRendering/PageRenderingPipeline.cs");
            testFiles = ("test/DemaConsulting.DocDown.Pdf.Rendering.Tests/PdfPageRenderingExtractorTests.cs");
            designFiles = ("docs/design/docdown-pdf-rendering/pdf-page-rendering-extractor.md");
            verificationFiles = ("docs/verification/docdown-pdf-rendering/pdf-page-rendering-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-pdf-rendering/pdf-page-rendering-extractor.yaml");
        }
    }
}
