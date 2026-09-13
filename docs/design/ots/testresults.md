## TestResults

### Purpose

`DemaConsulting.TestResults` is the library the `docdown` tool uses to build a test-results collection
and serialize it to TRX and JUnit for the `--validate` command. It was chosen because it is the DEMA
house library for exactly this task — every other DEMA tool emits its self-validation results through
it — and because it is a lightweight, fully managed, MIT-licensed package with no transitive runtime
dependencies, so it adds nothing to the tool's dependency graph beyond itself.

It is the one runtime OTS dependency of `DocDown.Tool`, and it is referenced by no other package:
`DocDown.Core` and `DocDown.Pdf` are free of it, which is what preserves Core's zero-runtime-NuGet
-dependency posture. MIT is compatible with this repository's MIT license.

### Features Used

- `DemaConsulting.TestResults.TestResults` — the named collection of results.
- `DemaConsulting.TestResults.TestResult` — the per-test record, of which the tool sets `Name`,
  `ClassName`, `CodeBase`, `ComputerName`, `Duration`, `Outcome`, and `ErrorMessage`.
- `DemaConsulting.TestResults.TestOutcome` — the outcome enumeration, of which the tool uses
  `Passed`, `Failed`, and `NotExecuted`.
- `DemaConsulting.TestResults.IO.TrxSerializer.Serialize` — TRX serialization for a `.trx` results
  file.
- `DemaConsulting.TestResults.IO.JUnitSerializer.Serialize` — JUnit serialization for a `.xml`
  results file.

The `Deserialize` counterparts on both serializers are used only by the tool's tests, to re-parse an
emitted file and confirm it is well-formed; the tool itself only serializes.

### Integration Pattern

`DemaConsulting.TestResults` is referenced as a real runtime dependency of the `DocDown.Tool` package
and would flow to a consumer that referenced the tool as a library — but the tool is a distributed
executable, not a library others build on, so in practice the dependency stays inside the tool.
Its usage is a stateless build-and-serialize sequence per `--validate` run: the `SelfTestAdapter`
unit maps each self-test result into a `TestResult`, the `Validation` unit collects them into a
`TestResults`, and one serializer call turns the collection into TRX or JUnit text that is written to
the requested file. There is no global initialization and no state retained between runs.

**The dependency boundary is deliberate and one-directional.** The mapping into this library's types
lives entirely in the tool's `SelfTestAdapter`, so Core's self-test seam stays dependency-free and
neither Core nor Pdf gains a reference to `DemaConsulting.TestResults`. This is what lets Core keep
its zero-runtime-dependency guarantee while the tool still emits standard results files.

**The not-executed outcome is the reason the mapping is not trivial.** A skipped self-test is mapped
to `TestOutcome.NotExecuted`, which this library models as an outcome distinct from `Failed`. That
distinction is what a downstream traceability pipeline relies on: a not-executed result is not counted
as executed evidence, so a benign skip produces no false failure and also satisfies no requirement.

**The result-file name is load-bearing downstream.** The traceability tooling filters results by a
case-insensitive substring match against the result file's base name, so a `--validate` TRX is named
to contain the platform token a requirement filters on — for example `docdown-validate-windows.trx`.
This is a property of how the emitted file is consumed rather than of the library itself, and is
recorded here and in the tool's verification design.

**No native assets.** The package is fully managed and carries no native binary, so it imposes no
runtime-identifier constraint on the tool.
