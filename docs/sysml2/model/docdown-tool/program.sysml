package DocDownTool {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* The tool's entry point: priority-ordered dispatch, banner and help, explicit engine registration, extraction and reporting, and the list-backends command. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Tool/Program.cs");
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/ProgramTests.cs");
            designFiles = ("docs/design/docdown-tool/program.md");
            verificationFiles = ("docs/verification/docdown-tool/program.md");
            requirementFiles = ("docs/reqstream/docdown-tool/program.yaml");
        }
    }
}
