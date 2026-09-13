package DocDownTool {
    part def Cli {
        doc /* The Cli subsystem: parses and validates command-line arguments, maps the extraction flags onto the engine's options, and routes output to the console and an optional log. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Tool.Tests/Cli/ContextTests.cs */
        comment designRef /* Design: docs/design/docdown-tool/cli.md */
        comment verificationRef /* Verification: docs/verification/docdown-tool/cli.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-tool/cli.yaml */

        part context : Context;
    }
}
