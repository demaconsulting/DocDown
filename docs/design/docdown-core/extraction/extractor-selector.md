### ExtractorSelector

![Extraction Structure](ExtractionView.svg)

#### Purpose

`ExtractorSelector` chooses the extractor to run for a detected format. Its single responsibility is
selection, expressed as a pure static function so it can be tested with hand-built candidates and no
I/O.

#### Data Model

`ExtractorSelector` is a `static class` with no instance state.

Its private static data is limited to two naming tables:

- **`WellKnownPackages`** — maps a detected modern format such as `docx` or `pptx` to the DocDown
  package that owns that extractor.
- **`LegacyBinaryFormatIds`** — the detected legacy binary Office formats that DocDown names but does
  not support.

The public result of selection is either an `ExtractorDescriptor` or a prose `ExtractionFailure`
returned through the `out` parameter.

#### Key Methods

- **`ExtractorDescriptor? Select(FormatDetection format, ExtractionOptions options,
  IReadOnlyList<ExtractorCandidate> candidates, out ExtractionFailure? failure)`** — chooses one
  extractor or returns failure prose.
  - *Preconditions*: all arguments non-null.
  - *Algorithm*:
    1. keep only candidates whose descriptor supports the detected format;
    2. keep only candidates whose availability is currently usable;
    3. when `RenderPages` is true, prefer the available candidates that report
       `ProvidesRenderedPages = true`;
    4. choose by descending `Priority`, then ascending `Id`.
  - *Postconditions*: on success `failure` is null and the returned descriptor is non-null; on
    failure the return value is null and `failure` contains direct displayable prose.

Private helpers keep concerns separate:

- **`Supports`** — matches a descriptor to a detected format by stable format identifier.
- **`DescribeUnsupportedFormat`** — returns honest prose naming the owning package for a well-known
  format or stating that a legacy binary format is unsupported.
- **`MakeFailure`** — composes the final multi-line `ExtractionFailure`.

#### Error Handling

`Select` throws `ArgumentNullException` only for null arguments. Unsupported or unavailable formats do
not throw; they return `ExtractionFailure` prose instead. The selector performs no I/O and has no
filesystem or process failure modes of its own.

#### Dependencies

- **`FormatDetection`** from the Detection subsystem.
- **`ExtractionOptions`**, **`ExtractorCandidate`**, **`ExtractorDescriptor`**, and
  **`ExtractionFailure`** from the Extraction subsystem.

#### Callers

`DocDownEngine` calls `Select` during the selection step of the pipeline. The static method is also
public for direct unit testing.
