# DocDown.Visio System Design

![DocDown.Visio Structure](VisioView.svg)

`DocDown.Visio` is the Visio extraction system for the DocDown output contract. It reads each page
name, the text of the shapes that carry text, the directed connections between shapes, embedded
images, and metadata from modern `.vsdx` and `.vsdm` drawings. When Microsoft Visio is available,
it also renders each page to a PNG. Every artifact is written through `IExtractionSink`, and the
package reports what it extracted through looked-for content inventory and short notes for attempted
steps it could not complete. Legacy binary `.vsd` drawings are not supported and are returned as
unreadable.

## Architecture

The system has three subsystems and one direct unit:

- **VisioDocDownBuilderExtensions** *(direct unit)* is the registration seam. `AddVisio` registers
  both Visio backends without reflection or assembly scanning.
- **OpenXml** *(subsystem)* is the guaranteed managed content path. It contains the extractor the
  engine selects for ordinary Visio extraction, the package reader that turns a Visio Open Packaging
  container into the shared drawing model, and the image reader that resolves embedded image bytes
  and page association.
- **Markdown** *(subsystem)* is the reader-neutral projection. It contains the emitter that writes
  per-page content, looked-for inventory, image links, document info, metadata, and plain notes,
  and the shape labeler that decides how topology endpoints are named.
- **Com** *(subsystem)* is the rendering seam. It contains the composing extractor that delegates the
  managed content path and adds rendered pages, the availability probe that keeps rendering honest,
  and the real Microsoft Visio automation adapter.

### Why there are two backends

The engine selects exactly one backend. The managed backend is the ordinary default because it can
always recover the page names, shape text, topology, images, and metadata from modern Visio
packages. The COM backend exists only to add rendered pages. It therefore supports the same drawing
formats, ranks below the managed backend, and becomes relevant only when page rendering was
requested and its availability probe can report `Available(providesRenderedPages: true)`. This keeps
content extraction deterministic while still using Microsoft Visio where it adds value.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap, side-effect-free probes |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddVisio` is the surface |
| Source document | Inbound | `.vsdx` / `.vsdm` byte stream | Not guaranteed seekable |
| Microsoft Visio | Outbound, from the COM adapter | Late-bound IDispatch | Windows-only; environment-dependent |
| `ExtractionOutcome` | Outbound | `Produced` / `Unreadable` | Normal completion returns `Produced` |

The interfaces carry the following constraints:

- **`IDocumentExtractor`** is implemented by two backends. `VisioOpenXmlExtractor` supports
  `.vsdx` and `.vsdm`, carries priority `10`, leaves page rendering applicable for Visio drawings,
  and answers `ProbeAvailability()` with `Available()`. `VisioComExtractor` supports the same
  formats, carries priority `0`, and answers `ProbeAvailability()` with
  `Available(providesRenderedPages: true)` when Microsoft Visio can be reached, otherwise
  `Unavailable(reason)`. Both probes must stay well under 50 ms, open no document, launch no
  application, and not throw.
- **`ISelfValidating`** contributes cheap-to-enumerate cases. The managed backend contributes a
  topology round-trip case and a rendering-skip case; the COM backend contributes an availability
  case that skips cleanly off Windows.
- **`IExtractionSink`** is the only output channel. No file in the output layout is written directly
  by this package. The COM backend may materialize a temporary input file because Microsoft Visio
  opens paths rather than streams, and it deletes that file on every path it owns.
- **`DocDownBuilder`** is extended by one method, `AddVisio`; there is no alternative registration
  path.
- **The source document** is opened through `DocumentSource.OpenRead`. Because the package reader
  must seek and the source stream is not guaranteed seekable, both backends buffer the whole drawing
  into memory before they read it.
- **Microsoft Visio** is reached only by the COM adapter, only on Windows, and only after the
  availability probe has already confirmed that rendering is available in the current environment.
- **`ExtractionOutcome`** is limited to `Produced` and `Unreadable`. Both Visio backends return
  `Produced` on normal completion; Core converts thrown unreadable drawings into `Unreadable` while
  still writing the standard output layout.

## Dependencies

- **DocDown.Core** — the extraction contract, sink, options, and output layout. See the
  *DocDown.Core System Design*.
- **System.IO.Packaging** (OTS) — the managed OPC container reader the OpenXml subsystem uses
  directly because `DocumentFormat.OpenXml` has no Visio types.
- **Microsoft Visio** (installed application, optional, Windows-only) — reached by the COM backend
  over late-bound IDispatch to render pages. It is discovered at runtime and is not a restored or
  shipped package dependency.

There is no native rendering library in the package itself. The managed backend is fully managed,
and the COM backend talks to Microsoft Visio through late binding with no interop assembly on the
public surface.

## Risk Control Measures

- **One reader and one emitter.** `VisioPackageReader` produces a backend-neutral model and
  `VisioContentEmitter` writes it. The COM backend reuses that same path by delegation, so a
  drawing's content reads the same whether or not it was rendered.
- **Managed content is always available.** The page names, shape text, topology, images, and
  metadata come from the Open Packaging drawing itself, so the logical content is recoverable on any
  machine that can run the package.
- **Reporting stays factual.** The content emitter reports looked-for counts for pages, labeled
  shapes, and connections, including zero. When DocDown attempted a step and could not complete it —
  such as rendering one page through COM — it records a short note
  rather than inventing a judgment about the document.
- **COM is tightly contained.** No COM type appears on the public registration surface, and the real
  automation adapter is the single Windows-only boundary.
- **Unreadable failures are contained.** Packaging or automation faults propagate to Core, which
  converts them into `Unreadable` results with the full output layout still present.

## Data Flow

1. The engine detects `.vsdx` or `.vsdm`, considers whether page rendering was requested, and selects
   either the managed backend or the COM backend based on supported formats, rendering
   applicability, priority, and availability.
2. The managed backend records `visio.backend` and `visio.pageRendering` environment facts,
   buffers the drawing bytes, and hands a seekable stream to `VisioPackageReader`.
3. `VisioPackageReader.Read` produces a `VisioDocumentModel` containing pages in document order,
   shape text, directed connections, image references, and metadata.
4. `VisioContentEmitter.EmitAsync` reports looked-for inventory for pages, labeled shapes, and
   connections; writes empty content immediately when the drawing has no pages; writes image files
   before content so page sections can link them; writes per-page content as one flow or one part
   per page; writes document info and metadata; and records a plain note when a requested PNG image
   output cannot be honored and source-encoded bytes were written instead.
5. When page rendering was requested but the selected backend does not provide rendered pages in the
   current environment, Core records a plain note that pages were not rendered. When the COM backend
   runs, it delegates the managed content path through a composing sink, records `pages.renderer`,
   materializes the drawing to a path if needed, renders each foreground page through Microsoft
   Visio, and records a plain note for any page that could not be rendered.
6. Both backends return `Produced` on normal completion. Core finalizes `summary.txt`,
   `manifest.json`, and `metadata.json`. If an unreadable drawing or automation fault threw during
   extraction, Core instead returns `Unreadable` while still writing the standard output layout.

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
  loads.
- **Rendering is Windows- and Visio-dependent.** The COM backend runs only where Microsoft Visio can
  be reached and can report rendered-page support for the current environment.
- **The whole drawing is buffered into memory.** This is a deliberate design choice because the
  package reader must seek.
- **The symbol-font recovery table is deliberately minimal.** Only documented mappings are recovered,
  because a wrong replacement is worse than a faithful oddity.
- **Legacy `.vsd` files are detected but not extracted.** The result is unreadable with a plain
  explanation that the legacy binary format is unsupported.
