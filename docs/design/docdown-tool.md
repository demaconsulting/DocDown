# DocDown.Tool System Design

![DocDown.Tool Structure](DocDownToolView.svg)

`DocDown.Tool` is the `docdown` command-line tool. It is the first executable in the family: a thin,
honest shell over `DocDown.Core` and the registered extraction backends that turns a document into
the DocDown output contract from a terminal or a pipeline, and that validates itself on demand. It is
distributed as a RID-agnostic .NET tool invoked by the command name `docdown`.

## Architecture

The system has two subsystems and one direct unit, following the DEMA tool house pattern:

- **Program** *(direct unit)* is the entry point. It reads the parsed context, dispatches in a fixed
  priority order, prints the banner and help, builds the engine through the explicit `AddPdf` seam,
  runs an extraction and reports its outcome, exposes the auxiliary `--list-backends` and `--verify`
  commands, and translates the expected argument and operation faults into a clean, message-only,
  non-zero exit.
- **Cli** *(subsystem)* owns the command line. Its single unit, **Context**, parses and validates the
  arguments, projects the extraction flags onto the engine's options, and routes every line the tool
  emits to the console and an optional `--log` file, suppressing the console under `--silent` while
  always recording an error.
- **SelfTest** *(subsystem)* drives `--validate`. **Validation** prints an environment header, runs
  the tool's own functionality in-process and the union of self-test cases every registered backend
  contributes, and writes the outcome as TRX or JUnit. **SelfTestAdapter** maps Core's
  dependency-free self-test records into the `DemaConsulting.TestResults` object model.

### Why registration is explicit

The tool builds its engine with `new DocDownBuilder().AddPdf().Build()` — one visible call, no
reflection, no assembly scanning. This is the single fact that keeps single-file publishing and
trimming viable: a reflection-discovered backend would be invisible to the trimmer and absent from a
single-file image. The same explicit registration is what the self-validation exercises, so the
self-test union always reflects exactly what the tool actually runs.

### The dependency boundary that keeps Core clean

`DemaConsulting.TestResults` enters the dependency graph only in this tool, in the `SelfTestAdapter`
unit. Core defines its self-test contract (`ISelfValidating`, `SelfTestCase`, `SelfTestResult`,
`SelfTestStatus`) as plain records with no NuGet dependency, so it keeps its zero-runtime-dependency
posture; the adapter that turns those records into a serializable results model lives here, on the
tool side of the boundary. Neither Core nor Pdf references `DemaConsulting.TestResults`.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| Command line | Inbound, from a user or pipeline | Arguments | DEMA vocabulary plus DocDown's extraction flags |
| Standard output / error | Outbound | Text | Suppressed by `--silent`; error still sets the exit code |
| `--log` file | Outbound | Text | Opened with immediate flushing |
| `--results` file | Outbound | TRX or JUnit XML | Extension selects the serializer |
| `DocDownEngine` | Inbound, from Core | .NET API | The extraction, backend-status, and self-test surfaces |
| `DocDownBuilder` / `AddPdf` | Inbound, from Core / Pdf | .NET API | The explicit registration seam |
| `ContractVerifier` | Inbound, from Core | .NET API | Backs the `--verify` command |
| `DemaConsulting.TestResults` | Outbound, from the tool | .NET API | Referenced only here; MIT, zero deps |

## Dependencies

- **DocDown.Core** — the engine, the extraction options and result, the backend status, the contract
  verifier, and the self-test seam. See the *DocDown.Core System Design*.
- **DocDown.Pdf** — a registered extraction backend, added through `AddPdf`. See the
  *DocDown.Pdf System Design*.
- **DocDown.Pdf.Rendering** — the optional page-rendering backend, added through `AddPdfRendering`. It
  is the only reference that carries a native stack (PDFium/SkiaSharp) into the tool. See the
  *DocDown.Pdf.Rendering System Design*.
- **DemaConsulting.TestResults** (OTS) — the test-results object model and the TRX/JUnit serializers,
  referenced only by this tool. See *TestResults* under the OTS integration design.

The tool therefore carries the rendering backend's native binaries transitively, but the packaged
`.nupkg` stays **runtime-identifier-agnostic**: `PackAsTool` places every tool asset under the literal
`any` platform folder (`tools/<tfm>/any/…`), with the natives resolved from
`tools/<tfm>/any/runtimes/<rid>/native/…` at run time, so a single package installs on every RID from
one `dotnet tool install`. A self-contained single-file publish, by contrast, is RID-specific and
needs `dotnet publish -r <rid>`. This RID-agnostic property is proven against the produced package
artifact by `DocDownTool_Package_Nupkg_IsRidAgnosticDotNetTool`, not merely asserted from project
metadata.

## Risk Control Measures

- **Reflection-free registration.** The engine is built from one explicit call, so single-file and
  trimmed publishing cannot silently drop a backend, and the active backend set is a decision the
  code states rather than a deployment accident.
- **Failures are rendered, never thrown.** The engine returns every adverse condition as data with a
  display-ready explanation; the tool prints that explanation on a failed outcome and exits non-zero,
  so a batch caller never sees a stack trace and a document cannot abort a run.
- **The summary path is printed only when it exists.** `ExtractionResult.SummaryPath` is populated
  even on a scratch refusal, where it names a file that was never written, so the tool prints it only
  when the outcome is not a failure and renders the structured failure otherwise.
- **Expected faults do not surface as defects.** Argument and operation faults are caught and reduced
  to a message and a non-zero exit; any other exception is re-thrown after being written to standard
  error, so a genuine defect is still recorded by the runtime.
- **The dependency boundary is one-directional.** `DemaConsulting.TestResults` is referenced only by
  this tool, keeping Core and Pdf free of it; a build-time check confirms Core has zero runtime NuGet
  dependencies.

## Data Flow

1. `Main` builds a `Context` from the arguments — which parses and validates them and opens the log —
   then calls `Run` and returns the context's exit code, catching argument and operation faults as
   expected errors.
2. `Run` dispatches in priority order: version, then the banner and help, then `--verify`, then
   `--list-backends`, then `--validate`, then extraction — executing only the highest-priority match.
3. For an extraction, `Program` builds the engine through `AddPdf`, asks `Context` for the mapped
   options, and runs `ExtractAsync`. On success or a degraded outcome it prints the absolute path to
   `summary.txt`; on a failure it prints the structured explanation and sets a non-zero exit.
4. For `--validate`, `Validation` prints the header, runs the tool's own version and help checks
   in-process against a captured log, runs the engine's self-test union, maps each result through
   `SelfTestAdapter`, prints per-test lines, and writes the TRX or JUnit file when requested.

## Design Constraints

- **The result-file name is load-bearing.** A traceability pipeline filters test results by a
  case-insensitive substring match against the result file's base name, so a `--validate` TRX must be
  named to contain the platform token a platform requirement filters on — for example
  `docdown-validate-windows.trx`. This is a property of how the emitted file is consumed, recorded
  here and in the verification design.
- **A skipped self-test satisfies nothing.** A skip is emitted as a not-executed outcome, distinct
  from a failure. It produces no false failure, but because a traceability pipeline does not count a
  not-executed result as executed evidence, a requirement whose only linked result is a skip is
  reported unsatisfied. Emitting skips is correct and safe; it is not a substitute for a real run.
- **No option is invented that the engine cannot honor.** Every extraction flag maps onto a real
  `ExtractionOptions` member, so the command line never promises behavior the library does not
  provide.
