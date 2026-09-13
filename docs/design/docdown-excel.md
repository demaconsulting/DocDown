# DocDown.Excel System Design

![DocDown.Excel Structure](DocDownExcelView.svg)

`DocDown.Excel` is the Excel extraction system for the DocDown output contract. It reads a workbook's
worksheet cell values, the formulas behind computed cells, the charts each worksheet shows, the
annotations drawn over it, and its embedded images and metadata, and writes them through the
`IExtractionSink` the engine supplies. It is a separately distributed NuGet package that a host
registers explicitly alongside `DocDown.Core`, and it ships one backend: a managed Open XML SDK
backend that serves every `.xlsx`. Legacy binary `.xls` workbooks are not supported — by this package
or by any other — and a request to extract one is refused plainly rather than routed somewhere that
cannot deliver.

A workbook, in this product owner's domain, is a prose and data container, not a picture. Its cells
hold requirement quotations, pasted transcripts, exact numbers, and the formulas that produced them.
The whole design follows from that: cell values are preserved verbatim at full precision and full
length, formulas are kept alongside their computed values, every fact keeps its sheet identity and
cell address, and the workbook is deliberately never rendered.

## Architecture

The system has two subsystems and one direct unit:

- **ExcelDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddExcel` registers
  the managed backend. Reflection-free and free of any Open XML SDK type, so a host can reference the
  surface without the SDK's types entering its own compilation.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that walks the workbook
  model through the sink — one content part per worksheet, one per chart — and the chart writer that
  renders a chart's cached series as a table. Every mapping decision that differentiates an Excel
  extraction — the verbatim cell listing, the additive grid table with its elision note, the chart
  data table, the drawing-shape annotations, the inline image links — lives here and is testable from
  a hand-built model with no workbook behind it.
- **OpenXml** *(subsystem)* is the backend: the extractor the engine selects, the reader that turns a
  `SpreadsheetDocument` into the shared model, the image reader that yields each embedded image's
  bytes and sheet association, the chart reader that recovers each chart's cached data, the
  drawing-text reader that recovers each shape's annotation text, and `ExcelExtractionException`, the
  one exception type this package translates itself. Fully managed, no native asset.

### Why there is no rendering backend

Not rendering Excel is a positive design decision, not a missing feature. A workbook has no page grid;
rasterizing one would impose an arbitrary layout the data does not have, clip long prose at page
boundaries, and substitute a picture of a number for the number itself — losing exactly the
information the workbook exists to carry. `DocDown.Excel` therefore ships no rendering backend and
declares no `RenderedPages` capability, and — unlike `DocDown.Word`, whose page-rendering shortfall is
a counted `Unavailable` gap — a page request against a workbook is treated as **not applicable** rather
than unavailable. The extractor sets `PageRenderingApplicable` to false, records an environment fact
that names the reason, and the run does not degrade. The caller is told plainly, through that
environment fact and the engine's own record of the non-applicability, without a false shortfall a
reader could mistake for a capability failure.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddExcel` is the surface |
| Source document | Inbound | `.xlsx` byte stream | Not guaranteed seekable |
| `ExcelExtractionException` | Outbound | .NET exception type | Carried out by Core as a structured failure message |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by `ExcelOpenXmlExtractor` (Id `excel-openxml`, Priority 10).
  `ProbeAvailability` must be well under 50 ms, side-effect free, must not open the document, and must
  not throw. Nothing about this backend is environment-dependent, so its probe is unconditional. The
  extractor additionally reports `PageRenderingApplicable` as false.
- **`ISelfValidating`** enumeration is cheap; work happens only when a case's delegate is invoked. The
  backend contributes a parse round-trip case and a reasoned-skip page-rendering case.
- **`IExtractionSink`** is the only output channel. Every byte and every report goes through it, and no
  filesystem path is ever constructed in this package.
- **`DocDownBuilder`** is extended by one method, `AddExcel`; there is no other way to register this
  package's backend.
- **The source document** is opened through `DocumentSource.OpenRead` and is not guaranteed seekable
  for a stream source, so the backend buffers the whole workbook into memory before opening the
  package, because the package reader must seek.
- **`ExcelExtractionException`** is the only exception this package translates itself: a package the
  Open XML SDK cannot open (encrypted, malformed) or one with no workbook part produces a clear
  message, which Core carries into an `ExtractorFailed` failure with the full layout still written.

No Open XML SDK type appears on any of these. That containment is what keeps
`ExcelDocDownBuilderExtensions` usable by a host that has not itself referenced the SDK.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout. See the
  *DocDown.Core System Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the package is built on, pinned to the
  exact version range for restore determinism and SBOM reproducibility. See the *DocumentFormat.OpenXml*
  OTS design; its evidence reaches this package transitively through the Excel extraction tests.
- **System.IO.Packaging** (OTS, transitive) — the OPC (Zip) container reader underneath the SDK, which
  opens the `.xlsx` package and resolves its parts and relationships. See *System.IO.Packaging* under
  the OTS integration design.

There are no other runtime dependencies, and no dependency of any kind on a native library. The
package's build output contains no `runtimes/` folder and no native `.dll`, `.so`, or `.dylib`.

## Risk Control Measures

- **SDK containment.** `DocumentFormat.OpenXml` types appear only in the `OpenXml` subsystem; no SDK
  type appears in a public signature. A host referencing `ExcelDocDownBuilderExtensions` never pulls
  SDK types into its compilation.
- **One reader, one emitter.** The reader populates an `ExcelWorkbookModel` and hands it to
  `ExcelContentEmitter`, so every mapping decision is made once, in a place testable from a hand-built
  model with no workbook behind it. Reading and rendering cannot drift apart because there is exactly
  one of each.
