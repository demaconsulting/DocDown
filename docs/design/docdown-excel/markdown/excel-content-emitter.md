## ExcelContentEmitter

![DocDown.Excel Structure](DocDownExcelView.svg)

### Purpose

`ExcelContentEmitter` is the single emission path: it turns a read `ExcelWorkbookModel` into the output
contract. Its single responsibility is that every output the model implies — the worksheet parts, the
chart parts, the images, the content outline, the diagnostics, and the gaps — is produced in one place,
driven only by the model and never by how the model was read.

### Data Model

`ExcelContentEmitter` is an `internal static class`. It holds no state; a private `ChartAccounting`
class accumulates per-emission scratch state so every chart shortfall is reported in one place, and is
confined to a single emission and never shared. Named constants pin the grid-table shape decisions —
the minimum row span, the minimum and maximum column spans, the minimum fill density, and the maximum
table-cell length before elision — and the elision marker and its explanatory note.

### Key Methods

- **`ValueTask<bool> EmitAsync(IExtractionSink sink, ExtractionOptions options, ExcelWorkbookModel
  model, CancellationToken)`** — the whole emission. It counts the worksheets and charts as found,
  reports the empty-workbook gap when there are no worksheets, writes the embedded images first so each
  worksheet can link its pictures inline, writes one `ContentPartKind.Sheet` part per worksheet followed
  by one `ContentPartKind.Chart` part per chart the worksheet shows, notes an empty sheet
  informationally, reports the document info and metadata, and reports the image and chart gaps and the
  content outline. Returns whether any gap was reported. A page-rendering request is deliberately not
  answered here: the engine records the non-applicability, so the emitter stays silent rather than
  reporting a shortfall for a request a non-paginated workbook cannot lose.
- **`RenderSheet`** (private) — renders one worksheet: a heading, the merged-range note, the additive
  grid table when the region is dense and table-shaped, then the always-present address/value/formula
  listing, then the chart references, shape texts, and inline image links. The listing is the lossless
  ledger; the grid table restores row and column relationships without ever replacing it.
- **`AppendGridTable`** (private) — emits a grid table only for a populated region that clears the row,
  column, width, and density thresholds; a value too long or containing a line break is elided there with
  the marker, and its full verbatim value stays in the listing below.
- **`ReportCharts`** (private) — reports a counted gap for each chart that could not be read (`XLSX0004`),
  cached no data (`XLSX0005`), or was bounded (`XLSX0006`), and reports nothing for a chart-free workbook.
- **`ReportImages`** (private) — reports the vector-metafile caveat (`XLSX0003`, informational), a
  size-skip gap, and an unhonored force-PNG gap; a workbook that embeds no images reports nothing.
- **`ReportContentFeatures`** (private) — reports the outline counts (worksheets, populated cells, cells
  carrying a formula, inline images, charts, annotated drawing shapes, cached chart data points) from the
  model; Core drops any zero count.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed between worksheets and
propagates as `OperationCanceledException`. No other error condition arises here: the emitter writes only
through the sink and reads only the model, so an adverse workbook was already turned into a structured
failure upstream in the reader or Core.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `ContentPart`, `ContentPartKind`,
  `ExtractionDiagnostic`, `ExtractionGap`, `GapKind`, `GapScope`, `DiagnosticSeverity`, `DocumentInfo`,
  `ContentFeature`, `EmbeddedImageWriter`.
- **ExcelWorkbookModel** and the chart model — the read model it renders. See *ExcelOpenXmlReader Design*.
- **ExcelChartWriter** — renders each chart's cached series. See *ExcelChartWriter Design*.
- **ExcelDiagnosticCodes** — the `XLSX` diagnostic codes it reports.

### Callers

`ExcelOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after the reader produces the model. Nothing else
calls it.
