### Validation Verification Design

This document describes the unit-level verification strategy for `Validation`, the `--validate`
driver.

### Verification Approach

`Validation` is verified through unit tests in `SelfTest/ValidationTests.cs` in
`DemaConsulting.DocDown.Tool.Tests`, with method names beginning with `Validation_`. Each test drives
`docdown --validate` in-process against a captured log, and — where a results file is requested — over
a real emitted file whose existence and contents are then asserted. The engine and the PDF backend are
real, so the self-test union the driver runs is exactly the one the shipped tool runs.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds the requested results file
- **Engine**: these are unit tests of the `Validation` unit, so they supply their own engine —
  the managed PDF backends only. Every property asserted here (the header and its depth, the
  results-file shapes, the pass/skip/fail accounting, the exit code) belongs to the unit rather than
  to which backends happen to be registered, so none of them needs Microsoft Office driven. The
  managed engine still produces every case shape the accounting must handle: a passing round trip,
  an always-skipped page-rendering case, and a native-stack render.
- **Isolation**: each test owns its captured log and results file, and starts no application, so
  these tests create no contention and run fully in parallel

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `Validation` unit test run passes when the header honors the requested
heading depth and reports the environment; when the run executes `core.layout-invariance` and
`core.manifest-schema` together with the backend cases as one union, showing the always-skipped
page-rendering case as a skip; when a requested TRX or JUnit results file is written as well-formed
XML carrying the named self-test cases with correct per-result outcomes and run-level counts
consistent with those results, and an unsupported extension is reported as an error; and when an
all-pass run exits zero.

### Test Scenarios

#### The header honors depth and reports the environment

**Test**: `Validation_Run_Header_HonorsDepthAndReportsEnvironment`

Proves the header is emitted at the requested heading depth and names the machine and a UTC
timestamp. Evidence for `DocDownTool-Validation-Header`.

#### The self-test union runs the current Core and PDF cases

**Test**: `Validation_Run_RegisteredBackends_RunsCoreAndBackendSelfTestUnion`

Proves the run includes `core.layout-invariance`, `core.manifest-schema`, the PDF parse round trip,
and the PDF page-rendering case shown as a skip rather than a failure. Evidence for
`DocDownTool-Validation-RunsUnion`.

#### The results file is written, and an unsupported extension is an error

**Tests**: `Validation_Run_ResultsTrx_WritesFile`,
`Validation_Run_ResultsXml_WritesWellFormedJUnit`,
`Validation_Run_UnsupportedResultsExtension_WritesError`

Prove that a requested `.trx` and `.xml` results file is written and reported, that each re-parses as
well-formed XML carrying the named self-test cases, that a passing case is recorded as passed and the
always-skipped `pdf.pageRendering` case as not-executed and not as a failure, and that the run-level
counts in the file are consistent with the individual results; and that a `.json` results file is
rejected as an unsupported format with a non-zero exit. Evidence for
`DocDownTool-Validation-WritesResults`.

#### An all-pass run exits zero

**Test**: `Validation_Run_AllPass_ExitCodeZero`

Proves a run whose only non-passing result is a benign skip exits zero and reports no failure.
Evidence for `DocDownTool-Validation-Outcome`.
