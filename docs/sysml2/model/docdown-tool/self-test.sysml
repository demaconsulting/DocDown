package DocDownTool {
    part def SelfTest {
        doc /* The SelfTest subsystem: drives the --validate command and adapts Core's dependency-free self-test records into the TestResults model for TRX/JUnit serialization. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Tool.Tests/SelfTest/ValidationTests.cs */
        comment designRef /* Design: docs/design/docdown-tool/self-test.md */
        comment verificationRef /* Verification: docs/verification/docdown-tool/self-test.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-tool/self-test.yaml */

        part validation : Validation;
        part selfTestAdapter : SelfTestAdapter;
    }
}
