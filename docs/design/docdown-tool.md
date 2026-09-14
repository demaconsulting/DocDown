# DocDown.Tool System Design

![DocDown.Tool Structure](DocDownToolView.svg)

`DocDown.Tool` is the `docdown` command-line tool. It is the first executable in the family: a thin,
honest shell over `DocDown.Core` and the registered extraction backends that turns a document into
the DocDown output layout from a terminal or a pipeline, and that validates itself on demand. It is
distributed as a RID-agnostic .NET tool invoked by the command name `docdown`.

## Architecture

The system has two subsystems and one direct unit, following the DEMA tool house pattern:

- **Program** *(direct unit)* is the entry point. It reads the parsed context, dispatches in a
  fixed priority order, prints the banner and help, builds the engine through explicit backend
  registration, runs an extraction and reports its outcome, exposes the auxiliary `--list-backends`
  command, and translates expected argument and operation faults into a clean, message-only,
  non-zero exit.
- **Cli** *(subsystem)* owns the command line. Its single unit, **Context**, parses and validates
  the arguments, projects the extraction flags onto the engine's options, and routes every line the
  tool emits to the console and an optional `--log` file, suppressing the console under `--silent`
  while always recording an error.
- **SelfTest** *(subsystem)* drives `--validate`. **Validation** prints an environment header, runs
  the tool's own functionality in-process and the union of self-test cases every registered backend
  contributes, and writes the outcome as TRX or JUnit. **SelfTestAdapter** maps Core's
  dependency-free self-test records into the `DemaConsulting.TestResults` object model.

### Why registration is explicit

The tool builds its engine with one visible registration chain:
`new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build()`.
There is no reflection and no assembly scanning. That is the fact that keeps single-file publishing
viable: a reflection-discovered backend would be invisible to the trimmer and absent from a
single-file image. The same explicit registration is what self-validation exercises, so the
self-test union always reflects exactly what the tool actually runs.

### The dependency boundary that keeps Core clean

`DemaConsulting.TestResults` enters the dependency graph only in this tool, in the `SelfTestAdapter`
unit. Core defines its self-test contract (`ISelfValidating`, `SelfTestCase`, `SelfTestResult`,
`SelfTestStatus`) as plain records with no NuGet dependency, so it keeps its zero-runtime-
dependency posture; the adapter that turns those records into a serializable results model lives
here, on the tool side of the boundary. Neither Core nor the extraction backends references
`DemaConsulting.TestResults`.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| Command line | Inbound, from a user or pipeline | Arguments | DEMA vocabulary plus DocDown extraction flags |
| Standard output / error | Outbound | Text | Suppressed by `--silent`; an error still sets the exit code |
| `--log` file | Outbound | Text | Opened with immediate flushing |
| `--results` file | Outbound | TRX or JUnit XML | Extension selects the serializer |
| `DocDownEngine` | Inbound, from Core | .NET API | Extraction, backend inventory, and self-test surfaces |
| `DocDownBuilder` chain | Inbound, from Core and backends | .NET API | Explicit registration only; no discovery |
| `DemaConsulting.TestResults` | Outbound, from the tool | .NET API | Referenced only here; MIT, zero deps |

## Dependencies

- **DocDown.Core** — the engine, extraction options and results, the backend inventory surface, and
  the self-test seam. See the *DocDown.Core System Design*.
- **DocDown.Pdf** — the managed PDF extraction backend, added through `AddPdf`. See the
  *DocDown.Pdf System Design*.
- **DocDown.Pdf.Rendering** — the optional page-rendering backend, added through
  `AddPdfRendering`. It is the only reference that carries a native stack (PDFium/SkiaSharp) into
  the tool. See the *DocDown.Pdf.Rendering System Design*.
- **DocDown.Word** — the Word extraction backend, added through `AddWord`. See the
  *DocDown.Word System Design*.
- **DocDown.Visio** — the Visio extraction backends, added through `AddVisio`. See the
  *DocDown.Visio System Design*.
- **DocDown.PowerPoint** — the PowerPoint extraction backends, added through `AddPowerPoint`. See
  the *DocDown.PowerPoint System Design*.
- **DocDown.Excel** — the Excel extraction backend, added through `AddExcel`. See the
  *DocDown.Excel System Design*.
- **DemaConsulting.TestResults** (OTS) — the test-results object model and the TRX/JUnit
  serializers, referenced only by this tool. See *TestResults* under the OTS integration design.

The tool therefore carries the rendering backend's native binaries transitively, but the packaged
`.nupkg` stays **runtime-identifier-agnostic**: `PackAsTool` places every tool asset under the
literal `any` platform folder (`tools/<tfm>/any/…`), with the native assets resolved from
`tools/<tfm>/any/runtimes/<rid>/native/…` at run time, so a single package installs on every
supported RID from one `dotnet tool install`. A self-contained single-file publish, by contrast, is
RID-specific and needs `dotnet publish -r <rid>`. This RID-agnostic property is proven against the
produced package artifact by `DocDownTool_Package_Nupkg_IsRidAgnosticDotNetTool`, not merely
asserted from project metadata.

