### HelpCommand Verification Design

This document describes the unit-level verification strategy for `HelpCommand`, which answers the
`docdown help` command from the help cards embedded in the tool assembly.

### Verification Approach

`HelpCommand` is verified through unit tests in `Cli/HelpCommandTests.cs` in
`DemaConsulting.DocDown.Tool.Tests`, with method names beginning with `HelpCommand_`. Each test
creates a help command with `HelpCommand.Create`, which writes to in-memory writers instead of the
console. No mocking is applied: the tests use the real help cards embedded in the tool assembly and
the real HelpCards library, a documented dependency of the unit.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net10.0 and net11.0, the frameworks the tool ships on
- **Inputs**: the help cards embedded in the tool assembly; no files and no network access
- **Isolation**: each test owns its help command and its in-memory writers

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `HelpCommand` unit test run passes when the `help` command prints the topic
index, when a requested topic is printed, when the shipped catalog passes validation with warnings
treated as errors, and when every card prints in lines of at most 120 characters.

### Test Scenarios

#### The help command prints the topic index

**Test**: `HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero`

`TryRun` is called with `["help"]`; it answers with exit code 0 and the output lists the command of
every root topic. Evidence for `DocDownTool-HelpCommand-HelpVerb` and the HelpCards OTS item.

#### A requested topic is printed

**Test**: `HelpCommand_TryRun_TopicRequested_PrintsTopic`

`TryRun` is called with `["help", "command-line"]`; it answers with exit code 0 and the command-line
topic. Evidence for `DocDownTool-Cli-HelpTopics`.

#### The shipped catalog passes validation

**Test**: `HelpCommand_CreateOptions_ShippedCatalog_PassesValidation`

The catalog configuration from `CreateOptions` is validated with warnings treated as errors; no defect
is reported, which proves that the root cards are exactly `RootCardIds`, that every card carries one
purpose tag, and that every command example parses as a command line of the tool. Evidence for
`DocDownTool-HelpCommand-ValidCards` and the HelpCards OTS item.

#### Every card fits the maximum line width

**Test**: `HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth`

Every card of the shipped catalog is printed with its links and tokens expanded and a fixed, long
prerelease version in place of the build's version; each is answered with exit code 0 and has no line
longer than 120 characters. Evidence for `DocDownTool-HelpCommand-LineWidth`.
