package DocDownVisio {
    part def VisioComExtractor {
        doc /* The full-superset rendering backend: produces page names, shape text, the directed topology, and metadata by delegating to the managed Open Packaging backend with rendering suppressed, then rasterizes each foreground page to a PNG through Microsoft Visio over late-bound COM, isolating a per-page failure into a counted gap and reconciling the composed rendering facts. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Visio/Com/VisioComExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/Com/VisioComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/com/visio-com-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/com/visio-com-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/com/visio-com-extractor.yaml */
    }
}
