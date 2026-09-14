# DocDown.Excel System Design

![DocDown.Excel Structure](ExcelView.svg)

`DocDown.Excel` is the Excel extraction system for the DocDown output contract. It reads a workbook's
worksheet cell values, formulas, charts, drawing annotations, embedded images, and metadata, and writes
those artifacts through the `IExtractionSink` the engine supplies. It is a separately distributed NuGet
package that a host registers explicitly alongside `DocDown.Core`, and it ships one managed Open XML
backend for `.xlsx` workbooks. Legacy binary `.xls` workbooks are not extracted; they end as a
structured `Unreadable` result with a plain explanation that the format is unsupported.

A workbook, in this product's domain, is a prose and data container, not a picture. Its cells hold
requirement quotations, pasted transcripts, exact numbers, and the formulas that produced them. The
whole design follows from that: cell values are preserved verbatim at full precision and full length,
formulas stay beside their computed values, every fact keeps its sheet identity and cell address, chart
caches are reported as data, and page rendering is deliberately treated as not applicable.

## Architecture

The system has two subsystems and one direct unit:

- **ExcelDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddExcel` registers the
  managed backend. It stays reflection-free and names no Open XML SDK type on its public surface, so a
  host can reference the package's registration surface without the SDK's types entering its own
  compilation.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that walks the workbook
  model through the sink — one content part per worksheet and one per chart — and the chart writer that
  renders a chart's cached series as a table. Every mapping decision that differentiates an Excel
  extraction — the verbatim cell listing, the additive grid table, chart data tables, drawing-shape
  annotations, inline image links, content inventory counts, and short notes about incomplete steps —
  lives here and is testable from a hand-built model with no workbook behind it.
- **OpenXml** *(subsystem)* is the backend: the extractor the engine selects, the reader that turns a
  `SpreadsheetDocument` into the shared model, the image reader that yields each embedded image's bytes
  and sheet association, the chart reader that recovers each chart's cached data, the drawing-text
  reader that recovers each shape's annotation text, and `ExcelExtractionException`, the package's one
  workbook-specific exception type. The subsystem is fully managed and ships no native asset.

### Why there is no rendering backend

Not rendering Excel is a positive design decision, not missing work. A workbook has no page grid;
rasterizing one would impose an arbitrary layout the data does not have, clip long prose at artificial
boundaries, and substitute a picture of a number for the number itself. `DocDown.Excel` therefore ships
no renderer. The extractor reports `PageRenderingApplicable` as `false`, records an environment fact
stating that page rendering does not apply to a non-paginated workbook, and otherwise stays silent when
a caller requests rendered pages.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap probe; does not open the workbook |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Cheap enumeration; work runs in the case delegate |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddExcel` is the registration surface |
| Source document | Inbound | `.xlsx` byte stream | A stream source is not guaranteed seekable |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by `ExcelOpenXmlExtractor` (Id `excel-openxml`, Priority 10).
  `ProbeAvailability` completes well under 50 ms, performs no I/O, does not open the source document,
  and returns `ExtractorAvailability.Available()`. The extractor also reports
  `PageRenderingApplicable = false`.
- **`ISelfValidating`** enumeration is cheap. The backend contributes a parse round-trip case and a
  page-rendering case that reports a skip with a reason because a workbook is non-paginated.
- **`IExtractionSink`** is the only output channel. Every part, content count, note, environment fact,
  and metadata record goes through it, and no filesystem path is constructed in this package.
- **`DocDownBuilder`** is extended by one method, `AddExcel`; there is no alternate registration path.
- **The source document** is opened through `DocumentSource.OpenRead` and is not guaranteed seekable for
  a stream source, so the backend buffers the workbook into memory before opening the package because
  the package reader must seek.

## Dependencies

- **DocDown.Core** — the extraction contract, sink, options, output layout, and structured unreadable
  result. See the *DocDown.Core System Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the package is built on. See the
  *DocumentFormat.OpenXml* OTS design.
- **System.IO.Packaging** (OTS, transitive) — the OPC container reader underneath the SDK that opens
  the `.xlsx` package and resolves its parts and relationships. See *System.IO.Packaging* under the
  OTS integration design.

There are no other runtime dependencies, and there is no dependency of any kind on a native library.
The package's build output contains no `runtimes/` folder and no native `.dll`, `.so`, or `.dylib`.

## Risk Control Measures

- **SDK containment.** `DocumentFormat.OpenXml` types appear only in the `OpenXml` subsystem; no SDK
  type appears in a public signature. A host referencing `ExcelDocDownBuilderExtensions` never pulls
  SDK types into its own compilation.
- **One reader, one emitter.** The reader populates an `ExcelWorkbookModel` and hands it to
  `ExcelContentEmitter`, so every mapping decision is made once, in a place testable from a hand-built
  model with no workbook behind it.
