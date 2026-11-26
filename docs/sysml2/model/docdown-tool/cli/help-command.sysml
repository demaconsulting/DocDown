package DocDownTool_HelpCommand {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    part def HelpCommand {
        doc /* Built-in help command: answers the docdown help command from the help cards embedded in the assembly. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Tool/Cli/HelpCommand.cs");
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/docdown-tool/cli/help-command.md");
            verificationFiles = ("docs/verification/docdown-tool/cli/help-command.md");
            requirementFiles = ("docs/reqstream/docdown-tool/cli/help-command.yaml");
        }
    }

    // Serves the help command from the cards embedded in the assembly
    dependency HelpCommand to HelpCards;
}
