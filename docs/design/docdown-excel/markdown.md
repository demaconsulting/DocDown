## Markdown Subsystem

![DocDown.Excel Structure](DocDownExcelView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.Excel`. It defines how the
backend-neutral `ExcelWorkbookModel` becomes the output contract: the content emitter that walks the
model through the extraction sink — one content part per worksheet and one per chart — and the chart
writer that renders a chart's cached series as a table of categories against series values. Every
mapping decision that differentiates an Excel extraction — the verbatim address/value/formula listing,
the additive grid table and its elision note, the merged-range note, the chart data table, the
drawing-shape annotations, the inline image links, and the honest gap-versus-diagnostic policy — lives
here and is testable from a hand-built model with no workbook and no Open XML SDK.

The subsystem exists because reading a workbook and rendering what was read are separate concerns.
Holding the whole projection apart from the reader is what makes every mapping decision provable from a
hand-built model, and what keeps the output contract from acquiring a second implementation that could
drift from the first.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `ExcelWorkbookModel` | Inbound, from the reader | .NET record | The pivot between reading and rendering |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from `IExtractionContext` | .NET record | Split mode, force-PNG, images |
| `ExcelDiagnosticCodes` | Outbound to callers | .NET constants | The `XLSX0001`–`XLSX0006` codes this package owns |

### Design

**The workbook model.** `ExcelWorkbookModel` is the whole cross-reader contract: the worksheets in
workbook order, the deduplicated embedded images, and the workbook metadata. Each `ExcelSheetModel`
carries its tab name, its non-empty cells in row-major order, its merged ranges, the images it
references, the charts it shows, and the text of the shapes drawn over it; an `ExcelCellModel` carries
the A1 address, the value verbatim (or null when the cell holds only a formula), and the formula (or
null). These model records, and the chart model records, are the inbound contract this subsystem
renders; they are defined and documented by the OpenXml subsystem, where the files live, and are the
pivot both subsystems agree on.

**The content emitter.** `ExcelContentEmitter` is the single emission path. It counts the worksheets
and charts, writes the embedded images first so each worksheet can link its pictures inline, writes one
`ContentPartKind.Sheet` part per worksheet and one `ContentPartKind.Chart` part per chart, reports the
document info and metadata, and reports every diagnostic and counted gap the model implies. Its gap
policy is the whole honesty of the extraction: an empty workbook is a counted gap that degrades, an
empty sheet is an informational diagnostic that does not, a vector image carries an informational
readability caveat, and an unreadable, uncached, or bounded chart is a counted gap. The worksheet
renderer emits the verbatim listing always and the grid table additively, eliding a long or multi-line
value in the table alone with a note that names the cause and points to the full value in the listing.

**The chart writer.** `ExcelChartWriter` renders one chart's cached data: the labeling that makes the
numbers mean something — plot type, axis titles, per-series counts and source references — then a table
with a leading point-index column, one column per series, pipe characters escaped, and sparse indices
honored so a gap in the cache renders as the gap it is. The rendered table is bounded at a fixed number
of plotted points, and both the table and the emitter state what was dropped. A chart that could not be
read and one that cached no points are stated in the part itself so neither is ever a silent absence.

**The diagnostic-code contract.** `ExcelDiagnosticCodes` defines the six codes this package owns —
`XLSX0001` `NoWorksheets`, `XLSX0002` `EmptySheet`, `XLSX0003` `VectorImageWrittenAsIs`, `XLSX0004`
`ChartUnreadable`, `XLSX0005` `ChartWithoutCachedData`, `XLSX0006` `ChartPointsTruncated`. The distinct
`XLSX` prefix cannot collide with Core's `DD` range or another package's prefix in any shared manifest,
which makes ownership self-evident. It is a supporting type documented here.

**The unit split.** Two units divide the work along the boundaries their responsibilities draw:
`ExcelContentEmitter` owns the sink walk, the worksheet and grid rendering, and the whole
gap-and-diagnostic policy; `ExcelChartWriter` owns the rendering of a chart's cached series and the
plotted-point bound. The supporting model, chart-model, diagnostic-code, and `NamespaceDoc` types live
in the same subsystem folder because they are the vocabulary of the units, not units of their own.