- **Verbatim preservation.** Cell values are never truncated and never reformatted: a page of pasted
  prose survives at full length and a stored number reaches the output at full precision. The grid
  table's elision is additive and table-only — the full value is always present in the listing — so the
  verbatim guarantee cannot be weakened by a rendering choice.
- **Inventory and note discipline.** The emitter reports what it found through content inventory counts,
  including zeros for features it explicitly looked for. It records a short note only when DocDown
  attempted a chart or image step and could not complete it, such as an unreadable chart part or images
  skipped by caller-supplied size limits. A chart with no cached
  data, a bounded chart table, an empty worksheet, and a vector image written in its source encoding are
  stated in content instead of being characterized as incomplete work.
- **Failure containment.** The reader throws `ExcelExtractionException` for the workbook faults it can
  name better than the SDK — a package that cannot be opened and a missing workbook part. Other faults
  propagate to Core, which converts them into a structured `Unreadable` result with the output layout
  still written.

## Data Flow

1. The engine selects this backend for a document detected as `.xlsx` and calls `ExtractAsync` with a
   context exposing the options, the sink, and a cancellation token.
2. The extractor records two environment facts through the sink: `excel.backend` names the managed Open
   XML SDK, and `excel.pageRendering` states that page rendering does not apply to a non-paginated
   workbook.
3. `ExcelOpenXmlExtractor` buffers the source, opens the package read-only, and hands the stream to
   `ExcelOpenXmlReader.Read`, which produces an `ExcelWorkbookModel` in workbook order: worksheets,
   cells, merged ranges, images, charts, shape text, and workbook metadata.
4. `ExcelContentEmitter.EmitAsync` writes embedded images first so worksheets can link them inline,
   writes one `Sheet` part per worksheet and one `Chart` part per chart, reports document info and
   metadata, reports content inventory counts from the model, and records short notes only for
   chart-read or image-write attempts it could not complete.
5. The extractor returns `ExtractionOutcome.Produced` when workbook reading and emission complete. If
   the source cannot be opened as a workbook or carries no workbook part, the exception propagates to
   Core, which writes the structured `Unreadable` result and finalizes the layout.

### Mapping decisions

The mapping decisions this system makes are worth stating together, because they are what make an Excel
extraction a faithful data container rather than a flattened dump:

- **Verbatim listing.** Every worksheet becomes a `ContentPartKind.Sheet` part whose body is an
  address/value/formula listing: each cell's A1 address, its value verbatim at full length, and its
  formula when present. This listing is the lossless ledger and is always produced.
- **Additive grid table.** When a worksheet's populated region is dense and table-shaped, a grid table
  is emitted in addition to the listing to restore row and column relationships. It never replaces the
  listing. A value too long or containing a line break is shown as `…` in the table, and the nearby
  note states that the full value remains in the listing below.
- **Charts as data.** Each chart a worksheet shows becomes its own `ContentPartKind.Chart` part,
  written straight after the sheet, carrying a table of categories against series values recovered from
  the chart cache. A chart that could not be read keeps its own part stating the reason and also causes
  a short note naming the failed step. A chart with no cached data or one bounded to the plotted-point
  limit states that fact in its chart part.
- **Drawing annotations.** Text drawn over a worksheet in callouts and labels — text that lives in no
  cell — is written under its own heading after the cells, so a reader of the listing alone still sees
  it.
- **Images.** Embedded images are written through the sink and linked inline under the worksheet that
  shows them, recording which worksheet each belongs to. When caller-supplied size limits prevent a
  write, the emitter records a short note stating what happened. Images are written in whatever format
  the document stored them in, so a vector image written successfully is emitted silently in its source
  encoding.
- **Page rendering.** A page-rendering request is answered with silence and an environment fact naming
  the non-applicability, because a workbook has no page grid to render.

## Design Constraints

- **The package is 100% managed, runtime-identifier agnostic, and ships no native asset.** This is what
  makes it deployable anywhere the .NET runtime is.
- **The whole workbook is buffered into memory, and the SDK builds an in-memory object model.** A very
  large workbook is therefore bounded by available memory. The chart data table is bounded at a fixed
  number of plotted points so one logged sweep cannot crowd out the rest of the workbook, and the chart
  part states when the remainder was omitted.
- **`DocumentFormat.OpenXml` is pinned to an exact restore version.** A floating reference would let a
  restore substitute a different patch, changing the resolved dependency set recorded in the generated
  SBOM and breaking build reproducibility. See *DocumentFormat.OpenXml* under the OTS integration
  design.
- **`ProbeAvailability` is unconditional.** Nothing about this backend is environment-dependent — there
  is no native asset, no installed application, and no registration to check — so the probe is
  satisfied by construction and reports `Available()` everywhere the package loads.
- **The legacy binary formats are detected but never extracted.** Core recognizes `.xls` deliberately,
  because "this is an `.xls`, and DocDown does not support legacy binary formats" is a far better answer
  than "unrecognized format".
