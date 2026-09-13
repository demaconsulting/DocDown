package DocDownTool {
    doc
    /*
     * DocDown.Tool is the docdown command-line tool: a thin executable shell over DocDown.Core
     * and the registered extraction backends. It extracts a document into the DocDown output
     * contract, validates itself on demand, and is distributed as a .NET tool named docdown.
     */

    part def DocDownToolSystem {
        doc /* The DocDown.Tool system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Tool.Tests/DocDownToolTests.cs */
        comment designRef /* Design: docs/design/docdown-tool.md */
        comment verificationRef /* Verification: docs/verification/docdown-tool.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-tool.yaml */

        part program : Program;
        part cli : Cli;
        part selfTest : SelfTest;
    }
}
