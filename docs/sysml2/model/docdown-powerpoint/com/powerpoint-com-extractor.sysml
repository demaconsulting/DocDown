package DocDownPowerPoint {
    part def PowerPointComExtractor {
        doc /* The full-superset rendering backend: produces slide text, titles, speaker notes, and metadata by delegating to the managed Open XML backend with rendering suppressed, then rasterizes each slide to a PNG through Microsoft PowerPoint over late-bound COM, recording a per-slide rasterization failure as a note. Contributes the release-time self-test cases powerpoint.com.available and powerpoint.com.render, the latter rendering a synthetic single-slide deck through the real COM adapter where Microsoft PowerPoint is present and skipping with a stated reason where it is not. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.PowerPoint/Com/PowerPointComExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/Com/PowerPointComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/com/powerpoint-com-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/com/powerpoint-com-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/com/powerpoint-com-extractor.yaml */
    }
}
