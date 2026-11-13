package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def Cli {
        doc /* The Cli subsystem: parses and validates command-line arguments, maps the extraction flags onto the engine's options, and routes output to the console and an optional log. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/docdown-tool/cli.md");
            verificationFiles = ("docs/verification/docdown-tool/cli.md");
            requirementFiles = ("docs/reqstream/docdown-tool/cli.yaml");
        }

        part context : Context;
    }
}
