# DemaConsulting.DocDown.Visio System Design

![DemaConsulting.DocDown.Visio Structure](VisioView.svg)

`DemaConsulting.DocDown.Visio` is the Visio extraction system for the DocDown output contract. It reads each page
name, the text of the shapes that carry text, the directed connections between shapes, embedded
images, and metadata from modern `.vsdx` and `.vsdm` drawings, through the file itself. Rendered page
images are a separate, opt-in concern provided by the `DemaConsulting.DocDown.Visio.Rendering` package; this
system's own backend always states that it provides no rendered pages. Every artifact is written
through `IExtractionSink`, and the package reports what it extracted through looked-for content
inventory and short notes for attempted steps it could not complete. Legacy binary `.vsd` drawings
are not supported and are returned as unreadable.

## Architecture

The system has two subsystems and one direct unit:

- **VisioDocDownBuilderExtensions** *(direct unit)* is the registration seam. `AddVisio` registers
  the managed Visio backend without reflection or assembly scanning.
- **OpenXml** *(subsystem)* is the guaranteed managed content path. It contains the extractor the
  engine selects for Visio extraction, the package reader that turns a Visio Open Packaging
  container into the shared drawing model, and the image reader that resolves embedded image bytes
  and page association.
- **Markdown** *(subsystem)* is the reader-neutral projection. It contains the emitter that writes
  per-page content, looked-for inventory, image links, document info, metadata, and plain notes,
  and the shape labeler that decides how topology endpoints are named.

### Why there is one backend here

The managed backend can always recover the page names, shape text, topology, images, and metadata
from modern Visio packages, with no environment dependency. Rendered pages are a distinct
architectural concern — rasterization versus parsing — carried entirely by the separate
`DemaConsulting.DocDown.Visio.Rendering` package, which delegates back to this package's own
`VisioOpenXmlExtractor` for the managed aspects. This keeps content extraction deterministic and
available everywhere the package loads, while a host that needs rendered pages opts into the extra
dependency explicitly.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap, side-effect-free probe |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddVisio` is the surface |
| Source document | Inbound | `.vsdx` / `.vsdm` byte stream | Not guaranteed seekable |
| `ExtractionOutcome` | Outbound | `Produced` / `Unreadable` | Normal completion returns `Produced` |

The interfaces carry the following constraints:

- **`IDocumentExtractor`** is implemented by `VisioOpenXmlExtractor`, which supports
  `.vsdx` and `.vsdm`, carries priority `10`, leaves page rendering applicable for Visio drawings,
  and answers `ProbeAvailability()` with `Available()` because nothing about it is
  environment-dependent. The probe must stay well under 50 ms, open no document, and not throw.
- **`ISelfValidating`** contributes cheap-to-enumerate cases: a topology round-trip case and a
  rendering-skip case that reports page rendering as not provided by this extractor.
- **`IExtractionSink`** is the only output channel. No file in the output layout is written directly
  by this package.
- **`DocDownBuilder`** is extended by one method, `AddVisio`; there is no alternative registration
  path.
- **The source document** is opened through `DocumentSource.OpenRead`. Because the package reader
  must seek and the source stream is not guaranteed seekable, the backend buffers the whole drawing
  into memory before it reads it.
- **`ExtractionOutcome`** is limited to `Produced` and `Unreadable`. The backend returns
  `Produced` on normal completion; Core converts thrown unreadable drawings into `Unreadable` while
  still writing the standard output layout.

## Dependencies

- **DemaConsulting.DocDown.Core** — the extraction contract, sink, options, and output layout. See the
  *DemaConsulting.DocDown.Core System Design*.
- **System.IO.Packaging** (OTS) — the managed OPC container reader the OpenXml subsystem uses
  directly because `DocumentFormat.OpenXml` has no Visio types.

There is no native rendering library in this package. The managed backend is fully managed and ships
no native asset.

## Risk Control Measures

- **One reader and one emitter.** `VisioPackageReader` produces a backend-neutral model and
  `VisioContentEmitter` writes it. The rendering package reuses that same path by delegation, so a
  drawing's content reads the same whether or not it was rendered.
- **Managed content is always available.** The page names, shape text, topology, images, and
  metadata come from the Open Packaging drawing itself, so the logical content is recoverable on any
  machine that can run the package.
- **Reporting stays factual.** The content emitter reports looked-for counts for pages, labeled
  shapes, and connections, including zero. When DocDown attempted a step and could not complete it,
  it records a short note rather than inventing a judgment about the document.
- **Unreadable failures are contained.** Packaging faults propagate to Core, which
  converts them into `Unreadable` results with the full output layout still present.

## Data Flow

1. The engine detects `.vsdx` or `.vsdm` and selects the managed backend.
2. The managed backend records `visio.backend` and `visio.pageRendering` environment facts,
   buffers the drawing bytes, and hands a seekable stream to `VisioPackageReader`.
3. `VisioPackageReader.Read` produces a `VisioDocumentModel` containing pages in document order,
   shape text, directed connections, image references, and metadata.
4. `VisioContentEmitter.EmitAsync` reports looked-for inventory for pages, labeled shapes, and
   connections; writes empty content immediately when the drawing has no pages; writes image files
   before content so page sections can link them; writes per-page content as one flow, each page
   under its own heading; and writes document info and metadata.
5. When page rendering was requested but no rendering-capable backend is registered alongside this
   one, Core records a plain note that pages were not rendered; this backend neither claims to
   provide rendered pages nor reports one. Rendering, when requested with
   `DemaConsulting.DocDown.Visio.Rendering` also registered, is served entirely by that separate
   package.
6. The backend returns `Produced` on normal completion. Core finalizes `summary.txt`,
   `manifest.json`, and `metadata.json`. If an unreadable drawing threw during extraction, Core
   instead returns `Unreadable` while still writing the standard output layout.

### Mapping decisions

The mapping decisions that shape the Visio output are:

- **Directed topology is primary content.** Each connector is rendered as a readable directed edge in
  the direction the drawing declares.
- **Endpoint labels stay truthful.** A label comes from the shape's own text when possible,
  otherwise from the shape's master type, otherwise from the bare shape id. The content explains the
  convention whenever a non-authored label appears.
- **Only informative shape text is listed.** Bare numeric callouts are omitted from the shape list and
  counted in the page prose because they identify a legend entry rather than a component.
- **Embedded images stay linked to their pages.** Images are written through the sink, linked inline
  where a path was returned, and keep page or template provenance.
- **Empty drawings remain visible.** A drawing with no pages writes empty content and zero-count
  looked-for inventory so the absence is explicit.

## Design Constraints

- **The managed backend is fully managed.** It ships no native asset and depends only on
  `System.IO.Packaging`, which is why the guaranteed content path is available everywhere the package
  loads. It can read `.vsdx`/`.vsdm` content everywhere but it does not render pages itself;
  rendered page images are a separate, opt-in concern provided by the
  `DemaConsulting.DocDown.Visio.Rendering` package.
- **The whole drawing is buffered into memory.** This is a deliberate design choice because the
  package reader must seek.
- **The symbol-font recovery table is deliberately minimal.** Only documented mappings are recovered,
  because a wrong replacement is worse than a faithful oddity.
- **Legacy `.vsd` files are detected but not extracted.** The result is unreadable with a plain
  explanation that the legacy binary format is unsupported.
