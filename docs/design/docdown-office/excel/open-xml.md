## OpenXml Subsystem

![DocDown.Excel Structure](ExcelView.svg)

### Overview

The OpenXml subsystem is the managed Excel extraction backend. It reads an `.xlsx` package through the
Open XML SDK, populates the shared `ExcelWorkbookModel`, and hands the model to `ExcelContentEmitter`,
which routes every byte through the sink. The subsystem is fully managed, carries no native asset, and
is deployable anywhere the .NET runtime is.

The subsystem exists because several responsibilities are cleanly separable: the extractor surface the
engine selects and probes (`ExcelOpenXmlExtractor`), the SDK-to-model translation of cells and formulas
(`ExcelOpenXmlReader`), and the focused readers factored out of it because each resolves a distinct part
of a worksheet's drawing layer — the image reader (`ExcelOpenXmlImageReader`), the chart reader
(`ExcelChartReader`), and the drawing-text reader (`ExcelDrawingTextReader`).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap probe; no I/O |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Cheap enumeration; work runs in the case delegate |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream may not seek |
| `IExtractionSink` | Outbound, via `ExcelContentEmitter` | .NET interface | The only output channel |
| `ExcelWorkbookModel` | Outbound, from the reader | .NET record | The pivot between reading and emission |
| `SpreadsheetDocument` | Internal | Open XML SDK | Opened read-only |

### Design

**The extractor surface.** `ExcelOpenXmlExtractor` declares `Id = "excel-openxml"`,
`DisplayName = "Excel (Open XML SDK)"`, `SupportedFormats = [Xlsx]`, and `Priority = 10`.
`PageRenderingApplicable` is `false` because a workbook is non-paginated. `ProbeAvailability` returns
`ExtractorAvailability.Available()` unconditionally, with no I/O: there is nothing to probe because the
SDK is a managed assembly shipped inside this package.

**The self-test set.** Two cases are exposed: an `excel.openxml.parseRoundTrip` case that builds a
embedded workbook authored in Microsoft Excel, reads it with the reader, and
passes when the workbook carries at least one worksheet; and an `excel.pageRendering` case that reports
a reasoned skip because a workbook is non-paginated and page rendering does not apply.

**The reader.** `ExcelOpenXmlReader` opens the package read-only, loads the shared-string table, and
walks each worksheet in workbook order. It keeps only cells that carry a value or a formula, resolves a
shared-string or inline-string cell to its whole text, resolves a boolean to `TRUE` or `FALSE`, and
reads every other type exactly as stored so no precision is lost. It reads merged ranges, embedded
images through `ExcelOpenXmlImageReader`, charts through `ExcelChartReader`, shape annotations through
`ExcelDrawingTextReader`, and the OPC core properties into workbook metadata.

**The image reader.** `ExcelOpenXmlImageReader` walks each worksheet's drawing part in workbook order,
yields every image part it holds as a passthrough with null pixel dimensions, deduplicates by
package-part identity, and records the 1-based tab index of every worksheet that references an image so
the sheet association survives extraction.

**The chart reader.** `ExcelChartReader` walks a worksheet's drawing for graphic frames, reads each
referenced chart part with `XDocument` rather than the typed chart classes, and recovers each series'
cached values as last plotted, keeping the source reference alongside them. A chart drawn with the newer
extended chart grammar, or any chart part that cannot be parsed, is returned with a failure reason
rather than thrown so the downstream reporting path can preserve the chart part and state plainly that
DocDown could not finish reading it.

**The drawing-text reader.** `ExcelDrawingTextReader` reads the text of every shape in a worksheet's
drawing, in drawing order, flattening group membership because a grouped callout is read no differently
by a person looking at the sheet, joining a shape's paragraphs into one readable annotation, and
skipping a shape that carries no text.

**Adverse cases.** The reader translates only the conditions it can recognize better than the SDK — a
package it cannot open and a missing workbook part — into `ExcelExtractionException`. Every other fault
propagates to Core, which converts it into a structured `Unreadable` result with the full layout still
written.

**Self-containment.** No SDK type reaches the public surface: `ExcelOpenXmlExtractor` is public but its
public members name Core types only; the reader and the focused readers are internal, exposed to the
test project through `InternalsVisibleTo`. The subsystem's supporting-type source files define the model
records (`ExcelWorkbookModel`, `ExcelSheetModel`, `ExcelCellModel`, `ExcelSheetImageRef`), the chart
model records (`ExcelChartData`, `ExcelChartSeries`, `ExcelChartPoint`), and `ExcelExtractionException`.
