package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def SelfTest {
        doc /* The SelfTest subsystem: drives the --validate command and adapts Core's dependency-free self-test records into the TestResults model for TRX/JUnit serialization. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/docdown-tool/self-test.md");
            verificationFiles = ("docs/verification/docdown-tool/self-test.md");
            requirementFiles = ("docs/reqstream/docdown-tool/self-test.yaml");
        }

        part validation : Validation;
        part selfTestAdapter : SelfTestAdapter;
    }
}
