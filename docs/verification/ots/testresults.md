## TestResults Verification

This document provides the verification evidence for the TestResults OTS software item. Requirements
for this OTS item are defined in the TestResults OTS Software Requirements document.

### Required Functionality

`DemaConsulting.TestResults` must model a test-results collection and serialize it to TRX and JUnit,
and it must represent a not-executed outcome that is distinct from a failure. Those are the only
capabilities `DocDown.Tool` relies on from it.

### Verification Approach

**TestResults is verified by transitive evidence from the `DocDown.Tool` `--validate` tests, and by
the `SelfTestAdapter` unit tests at the boundary.** This is stated explicitly because it is a
deliberate choice rather than an omission. Per the software-items standard, a dedicated OTS test
project is required only *if no other verification evidence is available*. That is not the case here:
the library is exercised directly and transitively by the tool's own `--validate` path, which builds a
`TestResults` collection from the self-test outcomes and serializes it through `TrxSerializer` and
`JUnitSerializer` to a real file the tests then re-parse. **No `test/OtsSoftwareTests/` project is
created.**

The evidence is these tests and nothing broader:

- `DocDownTool_Validate_ResultsTrx_ProducesWellFormedTrx` — runs `docdown --validate --results
  <file>.trx` in-process and re-parses the emitted file with `TrxSerializer.Deserialize`, asserting it
  carries the engine self-test cases. This is direct evidence that `TrxSerializer.Serialize` produces
  well-formed TRX for the collection the tool builds.
- `DocDownTool_Validate_ResultsXml_ProducesWellFormedJUnit` — the same for a `.xml` results file
  re-parsed with `JUnitSerializer.Deserialize`. Direct evidence that JUnit serialization is
  well-formed.
- `DocDownTool_Validate_SkippedPdfRenderCase_RecordedAsNotExecuted` — asserts the always-skipped PDF
  page-rendering case is serialized as `TestOutcome.NotExecuted` and re-parses as such, and that this
  is distinct from `TestOutcome.Failed`. Evidence that the model round-trips the one outcome the
  traceability trace matrix depends on.
- `SelfTestAdapter_ToTestOutcome_Skipped_ReturnsNotExecuted`,
  `SelfTestAdapter_ToTestResult_PreservesNameCategoryDurationAndMessage`, and the passed/failed
  mapping tests — evidence that the mapping into the `TestResults` object model is correct at the
  boundary where the tool constructs the model's values.

**The claim is deliberately narrow, and the boundaries are stated so it is not read as broader than it
is.** These tests exercise only the subset of the library the tool actually uses: a `TestResults`
collection with a `Name`, holding `TestResult` values that carry `Name`, `ClassName`, `CodeBase`,
`ComputerName`, `Duration`, `Outcome`, and `ErrorMessage`, serialized to TRX and JUnit and re-parsed.
They do **not** exercise the serializers' full schema surface, the deserialization of files produced
by other tools, or any outcome value beyond `Passed`, `Failed`, and `NotExecuted`. No claim is made
about those; only the paths `DocDown.Tool` drives on every `--validate` run are evidenced.

### A not-executed result is not evidence — recorded plainly

The trace matrix that consumes these results skips any result whose outcome is not executed, and a
requirement is reported satisfied only when it has at least one executed passing result and no
failures. A `NotExecuted` result therefore contributes neither a pass nor a fail: a requirement whose
only linked result is a skip has zero executed results and is reported **unsatisfied**, and enforcement
fails. Emitting a skip is correct and safe — it produces no false failure — but it satisfies nothing.
This is why the tool maps an unavailable self-test to `NotExecuted` rather than to a pass, and it is
the reason the distinction from `Failed` matters.

### The result-file name is load-bearing

The trace matrix filters test results by a case-insensitive substring match (`Contains`) against the
result file's **base name**, not by exact equality. So a `--validate` TRX must be named to contain the
platform token a requirement filters on: `docdown-validate-windows.trx` matches a `windows@…` filter,
`docdown-validate-ubuntu.trx` a `ubuntu@…` filter, and `docdown-validate-macos.trx` a `macos@…`
filter. The naming is a real dependency of the evidence, not a convenience, and is recorded here and
in the user guide.

### Test Environment

The evidence is produced by the standard `DocDown.Tool` test run: xUnit v3 under the .NET SDK,
targeting net10.0, across the CI operating-system matrix. `TestResults` enters the dependency graph
only through the tool, which is packaged for that single framework, so net10.0 is the full extent of
this dependency's exercised surface rather than a narrowing of it. Every results file is
written to a per-test temporary folder and re-parsed in the same test, so the evidence depends on no
committed artifact and no network access.

### Test Scenarios

#### The collection serializes to well-formed TRX and JUnit

**Tests**: `DocDownTool_Validate_ResultsTrx_ProducesWellFormedTrx`,
`DocDownTool_Validate_ResultsXml_ProducesWellFormedJUnit`

Prove the tool builds a `TestResults` collection and serializes it to a TRX and a JUnit file that each
re-parse and carry the expected results. Evidence for `DocDown-OTS-TestResults`.

#### A not-executed outcome round-trips distinct from a failure

**Tests**: `DocDownTool_Validate_SkippedPdfRenderCase_RecordedAsNotExecuted`,
`SelfTestAdapter_ToTestOutcome_Skipped_ReturnsNotExecuted`

Prove a skipped self-test is serialized and re-parsed as `NotExecuted`, distinct from `Failed`, and
that the adapter maps a skip to that outcome at the boundary. Evidence for
`DocDown-OTS-TestResults-Outcomes`.
