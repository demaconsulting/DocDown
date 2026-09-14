# DocDown.Word System Design

![DocDown.Word Structure](DocDownWordView.svg)

`DocDown.Word` is the Word extraction system for the DocDown output contract. It reads a Word
document's text, real tables, embedded images, reviewer comments, footnotes, document-control
content, and document metadata, then writes them through the `IExtractionSink` the engine supplies.
It is a separately distributed NuGet package that a host registers explicitly alongside
`DocDown.Core`, and it ships one backend: a managed Open XML SDK backend for `.docx`. Legacy binary
`.doc` documents are not supported and are reported as unreadable rather than routed to a backend
that cannot read them.

## Architecture

The system has two subsystems and one direct unit:

- **WordDocDownBuilderExtensions** *(direct unit)* is the registration seam. `AddWord` registers
  the managed backend through one explicit host call, with no reflection and no Open XML SDK type
  leaking onto the host-facing surface.
- **Markdown** *(subsystem)* is the reader-neutral core. It defines the block model the reader
  populates, the writer that turns that model into markdown, the table writer that renders a Word
  grid as a GitHub-flavored-markdown table, and the emitter that writes content, inventory,
  metadata, and short extraction notes through the sink.
- **OpenXml** *(subsystem)* is the backend. It contains the extractor the engine selects, the
  reader that turns a `WordprocessingDocument` DOM into the shared model, the image reader that
  resolves each embedded image part, and `WordExtractionException`, the one exception type this
  package raises intentionally.

`DocDown.Word` deliberately has no second backend. The package is fully managed, deployable
anywhere the .NET runtime is, and keeps its extraction behavior in one reader and one emitter
instead of splitting it across environment-specific implementations.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound | .NET | Probe returns `Available()` |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; delegates perform the work |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddWord` is the registration surface |
| Source document | Inbound | `.docx` byte stream | Not guaranteed seekable |
| `WordExtractionException` | Outbound | .NET | Raised only for clearly named unreadable cases |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by `WordOpenXmlExtractor` (Id `word-openxml`,
  Priority `10`). `ProbeAvailability()` performs no I/O, does not open the document, and returns
  `ExtractorAvailability.Available()` because nothing about this backend depends on a machine-local
  prerequisite. Page rendering is outside the backend's scope and is stated through environment
  facts and self-test output rather than through a separate declaration mechanism.
- **`ISelfValidating`** contributes two cheap-to-enumerate cases: a parse round trip that builds
  and reads a tiny `.docx` in memory, and a page-rendering case that reports a skip because this
  package does not attempt page rendering.
- **`IExtractionSink`** is the only output channel. Content, images, inventory features, notes,
  metadata, and environment facts all go through it, and this package never writes directly to the
  filesystem.
- **`DocDownBuilder`** is extended by one method, `AddWord`; there is no alternate registration
  path and no environment-sensitive registration behavior.
- **The source document** is opened through `DocumentSource.OpenRead()` and is not guaranteed
  seekable for a stream source, so the backend buffers the whole document into memory before
  opening the package.
- **`WordExtractionException`** is reserved for conditions the backend can explain more plainly
  itself, such as the OLE compound-file signature that fronts a password-protected `.docx`. Core
  converts any uncaught exception into an `Unreadable` result.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, the content inventory, the
  extraction-note shape, and the output layout. See the *DocDown.Core System Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the package is built on, pinned to
  `[3.5.1]` for restore determinism and SBOM reproducibility. See the
  *DocumentFormat.OpenXml* OTS design.
- **System.IO.Packaging** (OTS, transitive) — the OPC container reader underneath the SDK. See
  *System.IO.Packaging* under the OTS integration design.

There are no native runtime dependencies and no dependency on a locally installed Office
application.

## Risk Control Measures

- **One reader, one emitter.** `WordOpenXmlReader` produces a `WordDocumentModel`, and
  `WordContentEmitter` is the one downstream path that writes it. Reading and reporting do not have
  parallel implementations that can drift apart.
- **SDK containment.** Open XML SDK types stay inside the `OpenXml` subsystem; no SDK type appears
  on the registration surface.
- **Truthful image provenance.** Every image reference carries its bytes, media type, preferred
  name, optional description, and source part URI together. The emitter always reports
  `ImageTransform.Passthrough`, because the stored part bytes are the extracted image bytes.
- **Report-only completeness.** The package describes what it produced in the content inventory and
  uses a short note only when it attempted a step it could not complete. Empty content is therefore
  conveyed by a zero inventory count, while chart extraction and table flattening are conveyed by
  notes.
- **Unreadable isolation.** The reader detects encrypted containers up front and raises a plain
  `WordExtractionException`. Any other adverse document state propagates to Core, which converts it
  into an `Unreadable` result without leaving a partial layout behind.

## Data Flow

1. The engine selects `word-openxml` for a document detected as `.docx` and calls `ExtractAsync()`
   with a context exposing the options, sink, and cancellation token.
2. `WordOpenXmlExtractor` records two environment facts through the sink:
   `word.backend = Open XML SDK (managed)` and
   `word.pageRendering = not provided by this extractor`.
3. The extractor buffers the source, checks for the OLE signature that identifies an encrypted
   `.docx`, opens the package read-only, and hands the stream to `WordOpenXmlReader.Read()`.
4. `WordOpenXmlReader` walks the document and produces a `WordDocumentModel` containing the body
   flow, deduplicated document-control content, comments, footnotes, image references, metadata,
   and the counts the emitter needs for inventory and notes.
5. `WordContentEmitter.EmitAsync()` writes images first, writes a single `content.md`, reports
   `DocumentInfo`, reports the content inventory, reports
   document metadata when captured, and emits short notes for incomplete steps the model implies.
6. Normal completion returns `ExtractionOutcome.Produced`. If reading or emission throws, Core
   records the extraction as `ExtractionOutcome.Unreadable` and writes the structured failure
   output.

### Markdown mapping decisions

The mapping decisions this system makes are worth stating together, because they are the reason a
Word extraction reads materially better than the same document routed through PDF:

- **Real tables.** A `w:tbl` becomes a GFM table with a header row, padded short rows, escaped cell
  content, and `<br>` line breaks inside a cell. Horizontal merges, vertical merges, and nested
  tables are flattened as faithfully as markdown allows, and the total flattened count is carried
  forward so the emitter can state that incomplete preservation in one short note.
- **`## Document Control`.** Headers and footers from every `w:sectPr` are rendered through the
  same writer as the body, deduplicated across sections, and emitted once under `## Document
  Control`. Page-numbering furniture is identified structurally from field instructions and omitted
  from the section instead of being repeated as body-like text.
