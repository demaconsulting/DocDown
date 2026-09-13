# DocDown.Excel Verification Design

This document describes the system-level verification strategy for `DocDown.Excel`, the Excel
extraction package.

## Verification Approach

`DocDown.Excel` is verified through system-level integration tests in `DocDownExcelTests.cs`, and unit
tests per unit, all in `DemaConsulting.DocDown.Excel.Tests`, running on xUnit v3 across net8.0, net9.0,
and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with `ContractAssert.NoViolations`, which runs Core's
contract verifier over the produced folder and reports any disagreement between what the manifest claims
and what is on disk. This is the highest-value assertion available to this package: it makes a dishonest
extraction a test failure rather than a review finding, and it applies to degraded and failed runs as
well as clean ones. The reconciliation covers the legacy `.xls` refusal scenario, so the full-layout
claim on a structured failure is machine-checked alongside the successful ones.

### One backend, one reader, one emitter

The package ships one backend: the managed Open XML reader. It reads a workbook into the reader-neutral
model and drives the content emitter, so every mapping decision is made in exactly one place and can be
proved from a hand-built model with no workbook behind it. A legacy binary `.xls` has no reader here and
none anywhere in DocDown, so the request fails with a structured, reasoned outcome — never with an
exception, and never with a remedy that promises a capability that does not exist.

### The verbatim guarantee is the headline property under test

The Excel intent forbids losing a value's length or precision, so the suite asserts it directly: a
worksheet cell holding a long synthetic prose block survives intact, and a stored number reaches the
output exactly as stored. The grid table is additive and its elision is table-only, so a scenario proves
a long value is elided in the table while its full value stays verbatim in the listing below — the
guarantee is never weakened by a rendering choice.

### Page rendering is a request that stays silent, not a shortfall

Unlike the Word package, whose page-rendering request degrades with a counted gap, a page-rendering
request against a workbook is treated as not applicable: a workbook has no page grid. A scenario proves
that requesting rendered pages leaves the run succeeded, opens no pages gap, and records the
non-applicability as an environment fact carrying the reason — the caller is told plainly, without a
false shortfall.

### Fixtures are generated, never committed

Every workbook the suite uses is built at test time by the Open XML SDK writer in
`TestData/XlsxFixtures.cs`. The legacy `.xls` scenario writes a placeholder byte sequence whose extension
drives format detection, because the selection path never opens the file: no registered backend supports
the format. No binary `.xlsx` or `.xls` is committed, so the repository stays text-only and no question
arises about the provenance or licensing of a sample workbook. Every value, sheet name, chart series, and
annotation in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input workbook and the
  extraction output
- **Inputs**: SpreadsheetML workbooks generated at test time by the Open XML SDK writer, plus a short byte
  sequence for the legacy `.xls` refusal scenario; no committed binary fixtures and no network access
- **Mocking**: none for the integration scenarios — every test drives the real engine and the real Open
  XML backend; the emitter and chart units are additionally exercised from hand-built models through a
  recording sink
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no unexpected
  exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations.
- No adverse workbook — legacy, empty, or unreadable — causes an exception to escape to the caller, and
  every one of them still produces the full output layout.
- Every cell value reaches the output verbatim at full length and full precision; a long prose value is
  never truncated, and where it is elided in a grid table its full value is present in the listing.
- Every chart a worksheet shows is surfaced as its own content part carrying its cached data, and every
  chart that could not be read, cached no data, or was bounded is counted and named.
- A page-rendering request leaves the run succeeded, opens no pages gap, and records the non-applicability
  with its reason.
- The legacy binary `.xls` format is refused with a coded structured failure whose remedy states that
  DocDown does not support legacy binary formats, never an exception and never an instruction the reader
  could act on and fail at.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
Platform requirements are covered by the source-filtered runs of the selection scenario.

### The default engine registers the Excel backend

**Test**: `AddExcel_RegistersSingleOpenXmlBackend`

Proves the one-liner the host uses to add Excel support registers the Open XML backend on the resulting
engine and registers nothing else, so the set of active backends is a decision readable in host code
rather than a deployment accident. Evidence for `DocDownExcel-Registration`.

### The Open XML backend is selected and produces the contract layout

**Test**: `DocDownExcel_Extract_Xlsx_SelectsOpenXml`

Proves a clean extraction: the managed Open XML backend is selected for an `.xlsx`, the full output
layout is produced, and the contract verifier reports no violations. This is also the anchor for the
platform requirements. Evidence for `DocDownExcel-Extraction`, and under source filters the six
`DocDownExcel-Platform-*` requirements.

### Long prose and formulas survive verbatim

**Test**: `DocDownExcel_Extract_PreservesLongProseAndFormulas`

Proves a worksheet cell holding a long synthetic prose block reaches the content document intact and a
computed cell keeps its formula alongside its value, so the workbook's information is preserved at full
length and precision. Evidence for `DocDownExcel-VerbatimCellValues`.

### A computed cell keeps its formula alongside its value

**Test**: `ExcelOpenXmlReader_Read_FormulaCell_KeepsFormulaAndValueAndAddress`

