# DocDown.Tool Verification Design

This document describes the system-level verification strategy for `DocDown.Tool`, the `docdown`
command-line tool.

## Verification Approach

`DocDown.Tool` is verified through system-level integration tests in `DocDownToolTests.cs` and unit
tests per unit, all in `DemaConsulting.DocDown.Tool.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### The tool is driven in-process against a captured log

Every scenario runs the real tool through its own entry point — `Program.Run`, or `Program.Main` for
the exit-code and error paths — with `--silent --log <temp>`, and asserts on the captured log. This
is the same silence-plus-log mechanism the tool's own self-validation uses, so the tests exercise the
tool exactly as a scripted caller would rather than through a test-only seam. The document each
extraction consumes is a PDF generated at test time by PdfPig's document writer, so no binary fixture
is committed.

### Extraction, backend inventory, and self-validation are all exercised end to end

The clean extraction scenario asserts the tool prints `Extraction produced the output layout.`, then
the absolute path to a `summary.txt` that exists on disk, and exits zero. The note-reporting scenario
asserts an extraction that records a note reports the short plain note count and preserves that note
in the summary and manifest. The unreadable-input scenario asserts the tool writes the engine's
explanation, not a stack trace, and exits non-zero. The self-validation scenarios run
`docdown --validate --results <file>` and re-parse the emitted TRX and JUnit files, confirming they
are well-formed, include the current Core self-tests, record the PDF page-rendering self-test as
not-executed, and surface the rendering backend's own self-test without reporting a failure.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds the generated input document and the
  extraction output
- **Inputs**: a PDF generated at test time; no committed binary fixtures and no network access
- **Mocking**: none; the tests drive the real tool, the real engine, and the real PDF backend
- **Isolation**: each test owns its temporary folder and its captured log, and cleans them on
  dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- A generated PDF extracts cleanly, the tool reports that the output layout was produced, the tool
  prints an absolute `summary.txt` path that exists, and the process exits zero, on every operating
  system and runtime in the CI matrix.
- An extraction that records notes reports the note count in plain prose and preserves the note in
  the produced summary and manifest.
- An unreadable input writes the explanation for the unreadable outcome, with no stack trace, and
  exits non-zero.
- The default engine registers exactly the PDF, PDF-rendering, Word, Visio, PowerPoint, and Excel
  backends through the explicit builder seams.
- `docdown --validate --results <file>.trx` and `--results <file>.xml` produce well-formed TRX and
  JUnit that re-parse, include `core.layout-invariance` and `core.manifest-schema`, and record the
  always-skipped page-rendering case as not-executed, distinct from a failure.
- The rendering backend's self-test case surfaces under `--validate` and is recorded without a
  failure outcome.
- `--list-backends` reports each backend as an identifier and display name line, a `formats:` line,
  and a `status:` line, with no capabilities line.
- The standard version and help commands print their expected output, and an unrecognized argument
  exits non-zero with a message and no stack trace.
- The tool project is configured to pack as a .NET tool with the command name `docdown`.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime.

## Test Scenarios

### A generated PDF extracts and prints the absolute summary path

**Test**: `DocDownTool_Extract_GeneratedPdf_PrintsAbsoluteSummaryPathAndExitsZero`

Proves the primary path: a generated PDF extracts cleanly, the tool reports that the output layout
was produced, the tool prints a fully qualified `summary.txt` path that exists on disk, and the
process exits zero. This is also the anchor for the platform requirements. Evidence for
`DocDownTool-Extraction` and, under source filters, the six `DocDownTool-Platform-*` requirements.

### A produced extraction reports recorded notes

**Test**: `DocDownTool_Extract_NoImages_ReportsRecordedNoteInSummaryAndManifest`

Proves an extraction that records a note still produces output, reports the plain note count, and
persists the note in both `summary.txt` and `manifest.json`. Evidence for
`DocDownTool-ExtractionNotes` and `DocDownTool-OptionMapping`.

### An unreadable input writes the explanation

**Test**: `DocDownTool_Extract_UnreadableInput_RendersStructuredFailureAndExitsNonZero`

Proves the tool writes the unreadable explanation for a missing document and exits non-zero, with no
stack trace reaching the output. Evidence for `DocDownTool-StructuredFailure`.

### The default engine registers the backends explicitly

**Test**: `DocDownTool_Build_DefaultEngine_RegistersBackendsExplicitly`

Proves the one-liner the tool uses to build its engine registers the expected backend set, with no
reflection. Evidence for `DocDownTool-ExplicitRegistration`.

### Self-validation produces well-formed TRX and JUnit

**Tests**: `DocDownTool_Validate_ResultsTrx_ProducesWellFormedTrx`,
`DocDownTool_Validate_ResultsXml_ProducesWellFormedJUnit`

Prove `docdown --validate --results <file>` writes a results file that re-parses and carries the
current self-test union, including the current Core cases. These are also the transitive evidence for
the TestResults OTS item. Evidence for `DocDownTool-SelfValidation`.

### A skipped self-test is recorded as not-executed

**Test**: `DocDownTool_Validate_SkippedPdfRenderCase_RecordedAsNotExecuted`

Proves the always-skipped PDF page-rendering case is serialized as a not-executed outcome, distinct
from a failure — the property a traceability pipeline depends on. Also transitive evidence for the
TestResults OTS item. Evidence for `DocDownTool-SelfValidationSkipDistinct`.

### The rendering backend self-test is surfaced

**Test**: `DocDownTool_Validate_RenderingSelfTestCase_IsRecordedAndNotFailed`

Proves the rendering backend contributes its own self-test case under `--validate`, and that the case
is recorded without a failure outcome. Evidence for `DocDownTool-SelfValidation`.

### Registered backends are listed

**Test**: `DocDownTool_ListBackends_DefaultEngine_ListsPdfAvailable`

Proves `--list-backends` reports the registered-backends heading, omits a capabilities line, and
prints the expected identity, formats, and status lines for the PDF backend. Evidence for
`DocDownTool-BackendInventory`.

### The standard version and help commands

**Tests**: `DocDownTool_Version_PrintsInformationalVersion`, `DocDownTool_Help_PrintsUsageAndOptions`

Prove the standard DEMA version and help output. Evidence for `DocDownTool-StandardFlags`.

### An unrecognized argument exits non-zero without a stack trace

**Test**: `DocDownTool_BadArgument_WritesErrorAndExitsNonZero`

Proves an unrecognized argument makes `Program.Main` return 1 with an error message and no stack
trace. Evidence for `DocDownTool-ExpectedErrors`.

### The produced package is a RID-agnostic .NET tool named docdown

**Test**: `DocDownTool_Package_Nupkg_IsRidAgnosticDotNetTool`

Packs the tool project with `dotnet pack` and inspects the produced `.nupkg` itself, rather than the
project's source metadata. Proves the package is **runtime-identifier-agnostic**: every
`tools/<tfm>/…` asset places its RID segment at exactly the literal `any` (no RID-specific tool
folder), and a `DotnetToolSettings.xml` declares `Command Name="docdown"`. The test deliberately does
**not** assert the absence of native assets: the tool carries the rendering package's PDFium/SkiaSharp
binaries transitively by design, under `tools/<tfm>/any/runtimes/<rid>/native/…`, and they are
resolved at run time — so the claim under test is RID-agnosticism (one package installs on every
RID), not native-freedom. Evidence for `DocDownTool-Packaging`.
