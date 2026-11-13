package DocDownTool {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Tool is the docdown command-line tool: a thin executable shell over DemaConsulting.DocDown.Core
     * and the registered extraction backends. It extracts a document into the DocDown output
     * contract, validates itself on demand, and is distributed as a .NET tool named docdown.
     */

    part def DocDownToolSystem {
        doc /* The DemaConsulting.DocDown.Tool system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Tool.Tests/DocDownToolTests.cs");
            designFiles = ("docs/design/docdown-tool.md");
            verificationFiles = ("docs/verification/docdown-tool.md");
            requirementFiles = ("docs/reqstream/docdown-tool.yaml");
        }

        part program : Program;
        part cli : Cli;
        part selfTest : SelfTest;
    }
}
