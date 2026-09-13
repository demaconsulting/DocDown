package DocDownVisio {
    part def VisioComAvailability {
        doc /* The rendering-availability probe: checks the operating system first and then, on Windows, whether the Visio.Application ProgID is registered — a cheap, side-effect-free, non-throwing yes-or-no with a declarative reason that never instructs an installation. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Visio/Com/VisioComAvailability.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/Com/VisioComAvailabilityTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/com/visio-com-availability.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/com/visio-com-availability.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/com/visio-com-availability.yaml */
    }
}
