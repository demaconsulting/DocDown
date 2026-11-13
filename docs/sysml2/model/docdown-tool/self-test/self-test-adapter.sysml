package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def SelfTestAdapter {
        doc /* Maps Core's dependency-free self-test records into the TestResults object model, mapping a skip to a not-executed outcome distinct from a failure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Tool/SelfTest/SelfTestAdapter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/SelfTest/SelfTestAdapterTests.cs");
            designFiles = ("docs/design/docdown-tool/self-test/self-test-adapter.md");
            verificationFiles = ("docs/verification/docdown-tool/self-test/self-test-adapter.md");
            requirementFiles = ("docs/reqstream/docdown-tool/self-test/self-test-adapter.yaml");
        }
    }
}
