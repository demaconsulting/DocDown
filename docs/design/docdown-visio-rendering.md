# DemaConsulting.DocDown.Visio.Rendering System Design

![DemaConsulting.DocDown.Visio.Rendering Structure](DocDownVisioRenderingView.svg)

`DemaConsulting.DocDown.Visio.Rendering` is the optional page-rendering backend for the DocDown output
contract. It produces the same shape text, embedded images, and document metadata the managed Visio
backend produces, and adds the one thing that backend cannot: raster images of the pages
themselves. It is a separately distributed, opt-in NuGet package that a host registers explicitly
alongside `DemaConsulting.DocDown.Core` and `DemaConsulting.DocDown.Office`.

It rasterizes through `DemaConsulting.CanvasNet.Vsdx`/`DemaConsulting.CanvasNet`, a fully-managed
rendering stack with no native binaries. Page rendering is still a separate package from
`DemaConsulting.DocDown.Office` because it is a distinct architectural concern — rasterization versus parsing — and
an additional dependency a consumer who only needs text and metadata should not have to carry, not
because of any native-binary consequence; both packages are fully managed and
runtime-identifier agnostic.

## Architecture

The system is flat: it has no subsystems, because there is one architectural boundary here —
rasterization — rather than several. Three units divide the work.

- **VisioPageRenderingExtractor** is the backend the engine selects and invokes. It exposes the
  stable Visio-rendering identity this package uses for selection and reporting; reports itself
  unconditionally available for rendered pages, since CanvasNet.Vsdx has no load-time dependency that
  could be absent; delegates the managed aspects to the base Visio backend; drives the
  rasterization of the requested pages; reports a plain note when a page it attempted could not be
  rasterized; and returns `Produced` unless the delegated managed extraction reports `Unreadable`.
- **PageRenderer** is the single rasterization seam. It opens a `CanvasNet.Vsdx.VsdxDocument` per
  call, rasterizes one page to a `Surface`, and encodes it to PNG bytes with `PngCodec`. It is the
  only place a CanvasNet.Vsdx or CanvasNet type appears; no other DocDown type names either package.
- **VisioRenderingDocDownBuilderExtensions** is the one visible edge from a host to this
  package: the single `AddVisioRendering` call that registers the backend. It carries no
  CanvasNet type on its surface, so a host can reference it without those types entering its own
  compilation.

### The single-backend selection problem, and its resolution

The engine selects exactly one backend for an extraction and runs only that backend. A rendering
backend that produced only raster images would therefore leave the managed Visio backend's
page-name, shape-text, topology, and metadata work undone. So the rendering backend cannot be a
"pages-only" add-on layered onto the base backend: once selected, it must deliver the same managed
artifacts the base backend delivers and add rendered pages on top.

It delivers the first by **delegating to a `VisioOpenXmlExtractor`** with a cloned options object
whose `RenderPages` is forced off. The managed backend writes the content, the images, the document
info, and its own notes; because rendering is suppressed on the delegate, it does not attempt
rendered-page output itself. This backend then rasterizes the requested pages and adds them. The one
honest artifact of the delegation is that the managed backend's own page-rendering environment fact
still reflects "not provided by this extractor" in a run that did render — which is literally true of
the managed inner backend and is complemented, not contradicted, by this backend's own
`pages.renderer` fact.

Selection learns that this backend can satisfy a render request from
`ProbeAvailability()`, which unconditionally returns
`ExtractorAvailability.Available(providesRenderedPages: true)`: CanvasNet.Vsdx is a fully-managed
dependency resolved at restore time, with no runtime load step that could fail, so there is nothing
to probe for. That is the only selection-time statement this package makes about rendered pages.

### The division of honesty between Core, the managed backend, and this package

Core knows the selection-time picture: which backends were registered, which one was chosen, and
whether any available backend in this environment could render pages. The managed backend knows what
an Open Packaging reader knows: which page names, shape text, and directed connector topology a
drawing carries. This package supplies the third layer — the facts only a rasterizer knows.

