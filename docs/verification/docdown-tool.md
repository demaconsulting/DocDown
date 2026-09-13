# DocDown.Tool Verification Design

This document describes the system-level verification strategy for `DocDown.Tool`, the `docdown`
command-line tool.

## Verification Approach

`DocDown.Tool` is verified through system-level integration tests in `DocDownToolTests.cs` and
unit tests per unit, all in `DemaConsulting.DocDown.Tool.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### The tool is driven in-process against a captured log

Every scenario runs the real tool through its own entry point — `Program.Run`, or `Program.Main` for
the exit-code and error paths — with `--silent --log <temp>`, and asserts on the captured log. This
is the same silence-plus-log mechanism the tool's own self-validation uses, so the tests exercise the
tool exactly as a scripted caller would rather than through a test-only seam. The document each
extraction consumes is a PDF generated at test time by PdfPig's document writer, so no binary fixture
is committed.

### Extraction, failure, and self-validation are all exercised end to end

The clean extraction scenario asserts the tool prints the absolute path to a `summary.txt` that
exists on disk and exits zero. The failure scenario asserts the tool renders the engine's structured
explanation, not a stack trace, and exits non-zero. The self-validation scenarios run
`docdown --validate --results <file>` and re-parse the emitted TRX and JUnit files, confirming they
are well-formed and that a skipped self-test is recorded as a not-executed outcome distinct from a
failure.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds the generated input document and the
  extraction output
- **Inputs**: a PDF generated at test time; no committed binary fixtures and no network access
- **Mocking**: none; the tests drive the real tool, the real engine, and the real PDF backend
- **Isolation**: each test owns its temporary folder and its captured log, and cleans them on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- A generated PDF extracts cleanly, the tool prints an absolute `summary.txt` path that exists, and
  the process exits zero, on every operating system and runtime in the CI matrix.
- An unreadable input renders the structured failure explanation, with no stack trace, and exits
  non-zero.
- The default engine registers exactly the PDF backend through the explicit `AddPdf` seam.
- An extraction option flag reaches the engine, evidenced by `--no-images` producing a reported
  suppression gap.
- `docdown --validate --results <file>.trx` and `--results <file>.xml` produce well-formed TRX and
  JUnit that re-parse, and the always-skipped page-rendering case is recorded as not-executed,
  distinct from a failure.
- `--list-backends` reports the PDF backend as available, and `--verify` reports no violations for a
  folder the tool produced.
- The standard version and help commands print their expected output, and an unrecognized argument
  exits non-zero with a message and no stack trace.
- The tool project is configured to pack as a .NET tool with the command name `docdown`.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime.

## Test Scenarios

### A generated PDF extracts and prints the absolute summary path

**Test**: `DocDownTool_Extract_GeneratedPdf_PrintsAbsoluteSummaryPathAndExitsZero`

Proves the primary path: a generated PDF extracts cleanly, the tool prints a fully qualified
`summary.txt` path that exists on disk, and the process exits zero. This is also the anchor for the
platform requirements. Evidence for `DocDownTool-Extraction` and, under source filters, the six
`DocDownTool-Platform-*` requirements.

### An unreadable input renders the structured failure

**Test**: `DocDownTool_Extract_UnreadableInput_RendersStructuredFailureAndExitsNonZero`

Proves the tool renders the engine's structured explanation for a missing document and exits
non-zero, with no stack trace reaching the output. Evidence for `DocDownTool-StructuredFailure`.

### The default engine registers the PDF backend explicitly

**Test**: `DocDownTool_Build_DefaultEngine_RegistersPdfExplicitly`

Proves the one-liner the tool uses to build its engine registers exactly the PDF backend, with no
reflection. Evidence for `DocDownTool-ExplicitRegistration`.

### An option flag reaches the engine

**Test**: `DocDownTool_Extract_NoImages_ReportsSuppressionGap`

Proves `--no-images` changes the extraction: images are suppressed and the tool reports the resulting
gap as a degraded outcome. Evidence for `DocDownTool-OptionMapping`.

### Self-validation produces well-formed TRX and JUnit

**Tests**: `DocDownTool_Validate_ResultsTrx_ProducesWellFormedTrx`,
`DocDownTool_Validate_ResultsXml_ProducesWellFormedJUnit`

Prove `docdown --validate --results <file>` writes a results file that re-parses and carries the
engine self-test cases. These are also the transitive evidence for the TestResults OTS item. Evidence
for `DocDownTool-SelfValidation`.

### A skipped self-test is recorded as not-executed

**Test**: `DocDownTool_Validate_SkippedPdfRenderCase_RecordedAsNotExecuted`

Proves the always-skipped PDF page-rendering case is serialized as a not-executed outcome, distinct
from a failure — the property a traceability pipeline depends on. Also transitive evidence for the
TestResults OTS item. Evidence for `DocDownTool-SelfValidationSkipDistinct`.

### Registered backends are listed

**Test**: `DocDownTool_ListBackends_DefaultEngine_ListsPdfAvailable`

Proves `--list-backends` reports the PDF backend as available. Evidence for `DocDownTool-ListBackends`.

### An existing scratch folder is verified

**Test**: `DocDownTool_Verify_ValidScratch_ReportsNoViolations`

Proves `--verify` runs the contract verifier over a folder the tool produced and reports no
violations. Evidence for `DocDownTool-VerifyFolder`.

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
project's source metadata. Proves the package is **runtime-identifier-agnostic**: every `tools/<tfm>/…`
asset places its RID segment at exactly the literal `any` (no RID-specific tool folder), and a
`DotnetToolSettings.xml` declares `Command Name="docdown"`. The test deliberately does **not** assert
the absence of native assets: the tool carries the rendering package's PDFium/SkiaSharp binaries
transitively by design, under `tools/<tfm>/any/runtimes/<rid>/native/…`, and they are resolved at run
time — so the claim under test is RID-agnosticism (one package installs on every RID), not
native-freedom. Evidence for `DocDownTool-Packaging`.
