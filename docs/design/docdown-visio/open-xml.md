## OpenXml Subsystem

![DocDown.Visio Structure](DocDownVisioView.svg)

### Overview

The OpenXml subsystem is the guaranteed content backend of `DocDown.Visio` — the path that recovers a
schematic's logical content with no Visio installation present. It is the extractor the engine selects for
a content extraction, the reader that turns a Visio Open Packaging container into the backend-neutral
model, and the image reader that resolves each embedded image's bytes and page association. Because
`DocumentFormat.OpenXml` has zero Visio types, the reader opens the package directly with
`System.IO.Packaging`, so the subsystem is fully managed and ships no native asset.

The reader recovers exactly what makes a drawing a schematic: each page's name, the text of every shape
that carries text, the master each shape was instantiated from (so a text-less shape is still classified
by its type), and — above all — the directed connections resolved from the connector records into real
directed edges. It is where a `.vsdx` or `.vsdm` becomes the model the rest of the package works on, and
it wraps a package it cannot open or a missing pages part in `VisioExtractionException` so Core sees a
structured failure.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from engine | .NET interface | `ProbeAvailability` unconditional, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| Source document | Inbound | `.vsdx` / `.vsdm` byte stream | Buffered because the package reader must seek |
| `System.IO.Packaging` | Outbound | OPC container API | Reads parts and relationships directly |

### Design

**The extractor.** `VisioOpenXmlExtractor` declares `Id = "visio-openxml"`, `SupportedFormats =
[Vsdx, Vsdm]`, `Priority = 10`, and the capability set `Text | EmbeddedImages | DocumentMetadata |
DocumentStructure` — pointedly not `RenderedPages`, because it ships no renderer. `ProbeAvailability` is
unconditional and performs no I/O, because `System.IO.Packaging` is a managed assembly. `ExtractAsync`
records the `visio.backend` and `visio.pageRendering` environment facts, buffers the source into memory
(a stream is not guaranteed seekable and the package reader must seek), reads the model, delegates emission
to `VisioContentEmitter`, and returns `Degraded` when the emitter reported any gap. `GetSelfTestCases`
contributes a topology round-trip case — it builds a one-page drawing with two labeled shapes and a
directed edge, reads it back, and confirms the connection resolved — and a page-rendering case that
reports skipped with a reason.

**The reader.** `VisioPackageReader` opens the package, reads `visio/pages/pages.xml` for each page's name
and its relationship to a page-contents part, reads that part's shapes and their text, and resolves each
connector's `Connect` records — whose `FromCell` of `BeginX` or `EndX` carries the direction — into
directed edges scoped to the page that owns them. It reads `visio/masters/masters.xml` once so a text-less
shape can be classified by its master name, falling back to the master's universal name where only that is
present and carrying no master name for a shape with no master, a dangling reference, or a drawing with no
masters part. It recovers a Wingdings arrow byte to its Unicode equivalent only within a run whose font
declares that symbol font, because a blind replacement would corrupt genuine text. Any packaging or XML
fault is wrapped in `VisioExtractionException`.

**The image reader.** `VisioImageReader` walks the container's parts, follows every internal image
relationship to its media part, and yields the bytes unchanged, deduplicated by package-part identity. An
image relationship on a page-contents part records that page's index as a referrer; one on a master flags
the image as template-referenced rather than giving it a fabricated page; a media part shared across pages
records every referring page. The package thumbnail is excluded — it is referenced by a thumbnail
relationship under `docProps`, not an image relationship on a page or master, and is document furniture
rather than embedded content.

### The inline supporting types (D8)

- **`VisioDocumentModel`** and its records (`VisioPageModel`, `VisioShapeModel`, `VisioConnectionModel`,
  `VisioPageImageRef`) — the backend-neutral model the reader populates and the emitter consumes, so every
  decision about what reaches the output is made once against a model that can be built by hand.
- **`VisioExtractionException`** — the one exception type this package translates itself, which Core
  surfaces as a structured failure with the full layout still written.
- **`VisioPackageBuilder`** — an in-memory synthesizer of Visio Open Packaging drawings used by the
  managed backend's self-test and by the test fixtures to build drawings at test time. It authors fixture
  packages; it does not extract user content, so it is a supporting type reviewed here rather than a unit.
- **`NamespaceDoc`** — the namespace documentation type for the `OpenXml` namespace.
