# DocDown.Word System Design

![DocDown.Word Structure](DocDownWordView.svg)

`DocDown.Word` is the Word extraction system for the DocDown output contract. It reads a Word
document's text, real tables, embedded images, and document metadata, and writes them through the
`IExtractionSink` the engine supplies. It is a separately distributed NuGet package that a host
registers explicitly alongside `DocDown.Core`, and it ships one backend: a managed Open XML SDK
backend that serves every `.docx`. Legacy binary `.doc` documents are not supported — by this
package or by any other — and a request to extract one is refused plainly rather than routed
somewhere that cannot deliver.

## Architecture

The system has two subsystems and one direct unit:

- **WordDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddWord` registers
  the managed backend. Reflection-free and free of any Open XML SDK type, so a host can reference
  the surface without the SDK's types entering its own compilation.
- **Markdown** *(subsystem)* is the reader-neutral core: the block model the reader populates,
  the writer that turns that model into markdown, the table writer that turns a `w:tbl` model into
  a GitHub-flavored-markdown table, and the emitter that walks a model through the sink and reports
  every gap and diagnostic the model implies. Every mapping decision that differentiates Word from
  PDF — real tables, `## Document Control`, image passthrough, split-mode selection — lives here
  and is testable from a hand-built model with no document behind it.
- **OpenXml** *(subsystem)* is the backend: the extractor the engine selects, the reader
  that turns a `WordprocessingDocument` DOM into the shared model, the image reader that
  yields the bytes and provenance of each embedded image part, and `WordExtractionException`, the
  one exception type this package translates itself. Fully managed, no native asset.

### The rendering-via-PDF boundary and where a Word rendering package would live

> Exporting a document to PDF **purely to rasterize page images** is legitimate: a page image is
> inherently a visual artifact, and a faithful raster of a page loses nothing that a page image was
> ever going to carry. What DocDown rejects is converting to PDF and then **extracting text and
> structure from the PDF**, because that path destroys precisely the structure DocDown exists to
> harvest — cell values, formulas, speaker notes, heading levels and table semantics — and replaces
> it with positioned glyphs. Rendering-via-PDF is not extraction-via-PDF. In every DocDown package,
> text and structure come from the document's own model; a PDF, where one is produced at all, is a
> rasterization intermediate and is never read back for content.

A Word page-rendering package is not part of this package and is not planned. `DocDown.Word`
therefore does not declare the `RenderedPages` capability; a request for rendered pages is reported
as a counted `GapKind.Pages`/`Unavailable` gap that names where the capability would live, and
states that no such package exists, rather than instructing the reader to obtain one.

### Why there is no second backend

An earlier design carried a second, COM automation backend that drove Microsoft Word through
`IDispatch`. It was removed, because both of its justifications were false:

- **It could not render pages.** Word exposes no page-to-bitmap API. `ExportAsFixedFormat` yields a
  PDF, which still needs a rasterizer this package does not ship, so "true-fidelity page rendering"
  was never on offer.
- **It was the only route to the legacy binary formats,** which are no longer supported at all.

