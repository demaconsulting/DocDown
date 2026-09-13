package DocDownVisio {
    part def Com {
        doc /* The Com subsystem: the rendering seam, active where Microsoft Visio is installed — the full-superset extractor that reuses the managed backend's page-name, shape-text, and directed-topology extraction and adds a rendered image of each page over late-bound COM, the availability probe that keeps that environment-dependent backend honest, and the real automation adapter that is the single untestable COM boundary. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/Com/VisioComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/com.yaml */

        part visioComExtractor : VisioComExtractor;
        part visioComAvailability : VisioComAvailability;
        part visioAutomation : VisioAutomation;
    }
}
