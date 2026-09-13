package DocDownPowerPoint {
    part def Com {
        doc /* The Com subsystem: the rendering seam, active where Microsoft PowerPoint is installed — the full-superset extractor that reuses the managed backend's content extraction and adds a rendered image of each slide over late-bound COM, the availability probe that keeps that environment-dependent backend honest, and the real automation adapter that is the single untestable COM boundary. */

        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/Com/PowerPointComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/com.yaml */

        part powerPointComExtractor : PowerPointComExtractor;
        part powerPointComAvailability : PowerPointComAvailability;
        part powerPointAutomation : PowerPointAutomation;
    }
}
