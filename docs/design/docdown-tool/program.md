## Program

![DocDown.Tool Structure](DocDownToolView.svg)

### Purpose

`Program` is the tool's entry point and the direct unit of the `DocDown.Tool` system. Its
responsibility is orchestration: read the parsed context, dispatch in a fixed priority order, print
the banner and help, build the engine through the explicit registration seam, run an extraction and
report its outcome, expose the `--list-backends` auxiliary command, and translate expected argument
and operation faults into a clean, message-only, non-zero exit.

### Data Model

`Program` is an `internal static class`. It holds no state beyond the reflected `Version` property,
which reads the assembly's `AssemblyInformationalVersionAttribute` on each access.

### Key Methods

- **`int Main(string[] args)`** — builds a `Context`, runs the logic, and returns the context's exit
  code. It catches `ArgumentException` and `InvalidOperationException` as expected errors — writing
  only the message to standard error and returning 1 with no stack trace — and re-throws any other
  exception after writing it to standard error, so a genuine defect is still recorded by the runtime.
- **`void Run(Context context)`** — the priority-ordered dispatch. It executes only the
  highest-priority match: version, then the banner and help, then `--list-backends`, then
  `--validate`, then extraction. This makes the outcome of any flag combination deterministic.
- **`RunExtraction`** (private) — requires `--input` and `--scratch`, rejecting a missing one with an
  argument fault; builds the engine through the explicit registration chain; asks the context for the
  mapped options; runs `ExtractAsync`; and reports. On `ExtractionOutcome.Unreadable` it writes the
  failure explanation to standard error and sets a non-zero exit; on `ExtractionOutcome.Produced` it
  prints `Extraction produced the output layout.`, optionally prints the note count, and then prints
  the absolute path to `summary.txt`, computed with `Path.GetFullPath` so it is always fully
  qualified.
- **`RunListBackends`** (private) — prints each registered backend's identifier and display name,
  supported formats, and availability from `DocDownEngine.GetBackends()`, which returns an
  `IReadOnlyList<ExtractorCandidate>`.
- **`BuildEngine`** (private) —
  `new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build()`;
  the one explicit, reflection-free registration chain the whole tool uses.

### Error Handling

Argument faults (a missing required option, an unrecognized flag, a malformed value) and operation
faults (a log file that cannot be opened) are the two expected error classes: `Main` reduces each
to its message and a non-zero exit. Every adverse extraction condition is returned by the engine as
data, so the extraction path never throws for a bad document; it writes the explanation instead. The
`catch (Exception)` re-throw is a defensive branch for a genuinely unexpected fault: the engine's
data-not-exceptions contract means it is not reached on any ordinary input, and it exists so a real
defect is surfaced rather than swallowed.

### Dependencies

- **Context** — the parsed arguments and the output routing. See *Context Design*.
- **Validation** — the `--validate` driver. See *Validation Design*.
- **DocDown.Core** — `DocDownBuilder`, `DocDownEngine`, `ExtractionOptions`, `ExtractionResult`,
  `ExtractionOutcome`, and `ExtractorCandidate`.
- **DocDown.Pdf**, **DocDown.Pdf.Rendering**, **DocDown.Word**, **DocDown.Visio**,
  **DocDown.PowerPoint**, and **DocDown.Excel** — the explicit backend registration seams.

### Callers

`Program.Main` is the process entry point invoked by the .NET tool host. `Program.Run` is also
invoked in-process by `Validation` to exercise the tool's own commands during self-validation.
