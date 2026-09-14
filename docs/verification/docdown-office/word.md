# DocDown.Word Verification Design

This document describes the system-level verification strategy for `DocDown.Word`, the Word
extraction package.

## Verification Approach

`DocDown.Word` is verified through system-level integration tests in `DocDownWordTests.cs` and
`WordGoldenTests.cs`, and through subsystem and unit tests in `DemaConsulting.DocDown.Office.Tests`,
running on xUnit v3 across net8.0, net9.0, and net10.0.

### Every extraction test proves the expected layout is written

Every system-level extraction scenario ends with `ContractAssert.LayoutPresent`, which proves the
expected output layout exists on disk for the same run whose content, notes, metadata, or summary
is being asserted. This is the highest-value assertion at system level: a successful or unreadable
result still has to produce the layout the rest of DocDown expects.

### One backend, one reader, one emitter

The package ships one backend: the managed Open XML reader. It reads a document into the
reader-neutral model and drives the shared emitter, so every mapping decision is made in one place
and can be proved either from a hand-built model or from a real extraction. Legacy binary `.doc`
content has no reader here and none anywhere in DocDown, so the request produces an unreadable
result with a plain explanation rather than an exception surfacing to the caller.

### Content inventory and short notes are both exercised

Most of the reporting behavior under test now falls into one of two buckets. The content inventory
states what reached the output, including deliberate zero counts for categories the extractor
looked for. Short extraction notes are reserved for attempted steps the extractor could not
complete, such as chart-part reading or flattened merged or nested table structure. The suite
therefore covers both clean extractions and honest incomplete-step reporting.

### Test fixtures are generated; the self-test probe is committed

Every document the suite uses is built at test time by the Open XML SDK writer in
`TestData/DocxFixtures.cs`, orchestrated through `TestData/WordTestHarness.cs`. The legacy `.doc`
scenario writes a placeholder byte sequence whose extension drives format detection, because the
selection path never opens the file. No fixture is committed.
The one committed binary is the backend's self-test probe: a real document authored in the
application that produces the format, embedded in the package so the self-test reads what that
application emits.

### Golden summaries pin representative outputs

Four representative extractions render `summary.txt` under a fixed timestamp and fixed backend,
normalize the machine-specific lines, and compare the remainder byte for byte against committed
goldens under `test/.../golden/`. Setting `DOCDOWN_UPDATE_GOLDEN=1` rewrites a golden, and
regeneration is refused in CI.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input document and the
  extraction output
- **Inputs**: WordprocessingML documents generated at test time by the Open XML SDK writer, plus a
  short byte sequence for the legacy `.doc` unreadable scenario; no committed binary fixtures and
  no network access
- **Mocking**: none — every system test drives the real engine and the real Open XML backend
- **Determinism**: the run-varying timestamp line is normalized so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception reaching the caller.
- A generated `.docx` produces the expected DocDown layout, real markdown table output, a Document
  Control section, `ExtractionOutcome.Produced`, and no extraction notes for the clean scenario.
- The summary and manifest make reviewer comments discoverable through content inventory rather than
  only through a character count.
- A document embedding a chart produces `ExtractionOutcome.Produced` together with one short note
  explaining that chart parts were not read.
- Embedded images are written and linked from the content, and a vector image is written unchanged
  when the document stores one.
- A legacy binary `.doc` produces `ExtractionOutcome.Unreadable` with a plain unsupported-format
  explanation and the expected output layout.
- Four representative summaries match their committed goldens after normalization.

## Test Scenarios

Each scenario corresponds to one system requirement or provides supporting system evidence.
Platform requirements are covered by the source-filtered runs of the clean-layout scenario.

### The default engine registers the Word backend

**Test**: `AddWord_OnBuilder_RegistersOpenXmlBackend`

Proves the one-liner a host uses to add Word support registers the Open XML backend and registers
nothing else. Evidence for `DocDownWord-Registration`.

### Contract layout is produced for a generated document

**Test**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`

Proves a clean extraction: the expected layout, a real GFM table in `content.md`, a
`## Document Control` heading, the Open XML backend selected, `ExtractionOutcome.Produced`, and no
notes. Evidence for `DocDownWord-Extraction`, `DocDownWord-Tables`, and under source filters the
six `DocDownWord-Platform-*` requirements.

### Reviewer comments are named in the summary outline

**Test**: `DocDownWord_Extract_DocxWithComment_SummaryOutlineNamesTheComments`

Proves the summary and manifest name the document's comments and distinct comment authors, so a
consumer can tell from the inventory that reviewer commentary is present. Supporting evidence for
the Markdown subsystem's content-inventory behavior.

### Document Control is preserved from an engineering-style document

**Test**: `DocDownWord_Extract_EngineeringStyleDocx_PreservesRevisionInDocumentControl`

Proves the extracted content contains `## Document Control`, preserves the revision and
classification text, and does not surface page-number furniture as body-like content. Evidence for
`DocDownWord-DocumentControl`.

### Embedded charts are reported as a short extraction note

**Test**: `DocDownWord_Extract_DocxWithChart_ReportsChartNote`

Proves a document embedding a chart still completes as `Produced` and reports one note explaining
that the extractor does not read chart parts, so chart data does not vanish silently. Evidence for
`DocDownWord-ChartNotes`.

### Embedded images are written and linked from the content

**Test**: `DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem`

Proves the embedded PNG is written byte-identically to the fixture source and linked from the
content with an `images/` path. Evidence for `DocDownWord-EmbeddedImages` and
`DocDownWord-ImageProvenance`.

### The managed backend is selected for a modern document

**Test**: `DocDownWord_Extract_Docx_SelectsOpenXml`

Proves a modern Word document selects the managed Open XML backend. Supporting evidence for the
OpenXml subsystem selection path.

### A legacy binary document is reported unreadable with a plain explanation

**Test**: `DocDownWord_Extract_LegacyDoc_IsUnreadableWithUnsupportedFormatExplanation`

Proves an engine with the Word package registered returns `ExtractionOutcome.Unreadable` for a
`.doc`, states plainly that DocDown does not support the legacy binary Office formats, and still
produces the expected layout. Evidence for `DocDownWord-LegacyFormatUnreadable`.

### Representative summaries match their committed goldens

**Tests**:

- `WordGolden_Simple_MatchesCommittedGolden`
- `WordGolden_TablesAndImages_MatchesCommittedGolden`
- `WordGolden_MergedCellsGap_MatchesCommittedGolden`
- `WordGolden_DocumentControl_MatchesCommittedGolden`

Prove four representative extractions — a simple two-section document, a document with tables and
images, one whose merged cells produce a flattening note, and an engineering-style document with a
Document Control section — render a normalized `summary.txt` that is byte-identical to its
committed golden. Evidence for `DocDownWord-Determinism`.
