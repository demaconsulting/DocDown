package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Owns the parsed command-line arguments and the tool's output channels: option mapping and silence-aware console and log routing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Tool/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/docdown-tool/cli/context.md");
            verificationFiles = ("docs/verification/docdown-tool/cli/context.md");
            requirementFiles = ("docs/reqstream/docdown-tool/cli/context.yaml");
        }
    }
}
