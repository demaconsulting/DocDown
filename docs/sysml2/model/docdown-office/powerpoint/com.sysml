package DocDownOffice {
    part def PowerPointCom {
        doc /* The Com subsystem: the rendering seam, active where Microsoft PowerPoint is installed — the full-superset extractor that reuses the managed backend's content extraction and adds a rendered image of each slide over late-bound COM, the availability probe that keeps that environment-dependent backend honest, and the real automation adapter that is the single untestable COM boundary. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Com/PowerPointComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/com.yaml */

        part powerPointComExtractor : PowerPointComExtractor;
        part powerPointComAvailability : PowerPointComAvailability;
        part powerPointAutomation : PowerPointAutomation;
    }

    part def IPowerPointAutomation {
        doc /* The seam the PowerPoint COM backend renders through, so the backend is testable without PowerPoint. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/PowerPoint/Com/IPowerPointAutomation.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/com.yaml (documented as a supporting type) */
    }

    part def PowerPointComDispatch {
        doc /* The late-bound IDispatch calls the PowerPoint automation adapter makes, with its timeout and teardown. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/PowerPoint/Com/PowerPointComDispatch.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/com.yaml (documented as a supporting type) */
    }
}
