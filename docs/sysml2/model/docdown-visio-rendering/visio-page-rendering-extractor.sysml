package DocDownVisioRendering {
    private import SoftwareItemMetadata::*;

    part def VisioPageRenderingExtractor {
        doc /* The superset backend: delegates the managed aspects, rasterizes the pages, isolates per-page faults. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Visio.Rendering/VisioPageRenderingExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Visio.Rendering.Tests/VisioPageRenderingExtractorTests.cs");
            designFiles = ("docs/design/docdown-visio-rendering/visio-page-rendering-extractor.md");
            verificationFiles = ("docs/verification/docdown-visio-rendering/visio-page-rendering-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-visio-rendering/visio-page-rendering-extractor.yaml");
        }
    }
}