- **Image passthrough.** Every image is written in its source encoding with
  `ImageTransform.Passthrough`. Pixel dimensions remain unstated because `wp:extent` is a display
  size in EMUs, not a pixel count.
- **Content inventory.** The system reports counts for text blocks, headings, tables, list items,
  inline images, comments, distinct comment authors, and footnotes. Categories a reader genuinely
  looked for, such as text blocks, comments, distinct comment authors, and footnotes, are marked
  looked-for so a zero count remains explicit.
- **Extraction notes.** The system currently emits notes for two incomplete steps only: embedded
  charts whose chart parts are not read, and merged or nested table structure that had to be
  flattened for markdown. An empty document is never a note; it is conveyed by a zero-count
  inventory feature.
- **Content layout.** A Word document is one continuous flow, so it is written as a single
  `content.md` with the leading matter and the `## Document Control` section in place.

## Design Constraints

- **The package is fully managed, runtime-identifier agnostic, and ships no renderer.** It reads
  structure and content from Open XML and does not attempt page rasterization.
- **The whole source document is buffered into memory, and the SDK builds an in-memory DOM.** Very
  large documents are therefore bounded by available memory; the package does not implement a
  streaming reader.
- **Trimming and AOT publishing stay off.** The tool project records that the overall stack has not
  been verified for trim or AOT publishing.
- **`DocumentFormat.OpenXml` is pinned to `[3.5.1]`.** The pin exists for supply-chain and SBOM
  reproducibility rather than API fragility.
- **The legacy binary formats are detected but never extracted.** A `.doc` is reported as
  unreadable with a plain explanation that DocDown does not support the legacy binary Office
  formats.