What remained was a backend that never won a `.docx` (it lost every tie to Open XML on priority),
could not extract embedded image bytes at all (the Word object model exposes a shape's dimensions
but not its content), and was inferior to Open XML on every content axis — while costing a COM
surface that continuous integration cannot exercise and a committed, hand-generated evidence file
to attest it. Word extraction is Open XML only.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddWord` is the surface |
| Source document | Inbound | `.docx` byte stream | Not guaranteed seekable |
| `WordExtractionException` | Outbound | .NET exception type | Carried out by Core as a structured failure message |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by `WordOpenXmlExtractor` (Id `word-openxml`, Priority 10).
  `ProbeAvailability` must be well under 50 ms, side-effect free, must not open the document, and
  must not throw. Nothing about this backend is environment-dependent, so its probe is unconditional.
- **`ISelfValidating`** enumeration is cheap; work happens only when a case's delegate is invoked.
  The backend contributes a parse round-trip case and a reasoned-skip page-rendering case.
- **`IExtractionSink`** is the only output channel. Every byte and every report goes through it, and
  no filesystem path is ever constructed in this package.
- **`DocDownBuilder`** is extended by one method, `AddWord`; there is no other
  way to register this package's backend.
- **The source document** is opened through `DocumentSource.OpenRead` and is not guaranteed seekable
  for a stream source, so the backend buffers the whole document into memory before opening
  the package.
- **`WordExtractionException`** is the only exception this package translates itself: the OLE
  compound-file signature that fronts a password-protected `.docx` produces a clear message, which
  Core carries into an `ExtractorFailed` failure.

No Open XML SDK type appears on any of these. That containment is what keeps `WordDocDownBuilderExtensions`
usable by a host that has not itself referenced the SDK.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout. See the
  *DocDown.Core System Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the package is built on, pinned to
  the exact version range `[3.5.1]` for restore determinism and SBOM reproducibility. See the
  *DocumentFormat.OpenXml* OTS design for what the pin actually protects and why the SDK plus its
  `Framework` companion count as one OTS item.
- **System.IO.Packaging** (OTS, transitive) — the OPC (Zip) container reader underneath the SDK.
  See *System.IO.Packaging* under the OTS integration design.

There are no other runtime dependencies, and no dependency of any kind on a native library. The
package's build output contains no `runtimes/` folder and no native `.dll`, `.so`, or `.dylib`.

## Risk Control Measures

- **SDK containment.** `DocumentFormat.OpenXml` types appear only in the `OpenXml` subsystem; no SDK
  type appears in a public signature.
  A host referencing `WordDocDownBuilderExtensions` never pulls SDK types into its compilation.
- **One reader, one renderer.** The reader populates a `WordDocumentModel` and hands it to
  `WordContentEmitter`, so every mapping decision is made once, in a place testable from a
  hand-built model with no document behind it. Reading and rendering cannot drift apart because
  there is exactly one of each.
- **Provenance segregation.** Every image reference carries its complete stored bytes, its media
  type, its preferred name, and its part URI together; the emitter never chooses bytes and their
  provenance label independently and cannot make them disagree. The image transform is always
  `Passthrough` because an Open XML image part stores a complete image file byte-for-byte.
- **Failure containment.** The OpenXml reader detects the OLE compound-file signature up front and
  raises a plain `WordExtractionException` rather than letting the SDK produce a raw package error.
  All other adverse conditions propagate to Core, which converts them into a coded, structured
  `ExtractorFailed` failure that still writes the full output layout — so an encrypted or
  malformed document in a batch run cannot abort the run or produce a partial layout.
- **Count-before-decide accounting.** Every image is counted as written before any gap decision is
  taken about it, so the ledger's denominator cannot be reduced by the same code path that failed
  to deliver an image. Merged, vertically merged, and nested table cells are counted before being
  flattened, so `WORD0005` states the loss with an exact number.

## Data Flow

1. The engine selects this backend for a document detected as `.docx` and calls `ExtractAsync` with
   a context exposing the options, the sink, and a cancellation token — but no filesystem path.
2. The extractor records two environment facts through the sink: `word.backend` names
   what parsed this document (the Open XML SDK), and `word.pageRendering` states
   plainly that page rendering is not provided by this extractor.
3. `WordOpenXmlExtractor` buffers the source, checks the OLE signature (raising
   `WordExtractionException` on a hit), opens the package read-only, and hands the stream to
   `WordOpenXmlReader.Read` which produces a `WordDocumentModel`.
4. `WordContentEmitter.EmitAsync` writes every image first (so the content renderer places the
   sink-allocated links), writes the content — one `content.md` for `Auto`/`Single`, split parts for
   `PerPart` — reports the document info, and reports every diagnostic and gap the model implies:
   `WORD0003`–`WORD0009` for the mapping decisions, `WORD0001` for a document with no text.
5. The extractor returns `Degraded` when any gap was reported and `Succeeded` otherwise. Core adds
   its own derived gaps (a `RenderedPages` request meets a Core-owned unavailability gap alongside
   the backend-specific one), finalizes the content, reconciles the ledger, and writes
   `summary.txt` and `manifest.json`.

### Markdown mapping decisions

The mapping decisions this system makes are worth stating together, because they are the reason a
Word extraction reads materially better than the same document routed through a PDF:

- **Real tables.** A `w:tbl` becomes a GFM table by the seven rules in *WordTableWriter* and
  *WordOpenXmlReader.BuildTable*: (1) row one is the header and, when `w:tblHeader` is absent,
  `WORD0004 TableHeaderAssumed` states the assumption; (2) column count is the widest row and short
  rows are padded so the grid stays rectangular; (3) a newline inside a cell becomes `<br>` and a
  literal pipe is escaped; (4) `w:gridSpan` puts the text in the first column and empties the
  spanned columns, counting each; (5) `w:vMerge` continuation cells are emptied and counted; (6)
  nested tables are flattened to `<br>`-joined rows and counted; (7) an empty table is skipped and
  `WORD0003 EmptyTableSkipped` records the omission. Rules 4–6 accumulate into one counted
  `GapKind.Structure`/`GapScope.PartiallyExtracted` gap plus `WORD0005 MergedCellsFlattened`.
- **`## Document Control`.** Headers and footers from every `w:sectPr` are rendered through the
  same writer as the body, deduplicated across sections, and emitted as labeled subsections after
  the title heading and before the body. Page-numbering furniture is classified **structurally by
  field instruction** — `PAGE`, `NUMPAGES`, `SECTIONPAGES`, `SECTIONPAGESNUM`, matched on the
  instruction's first token so `PAGEREF` is never misclassified — and stripped, never by matching
  rendered text. That distinction matters: rendered page-number text is locale- and format-
  dependent, so a regex over rendered text would misfire on a document whose numbers happen to look
  like nothing else, and it would fire on a revision string that happens to contain digits. The
  field instruction is the authoritative structural signal. A part reduced to nothing by furniture
  stripping, and an empty part, are each **omitted and recorded as a `WORD0009` informational
  diagnostic** — never a gap and never a degrade, because omitting furniture or an empty part loses
  no document content; a section made entirely of furniture is omitted whole. Gaps and DEGRADED are
  reserved for genuine loss (a flattened table, an undecodable image), so a complete extraction is
  never falsely reported incomplete because a header was empty.
