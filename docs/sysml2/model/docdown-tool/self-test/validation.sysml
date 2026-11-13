package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* The --validate driver: environment header, in-process command checks, the registered-backend self-test union, per-test results, and TRX/JUnit output. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Tool/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/docdown-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/docdown-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/docdown-tool/self-test/validation.yaml");
        }
    }
}
