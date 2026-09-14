## SelfTest Subsystem Verification Design

This document describes the verification strategy for the SelfTest subsystem, which drives the
`--validate` command and adapts Core's self-test records into the TestResults model.

### Verification Approach

The subsystem's two units are verified through unit tests in `SelfTest/ValidationTests.cs` and
`SelfTest/SelfTestAdapterTests.cs` in `DemaConsulting.DocDown.Tool.Tests`. The `Validation` tests
drive the `--validate` command in-process against a captured log and, where a results file is
requested, over a real emitted file; the `SelfTestAdapter` tests exercise the mapping directly.
Together they verify that a self-validation runs the tool's own functionality and the backend
self-test union, writes a well-formed results file, and records a skip as a not-executed outcome
distinct from a failure.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net10.0, the single framework the tool ships on
- **Filesystem**: a per-test `TempScratch` folder holds the requested results file; the validation
  driver creates and cleans its own temporary work folders
- **Isolation**: each test owns its captured log and results file

### Acceptance Criteria

Per IEC 62304 §5.5.2, a SelfTest subsystem test run passes when the validation driver prints an
environment header at the requested depth, runs `core.layout-invariance` and `core.manifest-schema`
together with the registered backend cases as one union, writes a TRX or JUnit results file, reports
an error for an unsupported extension, and exits zero when no case failed; and when the adapter maps
each executed status to its outcome, maps a skip to not-executed, and preserves the case name,
category, duration, and message.

### Test Scenarios

The subsystem's behavior is verified by the `Validation` and `SelfTestAdapter` unit scenarios, whose
full detail is given in the respective unit verification documents.

#### The validation driver runs the self-test union and writes results

**Tests**: `Validation_Run_RegisteredBackends_RunsCoreAndBackendSelfTestUnion`,
`Validation_Run_ResultsTrx_WritesFile`

Prove the driver runs the current Core cases together with the backend cases and writes the requested
results file. Evidence for `DocDownTool-SelfTest-Validation`.

#### A skip is mapped to not-executed

**Test**: `SelfTestAdapter_ToTestOutcome_Skipped_ReturnsNotExecuted`

Proves the adapter maps a skip to a not-executed outcome distinct from a failure. Evidence for
`DocDownTool-SelfTest-Adaptation`.