Proves a formula cell reaches the model with its formula, its cached value, and its address, so the
relationship and one evaluation of it are both citable. Evidence for `DocDownExcel-Formulas`.

### Sheet identity and cell addressing are preserved

**Test**: `ExcelOpenXmlReader_Read_TwoSheetWorkbook_ReturnsSheetsInOrderWithNames`

Proves the worksheets are returned in workbook order, each carrying its tab name, so a fact can be cited
back to the sheet and cell that hold it. Evidence for `DocDownExcel-CellAddressing`.

### A dense region renders a grid table alongside the listing

**Test**: `ExcelContentEmitter_Emit_DenseGrid_RendersTableAndListing`

Proves a table-shaped populated region produces a grid table in addition to the always-present
address/value/formula listing, restoring the row and column relationships without replacing the lossless
ledger. Evidence for `DocDownExcel-WorksheetGridTable`.

### A chart is surfaced as its own content part

**Test**: `DocDownExcel_Extract_ChartWorkbook_WritesChartDataPart`

Proves a worksheet's chart becomes a `Chart` content part carrying the data it cached as last plotted, so
a chart's information survives into the extraction as data. Evidence for `DocDownExcel-Charts`.

### A chart that cached no data is a counted gap

**Test**: `ExcelContentEmitter_Emit_ChartWithoutCache_ReportsGap`

Proves a chart saved without a value cache is reported as a counted gap rather than dropped silently, so
a workbook that lost chart data says so. Evidence for `DocDownExcel-ChartFidelity`.

### Drawing-shape annotations are extracted under their sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves the text of a callout drawn over a worksheet — text that lives in no cell — is written under the
sheet, so a reader of the listing alone still sees it. Evidence for `DocDownExcel-DrawingShapeText`.

### Embedded images are written and linked from the sheet

**Test**: `DocDownExcel_Extract_ImageWorkbook_WritesEmbeddedImage`

Proves a workbook's embedded image is written to `images/` and the extraction reconciles cleanly, with
the sheet association recorded. Evidence for `DocDownExcel-EmbeddedImages`.

### A vector image is written as-is with a caveat

**Test**: `DocDownExcel_Extract_VectorImageWorkbook_Succeeds`

Proves an EMF metafile is written unchanged, `XLSX0003` records the readability caveat as an informational
diagnostic, and no images gap is opened — a well-formed vector-bearing workbook does not degrade. Evidence
for `DocDownExcel-VectorImages`.

### An empty workbook degrades with a counted gap

**Test**: `ExcelContentEmitter_Emit_EmptyWorkbook_ReportsGap`

Proves a workbook with no worksheets degrades with a counted gap naming the absence, so an empty content
document is a stated fact rather than a mystery. Evidence for `DocDownExcel-EmptyWorkbook`.

### An empty worksheet is an informational diagnostic

**Test**: `ExcelContentEmitter_Emit_EmptySheet_NotesInformationalDiagnostic`

Proves a worksheet with no populated cells is noted informationally and does not degrade the run, so a
complete extraction is not falsely reported incomplete. Evidence for `DocDownExcel-EmptyWorksheet`.

### The managed backend declares exactly the deliverable capabilities

**Test**: `ExcelOpenXmlExtractor_Descriptor_MatchesContract`

Proves the descriptor's identity, priority, supported format, and declared capabilities — text, embedded
images, document metadata, and document structure — match the deliverable set, and — as an express
counterpart — that the rendered-pages capability is not declared and page rendering is not applicable.
Evidence for `DocDownExcel-DeclaredCapabilities`.

### A page-rendering request stays silent and succeeds

**Test**: `DocDownExcel_Extract_RenderPagesRequested_StaysSilentAndSucceeds`

Proves a page-rendering request against a workbook leaves the run succeeded, opens no pages gap, and
records the non-applicability with its reason — the workbook is non-paginated, so rendering applies to
nothing. Evidence for `DocDownExcel-NoPageRendering`.

### The content outline is reported from the model

**Test**: `ExcelContentEmitter_Emit_Workbook_ReportsContentFeatures`

Proves the outline counts — worksheets, populated cells, formulas, images, charts, drawing annotations,
and cached chart points — are reported from the model, giving a reader the shape of the workbook. Evidence
for `DocDownExcel-ContentOutline`.

### A legacy binary workbook is refused with an honest remedy

**Test**: `DocDownExcel_Extract_LegacyXls_FailsWithUnsupportedFormatRemedy`

Proves an engine with the Excel package registered fails an `.xls` request with a structured failure and a
remedy that names the format and states plainly that DocDown does not support the legacy binary Office
formats — with no package named, no environment precondition, and no install verb, because no such route
exists. The full contract layout is still produced and reconciles cleanly. Evidence for
`DocDownExcel-LegacyFormatRefusal`.

### Self-validation cases are exposed and run

**Test**: `ExcelOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the backend contributes its cases, that the round-trip case genuinely passes in the environment
under test, and that the page-rendering capability the backend does not claim reports a skip with a reason
rather than a failure — the property a traceability pipeline depends on. Evidence for
`DocDownExcel-SelfValidation`.
