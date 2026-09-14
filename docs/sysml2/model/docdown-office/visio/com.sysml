package DocDownOffice {
    part def VisioCom {
        doc /* The Com subsystem: the rendering seam, active where Microsoft Visio is installed — the full-superset extractor that reuses the managed backend's page-name, shape-text, and directed-topology extraction and adds a rendered image of each page over late-bound COM, the availability probe that keeps that environment-dependent backend honest, and the real automation adapter that is the single untestable COM boundary. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/Com/VisioComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/com.yaml */

        part visioComExtractor : VisioComExtractor;
        part visioComAvailability : VisioComAvailability;
        part visioAutomation : VisioAutomation;
    }

    part def IVisioAutomation {
        doc /* The seam the Visio COM backend renders through, so the backend is testable without Visio. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/Com/IVisioAutomation.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/com.yaml (documented as a supporting type) */
    }

    part def VisioComDispatch {
        doc /* The late-bound IDispatch calls the Visio automation adapter makes, with its timeout and teardown. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/Com/VisioComDispatch.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/com.yaml (documented as a supporting type) */
    }
}
