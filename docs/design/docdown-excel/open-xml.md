## OpenXml Subsystem

![DocDown.Excel Structure](DocDownExcelView.svg)

### Overview

The OpenXml subsystem is the managed Excel extraction backend. It reads an `.xlsx` package through the
Open XML SDK, populates the shared `ExcelWorkbookModel`, and hands the model to `ExcelContentEmitter`
which routes every byte through the sink. The subsystem is fully managed, carries no native asset, and
is deployable anywhere the .NET runtime is; it is the reason `DocDown.Excel` can claim
runtime-identifier agnosticism as a package property rather than an aspiration.

The subsystem exists because several responsibilities are cleanly separable: the descriptor the engine
selects and probes (`ExcelOpenXmlExtractor`), the SDK-to-model translation of cells and formulas
(`ExcelOpenXmlReader`), and the three focused readers factored out of it because each resolves a
distinct part of a worksheet's drawing — the image reader (`ExcelOpenXmlImageReader`), the chart reader
(`ExcelChartReader`), and the drawing-text reader (`ExcelDrawingTextReader`).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability` under 50 ms, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream may not seek |
| `IExtractionSink` | Outbound, via `ExcelContentEmitter` | .NET interface | The only output channel |
| `ExcelWorkbookModel` | Outbound, from the reader | .NET record | The pivot between reading and rendering |
| `SpreadsheetDocument` | Internal | Open XML SDK | Opened read-only |

### Design

**The descriptor.** `ExcelOpenXmlExtractor` declares `Id = "excel-openxml"`, `DisplayName = "Excel (Open
XML SDK)"`, `SupportedFormats = [Xlsx]`, `Priority = 10`, and `Capabilities = Text | EmbeddedImages |
DocumentMetadata | DocumentStructure`. `RenderedPages` is pointedly absent, and `PageRenderingApplicable`
is false because a workbook is non-paginated. `ProbeAvailability` returns `Available(Capabilities)`
unconditionally, with no I/O: there is nothing to probe because the SDK is a managed assembly shipped
inside this package.

**The self-test set.** Two cases: an `excel.openxml.parseRoundTrip` case that builds a one-sheet workbook
in memory with `SpreadsheetDocument.Create`, reads it back with the reader, and passes when the workbook
carries at least one worksheet; and an `excel.pageRendering` case that reports a reasoned skip because a
workbook is non-paginated and page rendering does not apply. Building rather than embedding a fixture
keeps the case free of a shipped binary payload and exercises the writer and reader together.

**The reader.** `ExcelOpenXmlReader` opens the package read-only, loads the shared-string table, and
walks each worksheet in workbook order. It keeps only cells that carry a value or a formula, resolves a
shared-string or inline-string cell to its whole text, resolves a boolean to `TRUE`/`FALSE`, and takes
every other type exactly as stored so no precision is lost — never truncating a value. It reads the
merged ranges, resolves the embedded images through `ExcelOpenXmlImageReader`, the charts through
`ExcelChartReader`, and the shape annotations through `ExcelDrawingTextReader`, and maps the OPC core
properties to the workbook metadata. It wraps a package it cannot open or a missing workbook part in a plain
`ExcelExtractionException` so the message reaching the caller is the backend's own explanation rather than
the SDK's raw package error.

**The image reader.** `ExcelOpenXmlImageReader` walks each worksheet's drawing part in workbook (tab)
order, yields every image part it holds as a passthrough with null pixel dimensions, deduplicates by
package-part identity, and records the 1-based tab index of every worksheet that references an image so
the sheet association survives extraction. Naming candidates come from the referencing picture's
non-visual properties, and a picture that also carries a scalable vector graphic yields that too.

**The chart reader.** `ExcelChartReader` walks a worksheet's drawing for graphic frames, reads each
referenced chart part with `XDocument` rather than the typed chart classes — because every plot type
spells its series the same way, so a name-driven walk covers plot types this product has never seen — and
recovers each series' cached values as last plotted, keeping the source reference alongside them. A chart
drawn with the newer extended chart grammar, and any chart part that cannot be parsed, is returned as a
failure reason rather than thrown, so the emitter can report it as a gap and keep going. A chart part no
frame references is still collected afterwards so an unreferenced chart is reported rather than lost.

**The drawing-text reader.** `ExcelDrawingTextReader` reads the text of every shape in a worksheet's
drawing, in drawing order, flattening group membership because a grouped callout is read no differently
by a person looking at the sheet, joining a shape's paragraphs into one readable annotation, and dropping
a shape that carries no text.

**Adverse cases.** The reader translates only the conditions it can recognize better than the SDK — a package
it cannot open and a missing workbook part — into `ExcelExtractionException`. Every other fault
propagates to Core, which converts it into an `ExtractorFailed` failure with the full layout still
written. Translating everything locally would duplicate that machinery and discard the SDK's own
explanation of what was wrong.

**Self-containment.** No SDK type reaches the public surface: `ExcelOpenXmlExtractor` is public but its
public members (from `IDocumentExtractor` and `ISelfValidating`) name Core types only; the reader and the
three focused readers are internal, exposed to the test project through `InternalsVisibleTo`. This
subsystem's inline supporting-type source files — reviewed in its review-set rather than in unit docs —
are the model records `ExcelDocumentModel` defines (`ExcelWorkbookModel`, `ExcelSheetModel`,
`ExcelCellModel`, `ExcelSheetImageRef`), the chart model records `ExcelChartModel` defines
(`ExcelChartData`, `ExcelChartSeries`, `ExcelChartPoint`), and `ExcelExtractionException`, the shared
exception Core surfaces. The `Text`-projection supporting types — `ExcelDiagnosticCodes` — belong to the
Markdown subsystem, where the output codes are documented.
