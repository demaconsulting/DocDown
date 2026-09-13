## Cli Subsystem Verification Design

This document describes the verification strategy for the Cli subsystem, which parses and validates
command-line arguments, projects the extraction flags onto the engine's options, and routes output to
the console and an optional log.

### Verification Approach

The Cli subsystem is one unit, `Context`, verified through unit tests in `Cli/ContextTests.cs` in
`DemaConsulting.DocDown.Tool.Tests`, with method names beginning with `Context_`. The tests construct
a `Context` directly from an argument array and assert on the parsed choices, the projected options,
the output routing, and the argument faults raised for malformed input. No engine is involved: the
subsystem performs no extraction, so its verification is entirely about parsing, validation, mapping,
and output.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: temporary log files, opened and read with a shared handle to observe immediate
  flushing; standard output captured with `StringWriter` for the silence scenario
- **Isolation**: each test owns its arguments and any temporary log

### Acceptance Criteria

Per IEC 62304 §5.5.2, a Cli subsystem test run passes when output is suppressed on the console under
silence but still written to the log; when an error sets the exit code even under silence and a
run with no error reports success; when the extraction flags project onto the engine's options; and
when an unrecognized argument, a malformed option value, and an out-of-range heading depth are each
rejected with an argument fault.

### Test Scenarios

The subsystem's behavior is verified by the `Context` unit scenarios below.

#### Output routing under silence

**Test**: `Context_WriteLine_SilentMode_SuppressesConsoleButWritesLog`

Proves silence suppresses the console write while the log write still happens. Evidence for
`DocDownTool-Cli-Output`.

#### An error sets the exit code even under silence

**Test**: `Context_WriteError_SilentMode_StillSetsExitCodeOne`

Proves the error flag is set regardless of silence, so a silenced run that failed still exits
non-zero. Evidence for `DocDownTool-Cli-Output`.

#### Option mapping

**Test**: `Context_BuildExtractionOptions_Flags_MapOntoOptions`

Proves the extraction flags project onto the engine's options. Evidence for
`DocDownTool-Cli-OptionMapping`.

#### Argument validation

**Test**: `Context_Create_UnknownArgument_ThrowsArgumentException`

Proves an unrecognized argument is rejected with an argument fault. Evidence for
`DocDownTool-Cli-ArgumentValidation`.

The full per-scenario detail for each of these is given in the *Context Verification Design*.
