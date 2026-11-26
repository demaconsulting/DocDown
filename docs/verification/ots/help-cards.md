## HelpCards Verification

This document provides the verification evidence for the HelpCards OTS software item. Requirements
for this OTS item are defined in the HelpCards OTS Software Requirements document.

### Required Functionality

`DemaConsulting.HelpCards` (`DocDown-OTS-HelpCards-Cli` and `DocDown-OTS-HelpCards-Testing`) must
answer the help command of the tool from the cards embedded in the tool assembly, and report card
defects and example results to the tests of the tool. Those are the only capabilities
`DemaConsulting.DocDown.Tool` relies on from it.

### Verification Approach

HelpCards is a runtime library with no self-validation mode, so it is verified by integration test
evidence from the tests of the tool, which use the real package and the cards embedded in the tool
assembly. A dedicated OTS test project is required only where no other evidence is available, which
is not the case here.

### Test Environment

The evidence is produced by the standard `DemaConsulting.DocDown.Tool` test run: xUnit v3 under the
.NET SDK, targeting net10.0 and net11.0, across the CI operating-system matrix. The system tests run
the built tool as a separate process; the example runner works in its own scratch folders.

### Test Scenarios

#### Help command

**Tests**: `HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero`,
`DocDownTool_HelpTopics_HelpVerb_ListsTopics`

The `help` command is answered from the embedded cards, in-process and by the built tool. Evidence for
`DocDown-OTS-HelpCards-Cli`.

#### Test helpers

**Tests**: `HelpCommand_CreateOptions_ShippedCatalog_PassesValidation`,
`DocDownTool_HelpExamples_AllCommandExamples_GiveStatedResults`

The validator accepts the shipped cards, and the example runner runs every command example against the
built tool. Evidence for `DocDown-OTS-HelpCards-Testing`.
