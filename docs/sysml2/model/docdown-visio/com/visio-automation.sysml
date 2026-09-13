package DocDownVisio {
    part def VisioAutomation {
        doc /* The real COM automation adapter: the single unit that talks to Microsoft Visio over late-bound IDispatch, opening a drawing read-only, exporting each foreground page to a PNG at the requested resolution, and releasing its session on every path. It is the whole untestable COM boundary — deliberately mechanical, holding no extraction or note policy — proven by release-time self-tests rather than by CI. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Visio/Com/VisioAutomation.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/Com/VisioComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/com/visio-automation.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/com/visio-automation.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/com/visio-automation.yaml */
    }
}
