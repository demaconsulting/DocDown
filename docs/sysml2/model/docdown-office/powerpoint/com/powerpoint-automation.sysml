package DocDownOffice {
    part def PowerPointAutomation {
        doc /* The real PowerPoint COM automation adapter: the single unit that talks to Microsoft PowerPoint over late-bound IDispatch. It activates a session, opens the deck read-only, exports every slide to a PNG at the requested resolution, and tears the watchdog-bounded session down deterministically. The whole untestable COM boundary, proven in a deployed environment by release-time self-tests. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/PowerPoint/Com/PowerPointAutomation.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Com/PowerPointComExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/com/powerpoint-automation.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/com/powerpoint-automation.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/com/powerpoint-automation.yaml */
    }
}
