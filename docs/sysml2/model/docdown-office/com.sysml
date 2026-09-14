package DocDownOffice {
    part def OfficeCom {
        doc /* The helpers the COM automation backends share: the availability probe, and the composition that keeps a delegated run's rendering statement honest. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Com/PowerPointComAvailabilityTests.cs */
        comment designRef /* Design: docs/design/docdown-office/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/com.yaml */

        part officeComAvailability : OfficeComAvailability;
        part composingDelegatedSink : ComposingDelegatedSink;
        part delegatedExtractionContext : DelegatedExtractionContext;
    }

    part def OfficeComAvailability {
        doc /* Probes whether a named Office application's COM automation can run here, without launching it. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Com/OfficeComAvailability.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Com/PowerPointComAvailabilityTests.cs */
        comment designRef /* Design: docs/design/docdown-office/com.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/com.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/com.yaml */
    }

    part def ComposingDelegatedSink {
        doc /* Forwards a delegated managed run's output while suppressing its "rendering not provided" fact, which the COM run contradicts by rendering. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Com/ComposingDelegatedSink.cs */
        comment designRef /* Design: docs/design/docdown-office/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/com.yaml (documented as a supporting type) */
    }

    part def DelegatedExtractionContext {
        doc /* The context a COM backend hands its managed counterpart: page rendering off, output composed. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Com/DelegatedExtractionContext.cs */
        comment designRef /* Design: docs/design/docdown-office/com.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-office/com.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/com.yaml (documented as a supporting type) */
    }
}
