### Context

![DocDown.Tool Structure](DocDownToolView.svg)

#### Purpose

`Context` owns the parsed command-line arguments and every channel the tool writes to. Its single
responsibility is the boundary between the raw argument vector and the rest of the tool: it produces
a validated set of choices, an `ExtractionOptions` projection of the extraction flags, and a pair of
output methods whose behavior `--silent` and `--log` control.

#### Data Model

`Context` is an `internal sealed class` implementing `IDisposable`, with a private constructor and a
static `Create` factory. Parsing is delegated to a nested `private sealed class ArgumentParser`.
The parsed choices are exposed as `private init` properties:

- The DEMA vocabulary — `Version`, `Help`, `Silent`, `Validate`, `ResultsFile`, and `HeadingDepth`
  (default 1) — plus `ListBackends` for the auxiliary command.
- The extraction flags — `Input`, `Scratch`, `RenderPages`, `IncludeEmbeddedImages` (default true),
  `Pages`, `Dpi`, `ImageOutput`, `MaxImageDimensionPx`, `MaxImageBytes`, `ContentSplit`, and
  `ScratchMode`. The optional ones are nullable so an unset flag can be told apart from a flag set
  to its default.
- `ExitCode` returns 1 when any error has been reported, and 0 otherwise.

The log writer is a private `StreamWriter?` opened by `Create` when `--log` is given.

#### Key Methods

- **`static Context Create(string[] args)`** — validates the argument array, runs the nested parser,
  copies the parsed values into a new instance, and opens the log file if one was requested. An
  unrecognized or malformed argument surfaces as an `ArgumentException`; a log file that cannot be
  opened surfaces as an `InvalidOperationException`.
- **`ExtractionOptions BuildExtractionOptions()`** — projects the parsed extraction flags onto a
  fresh options instance, writing only the options the caller set. `--overwrite` accepts only the
  `clean` and `overwrite` tokens, mapping them directly to the current scratch-folder modes.
- **`void WriteLine(string message)`** — writes to standard output unless `Silent`, and always to the
  log when one is open.
- **`void WriteError(string message)`** — sets the error flag unconditionally, writes to standard
  error unless `Silent`, and always to the log when one is open.
- **`ArgumentParser.ParseArgument`** (nested, private) — a `switch` over the argument, with
  `GetRequiredStringArgument`, `GetRequiredIntArgument(min, max)`, and `GetRequiredLongArgument(min,
  max)` helpers, and token parsers for the page range, image mode, split mode, and scratch policy,
  each throwing an argument fault on a malformed value. The default case rejects an unsupported
  argument.

#### Error Handling

All argument and value validation happens at `Create` time and surfaces as `ArgumentException`, so a
bad command line fails before any work begins. A log file that cannot be opened is wrapped as an
`InvalidOperationException`. Both are the expected error classes `Program.Main` reduces to a clean
non-zero exit. `Dispose` closes the log writer.

#### Dependencies

- **DocDown.Core** — `ExtractionOptions` and the option value types the flags map onto: `PageRange`,
  `ImageOutputMode`, `ContentSplitMode`, and `ScratchFolderMode`.

#### Callers

`Program.Main` constructs a `Context` per invocation. `Validation` constructs additional contexts to
run the tool's own commands in-process with `--silent --log` during self-validation.
