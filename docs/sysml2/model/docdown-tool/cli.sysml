package DocDownTool_CliSubsystem {
    public import DocDownTool_Context::*;
    public import DocDownTool_HelpCommand::*;
    private import SoftwareItemMetadata::*;

    part def Cli {
        doc /* The Cli subsystem: parses and validates command-line arguments, maps the extraction flags onto the engine's options, routes output to the console and an optional log, and answers the built-in help command. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/Cli/ContextTests.cs", "test/DemaConsulting.DocDown.Tool.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/docdown-tool/cli.md");
            verificationFiles = ("docs/verification/docdown-tool/cli.md");
            requirementFiles = ("docs/reqstream/docdown-tool/cli.yaml");
        }

        part context : Context;
        part helpCommand : HelpCommand;
    }
}
