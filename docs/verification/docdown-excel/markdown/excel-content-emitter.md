## ExcelContentEmitter Verification Design

This document describes the unit-level verification strategy for `ExcelContentEmitter`, the model-to-sink
emission path.

### Verification Approach

`ExcelContentEmitter` is verified through unit tests in `Markdown/ExcelContentEmitterTests.cs` in
`DemaConsulting.DocDown.Excel.Tests`, driven from **hand-built models with no workbook behind them**
through a recording sink. This is deliberate: the emitter's contract is the mapping from a model onto the
parts, diagnostics, and gaps that reach the sink, and a hand-built model exercises each decision without a
workbook obscuring which one was responsible. Every value, sheet name, and series in the models is
synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `ExcelWorkbookModel` instances
- **Filesystem**: none; a recording sink captures the parts and reports
- **Mocking**: a recording sink; no other substitute
- **Isolation**: each test constructs its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelContentEmitter` unit test run passes when each worksheet becomes a titled
sheet part carrying its cell listing; when a computed cell's formula, value, and address all appear; when
a dense region renders a grid table and a sparse or very wide region keeps only the listing; when a long
or multi-line value is elided in the table with its full value verbatim in the listing and the elision
note appears only when an elision occurred; when merged ranges are stated as a note; when each chart
becomes a part after its sheet and is named under the sheet; when an unreadable, uncached, or bounded
chart is a counted gap and a chart-free workbook opens none; when shape text is written under the sheet;
when images are linked inline only where a path was returned; when a vector image records the informational
caveat; when an empty workbook degrades with a gap and an empty sheet is an informational diagnostic; when
a page-rendering request opens no gap; and when the content outline is reported from the model. A missing
part, a wrong outcome, or a weakened listing is a failure.

### Test Scenarios

#### Two worksheets become titled sheet parts

**Test**: `ExcelContentEmitter_Emit_TwoSheets_WritesTitledSheetParts`

Proves each worksheet becomes its own titled sheet part driven only by the model, and the sheet count is
reported found. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-EmitsModelContent` and
`DocDownExcel-Markdown-ExcelContentEmitter-WritesSheetParts`.

#### A formula cell renders its formula, value, and address

**Test**: `ExcelContentEmitter_Emit_FormulaCell_RendersFormulaValueAndAddress`

Proves the listing writes a computed cell's address, verbatim value, and formula together. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-RendersFormula`.

#### A dense grid renders a table and the listing

**Test**: `ExcelContentEmitter_Emit_DenseGrid_RendersTableAndListing`

Proves a table-shaped region produces a grid table in addition to the listing. The companion
`ExcelContentEmitter_Emit_SparseGrid_KeepsListingOnly` and
`ExcelContentEmitter_Emit_VeryWideGrid_KeepsListingOnly` prove a scatter and a pathologically wide region
keep only the listing. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-RendersGridTable`.

#### A long cell is elided in the table but verbatim in the listing

**Test**: `ExcelContentEmitter_Emit_LongCell_ElidedInTableVerbatimInListing`

Proves a long value is elided in the grid table while its full value stays verbatim in the listing;
`ExcelContentEmitter_Emit_ElidedCell_ExplainsTheElisionNearTheTable` and
`ExcelContentEmitter_Emit_NoElidedCell_OmitsTheElisionNote` prove the note appears exactly when an elision
occurred. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-ElidesLongCellsInTableOnly`.

#### Merged ranges are stated as a note

**Test**: `ExcelContentEmitter_Emit_MergedRanges_StatedAsNote`

Proves a worksheet's merged ranges are stated as a note beside the grid table a GFM table cannot span.
Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-NotesMergedRanges`.

#### A chart becomes a part after its sheet and is named under it

**Test**: `ExcelContentEmitter_Emit_SheetWithChart_WritesChartPartAfterSheet`

Proves a chart becomes its own part written after the sheet, and
`ExcelContentEmitter_Emit_SheetWithChart_NamesChartUnderTheSheet` proves the sheet part names the chart.
Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-EmitsChartParts`.

#### Chart shortfalls are counted gaps

**Test**: `ExcelContentEmitter_Emit_ChartWithoutCache_ReportsGap`

Proves an uncached chart is a counted gap; `ExcelContentEmitter_Emit_UnreadableChart_ReportsFailedGap`,
`ExcelContentEmitter_Emit_ChartBeyondBound_ReportsTruncationGap`, and
`ExcelContentEmitter_Emit_NoCharts_ReportsNoChartGap` prove an unreadable chart, a bounded chart, and a
chart-free workbook. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-ReportsChartGaps`.

#### Shape text is written under the sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves a worksheet's drawing-shape text is written under the sheet. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-WritesShapeText`.

#### Images are linked inline from the sheet

**Test**: `ExcelContentEmitter_Emit_SheetImage_LinksInlineFromSheet`

Proves an image a worksheet references is linked inline using the sink-returned path;
`ExcelContentEmitter_Emit_WithRasterImages_WritesThroughSink`,
`ExcelContentEmitter_Emit_NoImages_NoImagesGap`,
`ExcelContentEmitter_Emit_AutoImageLinks_ResolveOnDisk`, and
`ExcelContentEmitter_Emit_PerPartImageLinks_ResolveOnDisk` cover the sink write and the on-disk link
resolution. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-LinksImages`.

#### A vector image records the informational caveat

**Test**: `ExcelContentEmitter_Emit_VectorImage_WritesWithInfoCaveat`

Proves an EMF or WMF metafile is written unchanged and the readability caveat is informational rather than
a gap. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-ReportsVectorImageCaveat`.

#### An empty workbook degrades with a gap

**Test**: `ExcelContentEmitter_Emit_EmptyWorkbook_ReportsGap`

Proves a workbook with no worksheets degrades with a counted gap. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsEmptyWorkbookGap` and
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsGapsAndDiagnostics`.

#### An empty worksheet is an informational diagnostic

**Test**: `ExcelContentEmitter_Emit_EmptySheet_NotesInformationalDiagnostic`

Proves an empty worksheet is noted informationally and does not degrade the run. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-NotesEmptySheet`.

#### A page-rendering request stays silent

**Test**: `ExcelContentEmitter_Emit_RenderPagesRequested_StaysSilent`

Proves a page-rendering request opens no gap, because a workbook has no page grid to render. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-StaysSilentOnPageRequest`.

#### The content outline is reported from the model

**Test**: `ExcelContentEmitter_Emit_Workbook_ReportsContentFeatures`

Proves the outline counts are reported from the model. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsContentFeatures`.