Rendering is always available, so this package never reports a selection-time unavailability. If
rendering begins and a specific page cannot be rasterized, this package reports one short factual
note — `Page N could not be rasterized.` — because it attempted that step and could not complete
it. The note stays limited to that extraction fact itself.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `VisioOpenXmlExtractor` | Outbound, to DemaConsulting.DocDown.Office | .NET class | Rendering suppressed |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddVisioRendering` is the whole surface |
| Source document | Inbound | VSDX/VSDM byte stream | Not guaranteed seekable; buffered once |

- **`IDocumentExtractor`** is implemented by `VisioPageRenderingExtractor`. `ProbeAvailability`
  must be well under 50 ms, side-effect free, must not open the document, and must not throw; it
  unconditionally returns `Available(providesRenderedPages: true)` and never rasterizes.
- **`ISelfValidating`** enumeration is cheap; the render round-trip work happens only when the case's
  delegate is invoked.
- **`IExtractionSink`** is the only output channel; no filesystem path is ever constructed here.
- **`VisioOpenXmlExtractor`** is constructed and driven directly to produce the managed aspects,
  so the managed extraction is a single source of truth rather than a re-implementation.
- **`DocDownBuilder`** is extended by exactly one method, `AddVisioRendering`.
- **The source document** is buffered before use because both the managed backend and the
  rasterizer read it from the start, and a stream source is read-once and possibly non-seekable.

No CanvasNet.Vsdx or CanvasNet type appears on any public interface. That containment is
machine-enforced by a reflection test over the package's exported types.

## Dependencies

- **DemaConsulting.DocDown.Core** — the extraction contract, the sink, the options, and the output layout.
- **DemaConsulting.DocDown.Office** — the managed shape-text/topology/metadata extractor this package delegates to
  (`VisioOpenXmlExtractor`). This is a project reference; it adds no native asset.
- **CanvasNet.Vsdx** (OTS) — the fully-managed VSDX/VSDM rasterization API: `VsdxDocument.Open`,
  `PageCount`, and `Render` produce a `Surface`. Confined to `PageRenderer` and absent from the
  public API. See *CanvasNet.Vsdx* under the OTS integration design.
- **CanvasNet** (OTS) — the fully-managed 2D canvas/codec library CanvasNet.Vsdx builds on; this
  package references it directly for `PngCodec.Save`, which encodes the rendered `Surface` to PNG
  bytes. See *CanvasNet* under the OTS integration design. Unlike
  `DemaConsulting.DocDown.PowerPoint.Rendering`'s dependency on `CanvasNet.Pptx`, CanvasNet.Vsdx
  carries no `CanvasNet.Charts`-equivalent dependency, since Visio drawings have no embedded chart
  content to rasterize.
- **System.IO.Packaging** — reached only through the `DemaConsulting.DocDown.Office` project reference, for the
  delegated managed extraction. No type in this package names it.

## Risk Control Measures

- **Rasterizer containment.** CanvasNet.Vsdx and CanvasNet types appear in exactly
  one file (`PageRenderer.cs`) and in no public signature. A reflection test fails the build if any
  of them reaches the exported surface, so the rasterizer stays confined and replaceable.
- **Portability of the whole package graph.** `DemaConsulting.DocDown.Core`, `DemaConsulting.DocDown.Office`,
  `DemaConsulting.DocDown.Visio.Rendering`, and `DemaConsulting.DocDown.Tool` are all fully managed and free of
  runtime-identifier-specific dependencies. That this package ships no native asset is asserted
  against its produced `.nupkg` file, not merely its build output.
- **Per-page fault isolation.** Each page is rasterized independently. An unsupported page feature
  or an out-of-memory at a high DPI is caught per page, recorded as the plain note
  `Page N could not be rasterized.`, and the run continues with the remaining pages. No render
  fault reaches the caller as an exception, except cancellation, which always propagates.
- **Unconditional, honest availability.** CanvasNet.Vsdx is a fully-managed dependency resolved at
  restore time, with nothing to probe for at run time, so `ProbeAvailability()` always reports page
  rendering as available; a plain extraction that does not request rendering still pays nothing,
  because the backend is only selected and only opens a document when rendering is requested.
- **Independent per-call document instances.** `PageRenderer` opens and disposes its own
  `VsdxDocument` per call rather than sharing mutable state across calls, so concurrent renders do
  not corrupt each other's state.

## Data Flow

1. The engine selects this backend when a VSDX/VSDM is detected and page rendering is requested, and
   calls `ExtractAsync` with a context exposing the options, the sink, and a cancellation token.
2. `VisioPageRenderingExtractor` buffers the source once, then delegates to a
   `VisioOpenXmlExtractor` with a cloned options object whose `RenderPages` is off. The managed
   backend writes the content, images, metadata, inventory, and its own notes. If the delegated
   extractor reports `Unreadable`, this backend returns `Unreadable` and does not attempt page
   rendering. Because rendering is suppressed on the delegate and selection already handled ordinary
   renderer unavailability, this backend adds no "renderer unavailable" note of its own.
3. It records the authoritative `pages.renderer` environment fact.
4. It selects the pages to render, honoring any requested range, and for each renders a PNG through
   `PageRenderer` and writes it through the sink, named by its page number. A per-page fault
   becomes the note `Page N could not be rasterized.`; the run continues.
5. It returns `Produced` after the attempted page loop completes.
6. The engine finalizes the content and writes `summary.txt` and `manifest.json`.

## Design Constraints

- **CanvasNet.Vsdx is a fully-managed rasterizer with no native binaries.** Its one transitive
  dependency, CanvasNet, is confirmed to carry no native assets either. Adopting it means this
  backend never introduces a per-runtime-identifier native asset, a publish-matrix concern, or a
  process-wide rasterization lock.
- **No `<RuntimeIdentifier>` in the library project, and nothing RID-specific to resolve.** The
  package is RID-agnostic end to end: a framework-dependent reference and a self-contained
  single-file publish behave the same way on every supported platform, because there is no
  per-runtime-identifier native asset to select at publish or run time.
- **CanvasNet.Vsdx's `Render` overload accepts `dpi` as an `int`, not a `float`.** This is a
  documented API deviation from `DemaConsulting.CanvasNet.Pptx`'s own float-DPI overload that
  `DemaConsulting.DocDown.PowerPoint.Rendering`'s `SlideRenderer` uses. `PageRenderer` passes the
  caller's DPI through unchanged rather than casting, so the deviation is surfaced rather than
  papered over.
- **`.vsdm` support is inherited by construction, not independently verified.** CanvasNet.Vsdx reads
  the same Open Packaging Conventions container the managed backend reads, so the macro-enabled
  `.vsdm` variant — the same package structure with only a different macro-enabled content type — is
  expected to rasterize identically to `.vsdx`. This is a documented, untested extrapolation: this
  codebase has not exercised CanvasNet.Vsdx against a real `.vsdm` sample, only `.vsdx` probes.
- **CanvasNet.Vsdx's rasterization fidelity has documented gaps.** Spline and bezier curve geometry
  and ellipse shapes have deferred support, the arrow-style table is incomplete, and theme
  resolution is simplified. These are the same category of fidelity gap documented for
  CanvasNet.Pptx; a page whose drawing relies on one of these features may rasterize with reduced
  fidelity rather than with a reported fault.
- **Thread-safety is not documented by CanvasNet.Vsdx either way.** This package does not rely on any
  shared mutable rasterization state: `PageRenderer` opens, uses, and disposes its own
  `VsdxDocument` per call.
- **Trim and AOT compatibility are unverified for this stack.** They are not claimed. `PublishTrimmed`
  and AOT are left off, and this constraint is recorded rather than worked around; explicit,
  reflection-free registration keeps single-file publish viable regardless.
