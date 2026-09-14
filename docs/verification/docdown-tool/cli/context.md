### Context Verification Design

This document describes the unit-level verification strategy for `Context`, which owns the parsed
command-line arguments and the tool's output channels.

### Verification Approach

`Context` is verified through unit tests in `Cli/ContextTests.cs` in
`DemaConsulting.DocDown.Tool.Tests`, with method names beginning with `Context_`. Each test constructs
a `Context` from an argument array through the `Create` factory and asserts on the result: the parsed
choices, the projected `ExtractionOptions`, the console-and-log routing, and the argument faults
raised for malformed input. The log-flush scenario reads the log with a shared file handle while the
writer is still open, which is what proves the write reached disk immediately rather than at dispose.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: temporary log files; standard output redirected to a `StringWriter` for the silence
  scenario
- **Isolation**: each test owns its arguments and any temporary log, deleting it on completion

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `Context` unit test run passes when a written line reaches the console only
when not silent and always reaches the log; when a log line is flushed immediately; when an error sets
the exit code even under silence and no error leaves it zero; when the supported extraction flags
project onto the options; when `--overwrite` maps `clean` and `overwrite` to the supported scratch
modes and rejects the removed tokens; and when an unknown argument, a malformed page range, the
removed `--images` and `--split` options, and an out-of-range heading depth are each rejected with
an argument fault.

### Test Scenarios

#### Silence suppresses the console but not the log

**Test**: `Context_WriteLine_SilentMode_SuppressesConsoleButWritesLog`

Proves a written line is absent from a redirected console under silence but present in the log.
Evidence for `DocDownTool-Context-Output`.

#### Log lines are flushed immediately

**Test**: `Context_Create_LogFile_WritesLinesWithAutoFlush`

Proves a written line is readable from the log, through a shared handle, before the context is
disposed. Evidence for `DocDownTool-Context-Logging`.

#### An error sets the exit code even under silence

**Tests**: `Context_WriteError_SilentMode_StillSetsExitCodeOne`,
`Context_ExitCode_NoErrors_ReturnsZero`

Prove the error flag is set unconditionally so a silenced failure still exits non-zero, and that a run
with no error reports success. Evidence for `DocDownTool-Context-ErrorFlag`.

#### Supported extraction flags are mapped

**Test**: `Context_BuildExtractionOptions_SupportedFlags_MapOntoOptions`

Proves each supported extraction flag projects onto its `ExtractionOptions` member. Evidence for
`DocDownTool-Context-OptionMapping`.

#### Scratch-folder policy tokens are constrained

**Tests**: `Context_BuildExtractionOptions_SupportedScratchPolicyToken_MapsToMode`,
`Context_Create_RemovedScratchPolicyToken_ThrowsArgumentException`

Prove `--overwrite` accepts only `clean` and `overwrite`, maps them to the current scratch-folder
modes, and rejects the removed tokens during parsing. Evidence for
`DocDownTool-Context-ScratchPolicy`.

#### An unknown argument is rejected

**Tests**: `Context_Create_UnknownArgument_ThrowsArgumentException`,
`Context_Create_RemovedImagesOption_ThrowsArgumentException`,
`Context_Create_RemovedSplitOption_ThrowsArgumentException`

Prove an unrecognized argument raises an argument fault, including the removed `--images` and
`--split` options, which must be rejected rather than silently ignored. Evidence for
`DocDownTool-Context-UnknownArgument`.

#### A malformed option value is rejected

**Tests**: `Context_Create_MalformedPageRange_ThrowsArgumentException`,
`Context_Create_RemovedScratchPolicyToken_ThrowsArgumentException`

Prove a page range that is not two numbers and an unrecognized scratch-policy token are each
rejected with an argument fault at parse time. Evidence for `DocDownTool-Context-OptionValidation`.

#### An out-of-range heading depth is rejected

**Test**: `Context_Create_DepthOutOfRange_ThrowsArgumentException`

Proves a heading depth outside the supported range is rejected rather than clamped. Evidence for
`DocDownTool-Context-Depth`.
