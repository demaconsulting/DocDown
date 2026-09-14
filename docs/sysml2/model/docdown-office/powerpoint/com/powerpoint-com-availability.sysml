package DocDownOffice {
    part def PowerPointComAvailability {
        doc /* Probes whether the PowerPoint COM automation backend can run: checks the operating system first and then, on Windows, whether the PowerPoint.Application ProgID is registered. Opens no deck, launches no application, and never throws; its unavailable reasons are declarative and instruct no installation. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/PowerPoint/Com/PowerPointComAvailability.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Com/PowerPointComAvailabilityTests.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/com/powerpoint-com-availability.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/com/powerpoint-com-availability.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/com/powerpoint-com-availability.yaml */
    }
}
