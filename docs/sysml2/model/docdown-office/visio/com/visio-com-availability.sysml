package DocDownOffice {
    part def VisioComAvailability {
        doc /* The rendering-availability probe: checks the operating system first and then, on Windows, whether the Visio.Application ProgID is registered — a cheap, side-effect-free, non-throwing yes-or-no with a declarative reason that never instructs an installation. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/Com/VisioComAvailability.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/Com/VisioComAvailabilityTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/com/visio-com-availability.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/com/visio-com-availability.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/com/visio-com-availability.yaml */
    }
}
