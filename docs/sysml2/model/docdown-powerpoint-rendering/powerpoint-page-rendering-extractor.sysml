package DocDownPowerPointRendering {
    private import SoftwareItemMetadata::*;

    part def PowerPointPageRenderingExtractor {
        doc /* The superset backend: delegates the managed aspects, rasterizes the slides, isolates per-slide faults. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.PowerPoint.Rendering/PowerPointPageRenderingExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.PowerPoint.Rendering.Tests/PowerPointPageRenderingExtractorTests.cs");
            designFiles = ("docs/design/docdown-powerpoint-rendering/powerpoint-page-rendering-extractor.md");
            verificationFiles = ("docs/verification/docdown-powerpoint-rendering/powerpoint-page-rendering-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-powerpoint-rendering/powerpoint-page-rendering-extractor.yaml");
        }
    }
}