- **Verbatim preservation.** Cell values are never truncated and never reformatted: a page of pasted
  prose survives at full length and a stored number reaches the output at full precision. The grid
  table's elision is additive and table-only — the full value is always present in the listing — so the
  verbatim guarantee is structurally impossible to weaken by a rendering choice.
- **Cache-only chart data.** The chart reader recovers a chart's cached values as last plotted and
  keeps the source reference alongside them; it never presents a formula reference as if it were data.
  A chart part it cannot parse is returned as a reason rather than thrown, so an unreadable chart is
  reported as a gap rather than aborting the extraction.
- **Failure containment.** The reader wraps a package it cannot open or a missing workbook part in a plain
  `ExcelExtractionException`; every other adverse condition propagates to Core, which converts it into a
  coded, structured `ExtractorFailed` failure that still writes the full output layout — so an encrypted
  or malformed workbook in a batch run cannot abort the run or produce a partial layout.
- **Count-before-decide accounting.** Every worksheet, chart, and image is counted before any gap
  decision is taken, so the ledger's denominator cannot be reduced by the same code path that failed to
  deliver content. A bounded chart states how many points it dropped; an uncached or unreadable chart is
  named by its part URI.

## Data Flow

1. The engine selects this backend for a document detected as `.xlsx` and calls `ExtractAsync` with a
   context exposing the options, the sink, and a cancellation token — but no filesystem path.
2. The extractor records two environment facts through the sink: `excel.backend` names what parsed this
   workbook (the Open XML SDK), and `excel.pageRendering` states that page rendering is not applicable
   to a non-paginated workbook.
3. `ExcelOpenXmlExtractor` buffers the source, opens the package read-only, and hands the stream to
   `ExcelOpenXmlReader.Read`, which produces an `ExcelWorkbookModel` — worksheets in workbook order,
   each with its cells, merged ranges, images, charts, and shape texts, plus the workbook metadata.
4. `ExcelContentEmitter.EmitAsync` writes every image first (so the worksheet renderer places the
   sink-allocated links), writes one `Sheet` part per worksheet and one `Chart` part per chart, reports
   the document info and metadata, and reports every diagnostic and gap the model implies:
   `XLSX0001`–`XLSX0006` for the empty-workbook, empty-sheet, vector-image, and chart shortfalls.
5. The extractor returns `Degraded` when any gap was reported and `Succeeded` otherwise. A page-rendering
   request does not degrade the run, because rendering is not applicable to a workbook. Core finalizes
   the content, reconciles the ledger, and writes `summary.txt`, `manifest.json`, and `metadata.json`.

### Mapping decisions

The mapping decisions this system makes are worth stating together, because they are what makes an
Excel extraction a faithful data container rather than a flattened dump:

- **Verbatim listing.** Every worksheet becomes a `ContentPartKind.Sheet` part whose body is an
  address/value/formula listing: each cell's A1 address, its value verbatim at full length, and its
  formula when present. This listing is the lossless ledger and is always produced.
- **Additive grid table.** When a worksheet's populated region is dense and table-shaped — enough
  rows and columns, not too wide, and dense enough — a grid table is emitted *in addition* to the
  listing to restore the row and column relationships. It never replaces the listing and never carries
  the verbatim guarantee: a value too long or containing a line break is elided in the table with the
  marker the accompanying note explains, and merged ranges are stated as a note because a GFM table
  cannot span cells.
- **Charts as data.** Each chart a worksheet shows becomes its own `ContentPartKind.Chart` part,
  written straight after the sheet, carrying a table of categories against series values recovered from
  the chart's cache. A chart that could not be read, one that cached no data, and one bounded by the
  plotted-point limit are each stated in the part and reported as a counted gap.
- **Drawing annotations.** Text drawn over a worksheet in callouts and labels — text that lives in no
  cell — is written under its own heading after the cells, so a reader of the listing alone still sees
  it.
- **Images.** Embedded images are written through the sink and linked inline under the worksheet that
  shows them, recording which worksheet each belongs to. EMF and WMF metafiles are written as-is with an
  informational readability caveat.
- **Page rendering.** A page-rendering request is answered with silence and an environment fact naming
  the non-applicability, not a gap and not a degrade, because a workbook has no page grid to render.

## Design Constraints

- **The package is 100% managed, runtime-identifier agnostic, and ships no native asset.** This is what
  makes it deployable anywhere the .NET runtime is, and it is consistent with the decision not to
  declare the `RenderedPages` capability, which would require a renderer and native assets Excel
  deliberately does not provide.
- **The whole workbook is buffered into memory, and the SDK builds an in-memory object model.** A very
  large workbook is therefore bounded by available memory. This is a documented characteristic of the
  design, not an enforced limit — the reader neither streams nor imposes a size cap. The chart data
  table is bounded at a fixed number of plotted points so one logged sweep cannot crowd out the rest of
  the workbook, and the bound states what it dropped.
- **`DocumentFormat.OpenXml` is pinned to an exact version range.** A floating reference would let a
  restore substitute a different patch, changing the resolved dependency set recorded in the generated
  SBOM and breaking build reproducibility. This is a supply-chain reproducibility pin, not an
  API-fragility pin. See *DocumentFormat.OpenXml* under the OTS integration design.
- **`ProbeAvailability` is unconditional.** Nothing about this backend is environment-dependent — there
  is no native asset, no installed application, and no registration to check — so the probe is satisfied
  by construction and reports available everywhere the package loads.
- **The legacy binary formats are detected but never extracted.** Core recognizes `.xls` deliberately,
  because "this is an `.xls`, and DocDown does not support legacy binary formats" is a far better answer
  than "unrecognized format". No DocDown package extracts one, so the refusal names the format and states
  that fact declaratively rather than pointing at a package or an environment that could deliver the
  capability.
