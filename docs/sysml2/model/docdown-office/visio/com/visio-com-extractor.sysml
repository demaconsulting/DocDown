package DocDownVisio {
    part def VisioComExtractor {
        doc /* The full-superset rendering backend: produces page names, shape text, the directed topology, and metadata by delegating to the managed Open Packaging backend with rendering suppressed, then rasterizes each foreground page to a PNG through Microsoft Visio over late-bound COM, recording a per-page rasterization failure as a note. Contributes the release-time self-test cases visio.com.available and visio.com.render, the latter rendering a synthetic single-page drawing through the real COM adapter where Microsoft Visio is present and skipping with a stated reason where it is not. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/Com/VisioComExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/Com/VisioComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/com/visio-com-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/com/visio-com-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/com/visio-com-extractor.yaml */
    }
}