### Packaged footprint

Carrying a native rasterizer for every runtime identifier makes the tool package large, so what it
carries is a deliberate choice rather than whatever the dependency graph offers.

- **One target framework.** A tool is executed, never referenced, so the package is built for
  `net10.0` alone. Installing the tool therefore requires a .NET 10 runtime. The libraries keep
  their `net8.0;net9.0;net10.0` matrix, so this constrains only the command line.
- **No native debug symbols.** The PDFium and SkiaSharp runtime packages ship a `.pdb` beside each
  native binary; `libSkiaSharp.pdb` alone is 82–88 MB per Windows RID. They describe third-party
  native code a DocDown stack trace never enters. The tool's own managed symbols still ship in the
  companion `.snupkg`.
- **Supported platforms only.** SkiaSharp also ships natives for Android (`linux-bionic`),
  LoongArch, and RISC-V. DocDown is a desktop and CI command-line tool, and no CI matrix leg
  exercises those platforms, so they are excluded. Claimed platform support is therefore exactly
  what is built and tested: Windows, Linux (glibc and musl), and macOS.

Together these took the package from 592 MB to 108 MB. Both exclusions are enforced against the
produced artifact by `DocDownTool_Package_Nupkg_ExcludesNativeSymbolsAndUnsupportedRuntimes`, which
also asserts the supported platforms are still present, so a dependency update cannot quietly
restore the bulk nor silently strip the tool of the natives it needs.

## Risk Control Measures

- **Reflection-free registration.** The engine is built from one explicit registration chain, so
  single-file publishing cannot silently drop a backend and the active backend set stays a decision
  the code states rather than a deployment accident.
- **Outcome reporting stays factual.** An unreadable document is reported by the engine as data, and
  the tool writes the failure explanation verbatim to standard error and exits non-zero; a produced
  extraction is reported as produced, with an optional note count and the summary path. The tool does
  not add a separate acceptability verdict.
- **The summary path is printed only when output exists.** `summary.txt` is printed only for a
  produced extraction, which keeps the success handle aligned with an output layout that was actually
  written.
- **Expected faults do not surface as defects.** Argument and operation faults are caught and reduced
  to a message and a non-zero exit; any other exception is re-thrown after being written to standard
  error, so a genuine defect is still recorded by the runtime.
- **The dependency boundary is one-directional.** `DemaConsulting.TestResults` is referenced only by
  this tool, keeping Core and the extraction backends free of it.

## Data Flow

1. `Main` builds a `Context` from the arguments — which parses and validates them and opens the
   log — then calls `Run` and returns the context's exit code, catching argument and operation
   faults as expected errors.
2. `Run` dispatches in priority order: version, then the banner and help, then `--list-backends`,
   then `--validate`, then extraction — executing only the highest-priority match.
3. For `--list-backends`, `Program` builds the engine, reads `GetBackends()`, and prints each
   backend as an identifier and display name line, a `formats:` line, and a `status:` line.
4. For an extraction, `Program` builds the engine, asks `Context` for the mapped options, and runs
   `ExtractAsync`. On `ExtractionOutcome.Unreadable` it writes `Failure.Explanation` to standard
   error and exits non-zero. On `ExtractionOutcome.Produced` it prints `Extraction produced the
   output layout.`, optionally prints the note count, then prints the absolute path to
   `summary.txt`.
5. For `--validate`, `Validation` prints the header, runs the tool's own version and help checks
   in-process against a captured log, runs the engine's self-test union, maps each result through
   `SelfTestAdapter`, prints per-test lines, and writes the TRX or JUnit file when requested.

## Design Constraints

- **The result-file name is load-bearing.** A traceability pipeline filters test results by a
  case-insensitive substring match against the result file's base name, so a `--validate` TRX must
  be named to contain the platform token a platform requirement filters on — for example
  `docdown-validate-windows.trx`. This is a property of how the emitted file is consumed, recorded
  here and in the verification design.
- **A skipped self-test satisfies nothing.** A skip is emitted as a not-executed outcome, distinct
  from a failure. It produces no false failure, but because a traceability pipeline does not count a
  not-executed result as executed evidence, a requirement whose only linked result is a skip is
  reported unsatisfied. Emitting skips is correct and safe; it is not a substitute for a real run.
- **No option is invented that the engine cannot honor.** Every extraction flag maps onto a real
  `ExtractionOptions` member, so the command line never promises behavior the library does not
  provide.
- **The scratch-folder surface is intentionally narrow.** `--overwrite` accepts only `clean` and
  `overwrite`, matching the two scratch-folder policies the engine now exposes.
