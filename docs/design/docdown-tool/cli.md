## Cli Subsystem

![DocDown.Tool Structure](DocDownToolView.svg)

The Cli subsystem owns the command line. It parses and validates the arguments, projects the
extraction flags onto the engine's options, and routes every line the tool emits to the console and
an optional log file — suppressing the console when silence is requested while always recording an
error so the exit code stays truthful.

## Architecture

The subsystem is a single unit, **Context**, because there is one boundary here: the translation
between the argument vector the process receives and the two things the rest of the tool needs from
it — a validated set of choices, and a pair of output channels. Context follows the DEMA tool house
pattern faithfully: a private constructor with a static `Create` factory, a nested argument parser
that dispatches on a `switch`, required-value helpers that throw an argument fault, and
silence-aware `WriteLine` and `WriteError` routing with an optional log opened for immediate
flushing.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| Argument vector | Inbound, from the process | `string[]` | Parsed at `Create`; a bad argument is an argument fault |
| Standard output / error | Outbound | Text | Console suppressed under `--silent`; the log is not |
| `--log` file | Outbound | Text | Opened with immediate flushing; an open failure is an operation fault |
| `ExtractionOptions` | Outbound, to Program | .NET value | Built from the parsed flags; unset options keep defaults |

## Design Constraints

- **The error flag is set unconditionally.** `WriteError` records an error even under `--silent`, so
  a silenced run that hit an error still exits non-zero. Silence controls only whether the console
  shows the error, never whether the tool reports one.
- **Only set options are written.** `BuildExtractionOptions` writes the option for each flag the
  caller actually gave and leaves the rest at the `ExtractionOptions` default, so an unspecified
  flag never forces a value the caller did not choose.
- **The scratch-folder policy is explicit.** `--overwrite` accepts only `clean` and `overwrite`,
  mapping directly to the two scratch-folder modes the engine currently exposes.
- **No option the engine cannot honor is accepted.** Every extraction flag maps onto a real
  `ExtractionOptions` member, so the command line is an honest surface over the library.

## Dependencies

- **DocDown.Core** — `ExtractionOptions` and the option value types (`PageRange`,
  and `ScratchFolderMode`) the flags map onto.

There are no other dependencies; the subsystem performs no extraction and constructs no engine.