- **Image provenance.** Every image is `ImageTransform.Passthrough` because an Open XML image part
  already stores a complete image file. `WidthPx` and `HeightPx` stay `null` because `wp:extent` is
  an EMU display size, not a pixel count; reporting EMU as pixels would be a false provenance
  claim. Core's SHA-256 dedup means one logo referenced many times is written once. EMF and WMF
  metafiles are written as-is with `WORD0006 VectorImageWrittenAsIs`; an `ImageOutputMode.ForcePng`
  request is not honored (this package ships no imaging stack) and reported with `WORD0007
  ForcePngNotHonored`, source bytes and matching extension.
- **Split modes.** `Auto` and `Single` both produce a single-flow `content.md`; `Auto` deliberately
  chose single-flow for Word because a Word document is one continuous flow. `PerPart` splits at
  every `Heading 1`, and the leading matter together with the `## Document Control` section becomes
  part one.

## Design Constraints

- **The package is 100% managed, runtime-identifier agnostic, and ships no native asset.** This is
  what makes it deployable anywhere the .NET runtime is, and it is also precisely why the
  `RenderedPages` capability is not declared: rasterizing a page needs a renderer, and shipping one
  would mean shipping native assets.
- **The whole source document is buffered into memory, and the SDK builds an in-memory DOM.** A
  very large document is therefore bounded by available memory. This is a documented characteristic
  of the design, not an enforced limit — the reader neither reads in a streaming fashion nor
  imposes a size cap. Callers who need to process documents at scale should size their host
  accordingly; the memory usage is proportional to the document size and to the DOM the SDK
  produces from it.
- **Trimming and AOT publishing must stay off.** `DemaConsulting.DocDown.Tool/DemaConsulting.DocDown.Tool.csproj`
  records the fact that `PublishTrimmed` and AOT stay off because the tool's overall stack has
  unverified trim/AOT compatibility.
- **`DocumentFormat.OpenXml` is pinned to the exact version range `[3.5.1]`.** The comment in
  `src/DemaConsulting.DocDown.Word/DemaConsulting.DocDown.Word.csproj` states plainly why: a
  floating reference would let a restore substitute a different patch, which would change the
  resolved dependency set recorded in the generated SBOM and break build reproducibility. This is
  **not** an API-fragility pin (the SDK follows semantic versioning) — it is a supply-chain
  reproducibility pin. See *DocumentFormat.OpenXml* under the OTS integration design.
- **`ProbeAvailability` is unconditional.** Nothing about this backend is environment-dependent —
  there is no native asset, no installed application, and no registration to check — so the probe
  is satisfied by construction and reports available everywhere the package loads.
- **The legacy binary formats are detected but never extracted.** Core recognizes `.doc`, `.xls`,
  `.ppt`, and `.vsd` deliberately, because "this is a `.doc`, and DocDown does not support legacy
  binary formats" is a far better answer than "unrecognized format". No DocDown package extracts
  one, so the refusal names the format and states that fact declaratively rather than pointing at
  a package or an environment that could deliver the capability.
