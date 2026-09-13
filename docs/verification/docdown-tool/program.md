## Program Verification Design

This document describes the unit-level verification strategy for `Program`, the tool's entry point and
priority-ordered dispatcher.

### Verification Approach

`Program` is verified through unit tests in `ProgramTests.cs` in `DemaConsulting.DocDown.Tool.Tests`,
with method names beginning with `Program_`. The unit is driven through its own public surface —
`Program.Run` for the dispatch and command paths, and `Program.Main` for the exit-code and
error-handling paths — with `--silent --log` capturing the output the tests assert on. The engine and
the PDF backend are real, not mocked, so the extraction and list-backends paths exercise the same code
the shipped tool runs.

The `catch (Exception)` re-throw in `Main` is a defensive branch: the engine returns every adverse
condition as data, so no ordinary input reaches it, and it carries no requirement of its own. It is
documented here rather than tested, consistent with the standard's allowance for defensive code
without a linked requirement.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a PDF generated at test time; standard output and error captured with `StringWriter`
  for the `Main` paths
- **Isolation**: each test owns its temporary folder and captured log

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `Program` unit test run passes when the version and help commands print their
expected output; when dispatch is priority-ordered so a higher-priority flag wins; when an extraction
prints `Extraction produced the output layout.` and the absolute summary path; when an unreadable
input writes the failure explanation; when a missing required option and an unrecognized argument are
treated as expected errors with a non-zero exit and no stack trace; when a log file that cannot be
opened is likewise an expected error; and when `--list-backends` reports the PDF backend as an
identifier and display name line followed by `formats:` and `status:` lines, with no capabilities
line.

### Test Scenarios

#### The version command prints the informational version

**Test**: `Program_Run_VersionFlag_WritesInformationalVersion`

Proves the version query prints the assembly's informational version. Evidence for
`DocDownTool-Program-Version`.

#### The help command prints usage and options

**Test**: `Program_Run_HelpFlag_WritesUsageAndOptions`

Proves the help text names the usage line, the options heading, and at least the `--input` option, so
the interface is discoverable. Evidence for `DocDownTool-Program-Help`.

#### Dispatch is priority-ordered

**Test**: `Program_Run_PriorityOrder_VersionBeatsOtherCommands`

Proves that with version, help, and validate all requested, only the version prints — neither the help
text nor the banner appears — so a flag combination has a deterministic outcome. Evidence for
`DocDownTool-Program-PriorityDispatch`.

#### A produced extraction prints the summary path

**Test**: `Program_Run_Extraction_PrintsAbsoluteSummaryPath`

Proves the extraction reports that the output layout was produced, then prints a line that is a fully
qualified path ending in `summary.txt`, that the named file exists on disk, and that it lies under the
scratch folder the run targeted; the process exits zero. The asserted path is read from the tool's
output, not recomputed. Evidence for `DocDownTool-Program-Extraction`.

#### An unreadable input writes the explanation

**Test**: `Program_Run_UnreadableInput_RendersStructuredFailure`

Proves a missing document writes the engine's explanation and exits non-zero without throwing.
Evidence for `DocDownTool-Program-StructuredFailure`.

#### A missing required option is an expected error

**Test**: `Program_Run_MissingInput_ThrowsArgumentException`

Proves an extraction with no `--input` raises an argument fault. This is the argument-fault half of
the expected-error contract; it has no separate requirement and supports the expected-error scenarios.

#### Explicit registration

**Test**: `DocDownTool_Build_DefaultEngine_RegistersBackendsExplicitly`

The engine the tool builds registers the expected backend set through the explicit builder seams.
Evidence for `DocDownTool-Program-ExplicitRegistration` (shared with the system-level scenario).

#### Expected errors exit non-zero without a stack trace

**Tests**: `Program_Main_UnknownArgument_WritesErrorAndReturnsOneWithoutStackTrace`,
`Program_Main_LogOpenFailure_ReturnsOneWithoutStackTrace`

Prove that an unrecognized argument and a log file that cannot be opened each make `Main` return 1
with a message on standard error and no stack trace — the two expected error classes. Evidence for
`DocDownTool-Program-ExpectedErrors`.

#### Registered backends are listed

**Test**: `Program_Run_ListBackends_ListsPdf`

Proves `--list-backends` names the registered-backends heading, omits a capabilities line, and prints
the expected identity, formats, and status lines for the PDF backend. Evidence for
`DocDownTool-Program-BackendInventory`.
