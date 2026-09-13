### Validation

![DocDown.Tool Structure](DocDownToolView.svg)

#### Purpose

`Validation` is the `--validate` driver. It prints an environment header, exercises the tool's own
functionality in-process, runs the union of self-test cases every registered backend contributes,
prints per-test results, and writes the outcome as TRX or JUnit when a results file is requested.

#### Data Model

`Validation` is an `internal static class`. It holds no state; each run assembles a
`DemaConsulting.TestResults.TestResults` collection locally and disposes of the temporary work
folders it creates. A nested `TemporaryDirectory` type provides a self-cleaning scratch location for
the in-process checks and the engine self-test cases.

#### Key Methods

- **`void Run(Context context)`** — prints the header, runs the checks, computes the totals, and
  writes the results file if requested. It reports a non-zero exit through `context.WriteError` when
  any check failed or the results-file extension is unsupported; a skip is not a failure and does not
  affect the exit code.
- **`PrintValidationHeader`** (private) — emits a markdown heading at `context.HeadingDepth` and a
  table of the tool version, machine name, `RuntimeInformation.OSDescription`,
  `RuntimeInformation.FrameworkDescription`, and a UTC timestamp.
- **`RunCliTest`** (private) — runs one of the tool's own commands (`--version`, `--help`) in-process
  via `Context.Create([... "--silent", "--log", <temp>])` and `Program.Run`, then asserts on the
  captured log. This is the same silence-plus-log mechanism the reference DEMA tool uses.
- **`RunEngineSelfTests`** (private) — builds the engine with
  `new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build()`,
  enumerates `GetSelfTestCases()`, runs each case in its own work folder, maps the result through
  `SelfTestAdapter`, and prints a pass, fail, or skip line. The current Core portion of that union is
  `core.layout-invariance` and `core.manifest-schema`. A case that throws is recorded as a failure so
  the run continues and reports every case.
- **`WriteResultsFile`** (private) — selects `TrxSerializer` for a `.trx` extension and
  `JUnitSerializer` for a `.xml` extension, and reports an error for any other extension.

#### Error Handling

Every check runs inside a `try` and `catch` that records an exception as a failed result rather than
aborting the run, because a self-test exists to produce a health signal, not to throw. An unsupported
results-file extension and a results-file write failure are both reported through `WriteError`,
driving the exit code to 1.

#### Dependencies

- **Program** — run in-process to exercise the tool's own commands, and read for the `Version`
  property in the header.
- **Context** — constructed for each in-process check.
- **SelfTestAdapter** — maps each engine self-test result into the results model.
- **DocDown.Core** — the engine and the self-test types.
- **DocDown.Pdf**, **DocDown.Pdf.Rendering**, **DocDown.Word**, **DocDown.Visio**,
  **DocDown.PowerPoint**, and **DocDown.Excel** — the explicit backend registration chain the driver
  validates.
- **DemaConsulting.TestResults** — the results collection and the `TrxSerializer` and
  `JUnitSerializer`.

#### Callers

`Program.Run` invokes `Validation.Run` when `--validate` is the highest-priority match.
