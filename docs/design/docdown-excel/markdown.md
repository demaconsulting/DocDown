## Markdown Subsystem

![DocDown.Excel Structure](DocDownExcelView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.Excel`. It defines how the
backend-neutral `ExcelWorkbookModel` becomes the output contract: the content emitter that walks the
model through the extraction sink — one content part per worksheet and one per chart — and the chart
writer that renders a chart's cached series as a table of categories against series values. Every
mapping decision that differentiates an Excel extraction — the verbatim address/value/formula listing,
the additive grid table and its elision note, the merged-range note, the chart data table, the
drawing-shape annotations, the inline image links, the content inventory counts, and the short notes
about incomplete chart or image steps — lives here and is testable from a hand-built model with no
workbook and no Open XML SDK.

The subsystem exists because reading a workbook and rendering what was read are separate concerns.
Holding the projection apart from the reader is what makes every mapping decision provable from a
hand-built model, and what keeps the output contract from acquiring a second implementation that could
drift from the first.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `ExcelWorkbookModel` | Inbound, from the reader | .NET record | The pivot between reading and emission |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from `IExtractionContext` | .NET record | Split mode, page request, image policy |

### Design

**The workbook model.** `ExcelWorkbookModel` is the whole cross-reader contract: the worksheets in
workbook order, the deduplicated embedded images, and the workbook metadata. Each `ExcelSheetModel`
carries its tab name, its non-empty cells in row-major order, its merged ranges, the images it
references, the charts it shows, and the text of the shapes drawn over it; an `ExcelCellModel` carries
the A1 address, the value verbatim (or null when the cell holds only a formula), and the formula
(or null).

**The content emitter.** `ExcelContentEmitter` is the single emission path. It writes embedded images
first so each worksheet can link them inline, writes one `ContentPartKind.Sheet` part per worksheet and
one `ContentPartKind.Chart` part per chart, reports document info and metadata, reports content
inventory counts from the model, and records short notes only when DocDown attempted a chart or image
step and could not complete it. An empty workbook is conveyed through zero-count inventory entries, an
empty worksheet says so in its sheet part, a vector image written successfully records no special note,
an unreadable chart records a short note and still keeps its own chart part, and a chart with no cached
data or a bounded table states that fact in the chart part itself.

**The chart writer.** `ExcelChartWriter` renders one chart's cached data: the labeling that makes the
numbers mean something — plot type, axis titles, per-series counts and source references — then a table
with a leading point-index column, one column per series, pipe characters escaped, and sparse indices
honored so a hole in the cache renders as the hole it is. A chart part that could not be read states the
reason, a chart with no cached data points states that no table could be produced, and a bounded table
states how many plotted points were omitted.

**The reporting rule.** The subsystem reports what DocDown extracted and where. Content inventory counts
say what kinds of content were looked for and how many were found, including zero when the backend
explicitly looked. A short plain note is reserved for an attempted step DocDown could not complete,
such as an unreadable chart part or an image write the caller's limits or `ForcePng` request prevented.
A note never characterizes the workbook itself.

**The unit split.** Two units divide the work along the boundaries their responsibilities draw:
`ExcelContentEmitter` owns the sink walk, worksheet rendering, content inventory, and note policy;
`ExcelChartWriter` owns chart-part wording and the plotted-point bound. The supporting model,
chart-model, and `NamespaceDoc` types live in the same subsystem folder because they are the vocabulary
of the units, not units of their own.
