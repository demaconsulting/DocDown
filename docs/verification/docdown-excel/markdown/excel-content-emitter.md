## ExcelContentEmitter Verification Design

This document describes the unit-level verification strategy for `ExcelContentEmitter`, the model-to-sink
emission path.

### Verification Approach

`ExcelContentEmitter` is verified through unit tests in `Markdown/ExcelContentEmitterTests.cs` in
`DemaConsulting.DocDown.Excel.Tests`, driven from **hand-built models with no workbook behind them**
through a recording sink. This is deliberate: the emitter's contract is the mapping from a model onto
the parts, counts, and notes that reach the sink, and a hand-built model exercises each decision
without a workbook obscuring which one was responsible. Every value, sheet name, and series in the
models is synthetic.

The note-only rule is exercised directly by the unreadable-chart scenario. The image scenarios prove the
same emission path writes images, preserves zero counts when no images are present, and keeps a
successfully written vector image silent; image-size and `ForcePng` note branches share that same
reporting rule but have no dedicated Excel-specific test.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `ExcelWorkbookModel` instances
- **Filesystem**: none; a recording sink captures the parts and reports
- **Mocking**: a recording sink; no other substitute
- **Isolation**: each test constructs its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelContentEmitter` unit test run passes when each worksheet becomes a titled
sheet part carrying its cell listing; when a computed cell's formula, value, and address all appear;
when a dense region renders a grid table and a sparse or very wide region keeps only the listing; when a
long or multi-line value is elided in the table with its full value verbatim in the listing and the
elision note appears only when an elision occurred; when merged ranges are stated as a note; when each
chart becomes a part after its sheet and is named under the sheet; when an unreadable chart records a
short note and a chart with no cached data states that fact in its part; when shape text is written
under the sheet; when images are linked inline only where a path was returned; when a workbook with no
images or no worksheets reports zero counts for the features it looked for; when a successfully written
vector image records no vector-only note; when an empty worksheet states that it has no cell content;
when a page-rendering request records no note; and when the content outline is reported from the model.
A missing part, a wrong note, a missing zero count, or a weakened listing is a failure.

### Test Scenarios

#### Two worksheets become titled sheet parts

**Test**: `ExcelContentEmitter_Emit_TwoSheets_WritesTitledSheetParts`

Proves each worksheet becomes its own titled sheet part driven only by the model, and the worksheet
count is reported through the content inventory. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-EmitsModelContent` and
`DocDownExcel-Markdown-ExcelContentEmitter-WritesSheetParts`.

#### A formula cell renders its formula, value, and address

**Test**: `ExcelContentEmitter_Emit_FormulaCell_RendersFormulaValueAndAddress`

Proves the listing writes a computed cell's address, verbatim value, and formula together. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-RendersFormula`.

#### A dense grid renders a table and the listing

**Test**: `ExcelContentEmitter_Emit_DenseGrid_RendersTableAndListing`

Proves a table-shaped region produces a grid table in addition to the listing. The companion
`ExcelContentEmitter_Emit_SparseGrid_KeepsListingOnly` and
`ExcelContentEmitter_Emit_VeryWideGrid_KeepsListingOnly` prove a scatter and a pathologically wide
region keep only the listing. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-RendersGridTable`.

#### A long cell is elided in the table but verbatim in the listing

**Test**: `ExcelContentEmitter_Emit_LongCell_ElidedInTableVerbatimInListing`

Proves a long value is elided in the grid table while its full value stays verbatim in the listing;
`ExcelContentEmitter_Emit_ElidedCell_ExplainsTheElisionNearTheTable` and
`ExcelContentEmitter_Emit_NoElidedCell_OmitsTheElisionNote` prove the note appears exactly when an
elision occurred. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-ElidesLongCellsInTableOnly`.

#### Merged ranges are stated as a note

**Test**: `ExcelContentEmitter_Emit_MergedRanges_StatedAsNote`

Proves a worksheet's merged ranges are stated as a note beside the grid table a GFM table cannot span.
Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-NotesMergedRanges`.

#### A chart becomes a part after its sheet and is named under it

**Test**: `ExcelContentEmitter_Emit_SheetWithChart_WritesChartPartAfterSheet`

Proves a chart becomes its own part written after the sheet, and
`ExcelContentEmitter_Emit_SheetWithChart_NamesChartUnderTheSheet` proves the sheet part names the
chart. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-EmitsChartParts`.

#### An unreadable chart records a short note

**Test**: `ExcelContentEmitter_Emit_UnreadableChart_ReportsNoteAndKeepsChartPart`

Proves a chart part the reader could not interpret records a short note naming the chart and still gets
its own chart part stating the reason. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsAttemptNotes`.

#### A chart with no cached data states that fact in its part

**Test**: `ExcelContentEmitter_Emit_ChartWithoutCache_WritesChartPartOnly`

Proves an uncached chart states that no data table could be produced, with no sink note because the read
completed. `ExcelContentEmitter_Emit_ChartBeyondBound_StatesTruncationInChartPart` proves a bounded
chart states the omission in the chart part, and `ExcelContentEmitter_Emit_NoCharts_ReportsNoChartNotes`
proves a chart-free workbook records no chart note. Evidence for
`DocDownExcel-Markdown-ChartRendering`.

#### Shape text is written under the sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves a worksheet's drawing-shape text is written under the sheet. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-WritesShapeText`.

#### Images are linked inline from the sheet

**Test**: `ExcelContentEmitter_Emit_SheetImage_LinksInlineFromSheet`

Proves an image a worksheet references is linked inline using the sink-returned path;
`ExcelContentEmitter_Emit_WithRasterImages_WritesThroughSink`,
`ExcelContentEmitter_Emit_AutoImageLinks_ResolveOnDisk`, and
`ExcelContentEmitter_Emit_PerPartImageLinks_ResolveOnDisk` cover the sink write and the on-disk link
resolution. Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-LinksImages`.

#### Zero-count inventory is preserved when the backend looked and found none

**Test**: `ExcelContentEmitter_Emit_EmptyWorkbook_ReportsZeroCountInventory`

Proves a workbook with no worksheets reports zero worksheet and zero inline-image counts. The companion
`ExcelContentEmitter_Emit_NoImages_ReportsZeroImageInventory` proves the same zero-count rule for an
ordinary workbook that embeds no images. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsZeroCountInventory` and
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsContentFeatures`.

#### A vector image written successfully records no special note

**Test**: `ExcelContentEmitter_Emit_VectorImage_WritesSilently`

Proves an EMF or WMF metafile written unchanged records no vector-only note. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-LinksImages`.

#### An empty worksheet states that it has no cell content

**Test**: `ExcelContentEmitter_Emit_EmptySheet_WritesEmptySheetMessage`

Proves an empty worksheet says so in its sheet part and records no note. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-WritesEmptyWorksheetMessage`.

#### A page-rendering request stays silent

**Test**: `ExcelContentEmitter_Emit_RenderPagesRequested_StaysSilent`

Proves a page-rendering request records no note, because a workbook has no page grid to render.
Evidence for `DocDownExcel-Markdown-ExcelContentEmitter-StaysSilentOnPageRequest`.

#### The content outline is reported from the model

**Test**: `ExcelContentEmitter_Emit_Workbook_ReportsContentFeatures`

Proves the outline counts are reported from the model. Evidence for
`DocDownExcel-Markdown-ExcelContentEmitter-ReportsContentFeatures`.
