## ExcelContentEmitter

![DocDown.Excel Structure](ExcelView.svg)

### Purpose

`ExcelContentEmitter` is the single emission path: it turns a read `ExcelWorkbookModel` into the output
contract. Its single responsibility is that every output the model implies — worksheet parts, chart
parts, images, content inventory counts, and short notes about attempted steps that could not complete —
is produced in one place, driven only by the model and never by how the model was read.

### Data Model

`ExcelContentEmitter` is an `internal static class`. It holds no state. Named constants pin the
grid-table shape decisions — the minimum row span, the minimum and maximum column spans, the minimum
fill density, and the maximum table-cell length before elision — together with the elision marker and
its explanatory note.

### Key Methods

- **`ValueTask EmitAsync(IExtractionSink sink, ExtractionOptions options, ExcelWorkbookModel model,
  CancellationToken)`** — the whole emission. It writes embedded images first so each worksheet can
  link its pictures inline, writes one `ContentPartKind.Sheet` part per worksheet followed by one
  `ContentPartKind.Chart` part per chart the worksheet shows, records a note for each unreadable chart,
  reports document info and metadata, reports image-write notes for caller-supplied size-limit misses,
  and reports the content inventory from the model. An empty workbook is conveyed by
  zero-count inventory entries; an empty worksheet says so in its own sheet part.
- **`RenderSheet`** (private) — renders one worksheet: a heading, the merged-range note, the additive
  grid table when the region is dense and table-shaped, then the always-present address/value/formula
  listing, then the chart references, shape text, and inline image links. The listing is the lossless
  ledger; the grid table restores row and column relationships without ever replacing it.
- **`ReportContentFeatures`** (private) — reports the inventory counts (`worksheets`, `populated cells`,
  `cells carrying a formula`, `inline images`, `charts`, `annotated drawing shapes`, and
  `cached chart data points`) from the model and marks each count `LookedFor = true`, so a zero remains
  visible when the backend explicitly looked for that feature.
- **`ReportImageNotes`** (private) — records plain-language notes when the image write attempted work the
  backend could not finish: images skipped because they exceed caller-supplied size limits.
- **`BuildUnreadableChartNote`** (private) — builds the one-sentence note naming the chart and part URI
  when the chart part could not be read.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed between worksheets and
propagates as `OperationCanceledException`. No other error condition arises here: the emitter writes only
through the sink and reads only the model, so an adverse workbook was already turned into structured
content, a note, or an unreadable result upstream in the reader or Core.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `ContentPart`, `ContentPartKind`,
  `ExtractionNote`, `DocumentInfo`, `ContentFeature`, `EmbeddedImageWriter`, and `EmbeddedImageWriteResult`.
- **ExcelWorkbookModel** and the chart model — the read model it renders. See *ExcelOpenXmlReader
  Design*.
- **ExcelChartWriter** — renders each chart's cached series. See *ExcelChartWriter Design*.

### Callers

`ExcelOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after the reader produces the model. Nothing else
calls it.
