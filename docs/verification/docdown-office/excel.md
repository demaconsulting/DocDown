# DocDown.Excel Verification Design

This document describes the system-level verification strategy for `DocDown.Excel`, the Excel extraction
package.

## Verification Approach

`DocDown.Excel` is verified through system-level integration tests in `DocDownExcelTests.cs` and unit
tests per unit, all in `DemaConsulting.DocDown.Office.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with layout assertions proving the invariant output
folders and files are present. This is the highest-value assertion available to this package: it makes a
dishonest extraction a test failure rather than a review finding, and it applies to `Produced` and
`Unreadable` outcomes alike.

### One backend, one reader, one emitter

The package ships one backend: the managed Open XML reader. It reads a workbook into the reader-neutral
model and drives the content emitter, so every mapping decision is made in exactly one place and can be
proved from a hand-built model with no workbook behind it. A legacy binary `.xls` has no reader here
and none anywhere in DocDown, so the request ends as a structured `Unreadable` result with a plain
unsupported-format explanation.

### The headline properties under test are verbatim content and factual reporting

The Excel intent forbids losing a value's length or precision, so the suite asserts it directly: a
worksheet cell holding a long synthetic prose block survives intact, and a stored number reaches the
output exactly as stored. The reporting side is asserted with the same discipline: content inventory
counts describe what the backend looked for, including zero counts where appropriate, while short notes
appear only when DocDown attempted a step and could not complete it.

### Page rendering is a request that stays silent

A workbook has no page grid. A scenario proves that requesting rendered pages leaves the run
`Produced`, writes no page images, and records no note. The extractor surface and environment facts
carry the non-applicability; the content emission path remains silent.

### Test fixtures are generated; the self-test probe is committed

Every workbook the suite uses is built at test time by the Open XML SDK writer in
`TestData/XlsxFixtures.cs`. The legacy `.xls` scenario writes a placeholder byte sequence whose
extension drives format detection, because the selection path never opens the file: no registered
backend supports the format. No fixture is committed.
The one committed binary is the backend's self-test probe: a real document authored in the
application that produces the format, embedded in the package so the self-test reads what that
application emits.
The suite's own fixtures stay generated, so the repository stays
text-only and no question arises about the provenance or licensing of a sample workbook. Every value,
sheet name, chart series, and annotation in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input workbook and the
  extraction output
- **Inputs**: SpreadsheetML workbooks generated at test time by the Open XML SDK writer, plus a short
  byte sequence for the legacy `.xls` unsupported-format scenario; no committed binary fixtures and no
  network access
- **Mocking**: none for the integration scenarios — every test drives the real engine and the real Open
  XML backend; the emitter and chart units are additionally exercised from hand-built models through a
  recording sink
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every extraction scenario produces the expected DocDown layout.
- Every readable workbook completes with `ExtractionOutcome.Produced`, and the legacy binary `.xls`
  scenario completes with `ExtractionOutcome.Unreadable` and the expected plain explanation.
- Every cell value reaches the output verbatim at full length and full precision; a long prose value is
  never truncated, and where it is elided in a grid table its full value is present in the listing.
- Every chart a worksheet shows is surfaced as its own content part carrying cached data when present,
  while an unreadable chart records a short note and a chart with no cached data or a bounded table
  states that fact in the chart part itself.
- Content inventory counts are reported from the model, including zero counts for features the backend
  explicitly looked for in an empty or image-free workbook.
- A page-rendering request writes no page images and records no note.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.

## Test Scenarios

Each scenario corresponds to one or more system requirements and names the real test method that
provides the evidence. Platform requirements are covered by the source-filtered runs of the selection
scenario.

### The default engine registers the Excel backend

**Test**: `AddExcel_RegistersSingleOpenXmlBackend`

Proves the one-liner the host uses to add Excel support registers the managed Open XML backend on the
resulting engine and registers nothing else. Evidence for `DocDownExcel-Registration`.

### The Open XML backend is selected for `.xlsx`

**Test**: `DocDownExcel_Extract_Xlsx_SelectsOpenXml`

Proves the managed Open XML backend is selected for a modern workbook and reports page rendering not
applicable. This is also the anchor for the platform requirements. Evidence for
`DocDownExcel-Extraction` and, under source filters, the six `DocDownExcel-Platform-*` requirements.

### Each worksheet becomes its own content part

**Test**: `DocDownExcel_Extract_TwoSheetWorkbook_WritesPartPerSheet`

Proves each worksheet becomes a part under `parts/`, the full layout is present, and an ordinary
image-free workbook records no note about absent pictures. Evidence for `DocDownExcel-Extraction`.

### Long prose and formulas survive verbatim

**Test**: `DocDownExcel_Extract_PreservesLongProseAndFormulas`

Proves a worksheet cell holding a long synthetic prose block reaches the content document intact and a
computed cell keeps its formula alongside its value. Evidence for `DocDownExcel-VerbatimCellValues`.

### A computed cell keeps its formula alongside its value

**Test**: `ExcelOpenXmlReader_Read_FormulaCell_KeepsFormulaAndValueAndAddress`

Proves a formula cell reaches the model with its formula, its cached value, and its address. Evidence
for `DocDownExcel-Formulas`.

### Sheet identity and cell addressing are preserved

**Test**: `ExcelOpenXmlReader_Read_TwoSheetWorkbook_ReturnsSheetsInOrderWithNames`

Proves the worksheets are returned in workbook order, each carrying its tab name. Evidence for
`DocDownExcel-CellAddressing`.

### A dense region renders a grid table alongside the listing

**Test**: `ExcelContentEmitter_Emit_DenseGrid_RendersTableAndListing`

Proves a table-shaped populated region produces a grid table in addition to the always-present
address/value/formula listing. Evidence for `DocDownExcel-WorksheetGridTable`.

### A chart is surfaced as its own content part

**Test**: `DocDownExcel_Extract_ChartWorkbook_WritesChartDataPart`

Proves a worksheet's chart becomes a `Chart` content part carrying its cached data, title, and axis
labels. Evidence for `DocDownExcel-Charts`.

### An unreadable chart records a short note and still keeps its part

**Test**: `ExcelContentEmitter_Emit_UnreadableChart_ReportsNoteAndKeepsChartPart`

Proves a chart part the reader could not interpret is named in a short note and still gets its own chart
part stating the reason. Evidence for `DocDownExcel-AttemptNotes`.

### A chart with no cached data states that fact in its part

**Test**: `ExcelContentEmitter_Emit_ChartWithoutCache_WritesChartPartOnly`

Proves a chart saved without cached points still gets a chart part stating that no table could be
produced, with no note because the read completed. Evidence for `DocDownExcel-Charts`.

### Drawing-shape annotations are extracted under their sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves the text of a callout drawn over a worksheet — text that lives in no cell — is written under the
sheet. Evidence for `DocDownExcel-DrawingShapeText`.

### Embedded images are written and linked from the sheet

**Test**: `DocDownExcel_Extract_ImageWorkbook_WritesEmbeddedImage`

Proves a workbook's embedded image is written to `images/` and the extraction reconciles cleanly, with
the sheet association recorded. Evidence for `DocDownExcel-EmbeddedImages`.

### A vector image workbook succeeds silently

**Test**: `DocDownExcel_Extract_VectorImageWorkbook_Succeeds`

Proves an EMF metafile is written unchanged, the run remains `Produced`, and no vector-only note is
recorded. Evidence for `DocDownExcel-EmbeddedImages`.

### An empty workbook reports zero-count inventory

**Test**: `ExcelContentEmitter_Emit_EmptyWorkbook_ReportsZeroCountInventory`

Proves a workbook with no worksheets reports zero worksheet and zero inline-image counts through the
content inventory rather than through a note about the document. Evidence for
`DocDownExcel-EmptyWorkbookInventory`.

### An empty worksheet states that it has no cell content

**Test**: `ExcelContentEmitter_Emit_EmptySheet_WritesEmptySheetMessage`

Proves a worksheet with no populated cells says so in its sheet part and records no note. Evidence for
`DocDownExcel-EmptyWorksheetContent`.

### The extractor surface matches the supported contract

**Test**: `ExcelOpenXmlExtractor_Descriptor_MatchesContract`

Proves the extractor's identity, display name, priority, supported format, and `PageRenderingApplicable`
flag match the supported surface. Evidence for `DocDownExcel-NoPageRendering`.

### A page-rendering request stays silent and succeeds

**Test**: `DocDownExcel_Extract_RenderPagesRequested_StaysSilentAndSucceeds`

Proves a page-rendering request against a workbook leaves the run `Produced`, writes no pages, and
records no note. Evidence for `DocDownExcel-NoPageRendering`.

### The content outline is reported from the model

**Test**: `ExcelContentEmitter_Emit_Workbook_ReportsContentFeatures`

Proves the outline counts — worksheets, populated cells, formulas, images, charts, drawing annotations,
and cached chart points — are reported from the model. Evidence for `DocDownExcel-ContentOutline`.

### A workbook with no images still reports a zero image count

**Test**: `ExcelContentEmitter_Emit_NoImages_ReportsZeroImageInventory`

Proves the backend explicitly reports that it looked for inline images and found none, with no note
about missing work. Evidence for `DocDownExcel-ContentOutline`.

### A legacy binary workbook is unreadable with a plain explanation

**Test**: `DocDownExcel_Extract_LegacyXls_IsUnreadableWithUnsupportedFormatExplanation`

Proves an engine with the Excel package registered reports an `.xls` request as unreadable with a plain
unsupported-format explanation, names the detected format, and gives no installation instruction.
Evidence for `DocDownExcel-LegacyFormatUnreadableExplanation`.

### Self-validation cases are exposed and run

**Test**: `ExcelOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the backend contributes its cases, that the round-trip case genuinely passes in the environment
under test, and that the page-rendering case reports a skip with a reason. Evidence for
`DocDownExcel-SelfValidation`.
