## SelfTest Subsystem

![DocDown.Tool Structure](DocDownToolView.svg)

The SelfTest subsystem drives the `--validate` command. It exercises the tool's own functionality and
the union of self-test cases every registered backend contributes, reports the outcome legibly, and
writes a machine-readable results file — so an operator can confirm the tool works where it is
installed, and a traceability pipeline can consume the evidence.

## Architecture

Two units divide the work along the dependency boundary that keeps Core clean:

- **Validation** is the driver. It prints an environment header at the requested heading depth, runs
  the tool's version and help commands in-process against a captured log, runs the engine's self-test
  union, prints per-test results, and writes the outcome as TRX or JUnit when a results file is
  requested.
- **SelfTestAdapter** is the mapping. It converts Core's dependency-free self-test records into the
  `DemaConsulting.TestResults` object model, mapping a skip to a not-executed outcome distinct from a
  failure.

The split exists because `DemaConsulting.TestResults` must not become a Core dependency. Core defines
its self-test contract as plain records; the adapter that turns those records into a serializable
model lives here, on the tool side, and the driver depends on the adapter rather than on Core knowing
anything about the results model.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `DocDownEngine.GetSelfTestCases` | Inbound, from Core | .NET API | The union of Core and registered-backend cases |
| `Program.Run` | Inbound, in-process | .NET API | Driven with `--silent --log` to capture the tool's own output |
| `--results` file | Outbound | TRX or JUnit XML | Extension selects the serializer; any other extension is an error |
| `DemaConsulting.TestResults` | Outbound | .NET API | The results model and the TRX/JUnit serializers |

## Design Constraints

- **A skip is not evidence.** A skipped case is emitted as a not-executed outcome. It causes no false
  failure, but because a traceability pipeline does not count a not-executed result as executed, a
  skip satisfies no requirement. Emitting skips is correct and safe; it is not a substitute for a run.
- **The results-file name matters to a downstream pipeline.** The traceability tooling filters results
  by a case-insensitive substring match against the result file's base name, so a `--validate` TRX
  should be named to contain the platform token a platform requirement filters on — for example
  `docdown-validate-windows.trx`.

## Dependencies

- **DocDown.Core** — `DocDownBuilder`, `DocDownEngine`, and the self-test seam (`SelfTestCase`,
  `SelfTestResult`, `SelfTestStatus`, `SelfTestContext`).
- **DocDown.Pdf** — `AddPdf`, so the union includes the PDF backend's cases.
- **DemaConsulting.TestResults** (OTS) — the results model and serializers, referenced only here.
