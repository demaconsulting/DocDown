# DocDown.Pdf.Rendering System Design

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

`DocDown.Pdf.Rendering` is the optional PDF page-rendering backend for the DocDown output contract.
It produces the same text, embedded images, and document metadata the managed PDF backend produces,
and adds the one thing that backend cannot: raster images of the pages themselves. It is a
separately distributed, opt-in NuGet package that a host registers explicitly alongside
`DocDown.Core` and `DocDown.Pdf`.

It rasterizes through `DemaConsulting.CanvasNet.Pdf`/`DemaConsulting.CanvasNet`, a fully-managed PDF
rendering stack with no native binaries. Page rendering is still a separate package from
`DocDown.Pdf` because it is a distinct architectural concern — rasterization versus parsing — and an
additional dependency a consumer who only needs text and metadata should not have to carry, not
because of any native-binary consequence; both packages are fully managed and
runtime-identifier agnostic.

## Architecture

The system is flat: it has no subsystems, because there is one architectural boundary here —
rasterization — rather than several. Three units divide the work.

- **PdfPageRenderingExtractor** is the backend the engine selects and invokes. It exposes the stable
  PDF-rendering identity this package uses for selection and reporting; reports itself
  unconditionally available for rendered pages, since CanvasNet.Pdf has no load-time dependency that
  could be absent; delegates the managed aspects to the base PDF backend; drives the rasterization of
  the requested pages; reports a plain note when a page it attempted could not be rasterized; and
  returns `Produced` unless the delegated managed extraction reports `Unreadable`.
- **PageRenderer** is the single rasterization seam. It opens a `CanvasNet.Pdf.PdfDocument` per call,
  rasterizes one page to a `Surface`, and encodes it to PNG bytes with `PngCodec`. It is the only
  place a CanvasNet.Pdf or CanvasNet type appears; no other DocDown type names either package.
- **PdfRenderingDocDownBuilderExtensions** is the one visible edge from a host to this package: the
  single `AddPdfRendering` call that registers the backend. It carries no CanvasNet type on
  its surface, so a host can reference it without those types entering its own compilation.

### The single-backend selection problem, and its resolution

The engine selects exactly one backend for an extraction and runs only that backend. A rendering
backend that produced only raster images would therefore leave the managed PDF backend's text,
embedded-image, and metadata work undone. So the rendering backend cannot be a "pages-only" add-on
layered onto the base backend: once selected, it must deliver the same managed artifacts the base
backend delivers and add rendered pages on top.

It delivers the first three not by re-implementing them but by **delegating to a
`PdfDocumentExtractor`** with a cloned options object whose `RenderPages` is forced off. The managed
backend writes the content, the images, the document info, and its own notes; because rendering is
suppressed on the delegate, it does not attempt rendered-page output itself. This backend then
rasterizes the requested pages and adds them. The one honest artifact of the delegation is that the
managed backend's `pdf.pageRendering = not provided by this extractor` environment fact still appears
in a run that did render — which is literally true of the managed inner backend and is complemented,
not contradicted, by this backend's own `pages.renderer` fact.

Selection learns that this backend can satisfy a render request from
`ProbeAvailability()`, which unconditionally returns
`ExtractorAvailability.Available(providesRenderedPages: true)`: CanvasNet.Pdf is a fully-managed
dependency resolved at restore time, with no runtime load step that could fail, so there is nothing
to probe for. That is the only selection-time statement this package makes about rendered pages.

### The division of honesty between Core, the managed backend, and this package

Core knows the selection-time picture: which backends were registered, which one was chosen, and
whether any available backend in this environment could render pages. The managed backend knows what
a PDF reader knows: which encoding an image used and whether the pages carried glyphs. This package
supplies the third layer — the facts only a rasterizer knows.

Rendering is always available, so this package never reports a selection-time unavailability. If
rendering begins and a specific page cannot be rasterized, this package reports one short factual
note — `Page N could not be rasterized.` — because it attempted that step and could not complete it.
The note stays limited to that extraction fact itself.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `PdfDocumentExtractor` | Outbound, to DocDown.Pdf | .NET class | Delegated to with rendering suppressed |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPdfRendering` is the whole surface |
| Source document | Inbound | PDF byte stream | Not guaranteed seekable; buffered once |

- **`IDocumentExtractor`** is implemented by `PdfPageRenderingExtractor`. `ProbeAvailability` must be
  well under 50 ms, side-effect free, must not open the document, and must not throw; it
  unconditionally returns `Available(providesRenderedPages: true)` and never rasterizes.
- **`ISelfValidating`** enumeration is cheap; the render round-trip work happens only when the case's
  delegate is invoked.
- **`IExtractionSink`** is the only output channel; no filesystem path is ever constructed here.
- **`PdfDocumentExtractor`** is constructed and driven directly to produce the managed aspects, so
  the managed extraction is a single source of truth rather than a re-implementation.
- **`DocDownBuilder`** is extended by exactly one method, `AddPdfRendering`.
- **The source document** is buffered before use because both the managed backend and the
  rasterizer read it from the start, and a stream source is read-once and possibly non-seekable.

No CanvasNet.Pdf or CanvasNet type appears on any public interface. That containment is
machine-enforced by a reflection test over the package's exported types.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout.
- **DocDown.Pdf** — the managed text/embedded-image/metadata extractor this package delegates to.
  This is a project reference; it adds no native asset.
- **CanvasNet.Pdf** (OTS) — the fully-managed PDF rasterization API: `PdfDocument.Open`,
  `GetPageInfo`, and `Render` produce a `Surface`. Confined to `PageRenderer` and absent from the
  public API. See *CanvasNet.Pdf* under the OTS integration design.
- **CanvasNet** (OTS) — the fully-managed 2D canvas/codec library CanvasNet.Pdf builds on; this
  package references it directly for `PngCodec.Save`, which encodes the rendered `Surface` to PNG
  bytes. See *CanvasNet* under the OTS integration design.
- **PdfPig** — reached only through the `DocDown.Pdf` project reference, for the delegated managed
  extraction. No type in this package names it.

## Risk Control Measures

- **Rasterizer containment.** CanvasNet.Pdf and CanvasNet types appear in exactly one file
  (`PageRenderer.cs`) and in no public signature. A reflection test fails the build if either
  reaches the exported surface, so the rasterizer stays confined and replaceable.
- **Portability of the whole package graph.** `DocDown.Core`, `DocDown.Pdf`, `DocDown.Pdf.Rendering`,
  and `DocDown.Tool` are all fully managed and free of runtime-identifier-specific dependencies. That
  the PDF packages ship no native asset is asserted against their produced `.nupkg` files, not merely
  their build output.
- **Per-page fault isolation.** Each page is rasterized independently. An unsupported page or an
  out-of-memory at a high DPI is caught per page, recorded as the plain note
  `Page N could not be rasterized.`, and the run continues with the remaining pages. No render
  fault reaches the caller as an exception.
- **Unconditional, honest availability.** CanvasNet.Pdf is a fully-managed dependency resolved at
  restore time, with nothing to probe for at run time, so `ProbeAvailability()` always reports page
  rendering as available; a plain extraction that does not request rendering still pays nothing,
  because the backend is only selected and only opens a document when rendering is requested.
- **Independent per-call document instances.** `PageRenderer` opens and disposes its own
  `PdfDocument` per call rather than sharing mutable state across calls, so concurrent renders do not
  corrupt each other's state. CanvasNet.Pdf's thread-safety is not documented either way; this
  isolation is the mitigation, and the concurrent-render test in this package's test suite is the
  regression guard for it. The one known shared mutable state in the dependency graph is CanvasNet's
  lazily-initialized system font catalog, which is accepted as a residual risk: a race there could
  only affect font substitution during concurrent first-use renders, never data integrity, and no
  text is ever rendered from an untrusted source through this path.

## Data Flow

1. The engine selects this backend when a PDF is detected and page rendering is requested, and calls
   `ExtractAsync` with a context exposing the options, the sink, and a cancellation token.
2. `PdfPageRenderingExtractor` buffers the source once, then delegates to a `PdfDocumentExtractor`
   with a cloned options object whose `RenderPages` is off. The managed backend writes the content,
   images, metadata, inventory, and its own notes. If the delegated extractor reports
   `Unreadable`, this backend returns `Unreadable` and does not attempt page rendering. Because
   rendering is suppressed on the delegate and selection already handled ordinary renderer
   unavailability, this backend adds no "renderer unavailable" note of its own.
3. It records the authoritative `pages.renderer` environment fact.
4. It selects the pages to render, honoring any requested range, and for each renders a PNG through
   `PageRenderer` and writes it through the sink, named by its document page number. A per-page fault
   becomes the note `Page N could not be rasterized.`; the run continues.
5. It returns `Produced` after the attempted page loop completes.
6. The engine finalizes the content and writes `summary.txt` and `manifest.json`.

## Design Constraints

- **CanvasNet.Pdf replaces the previously used PDFtoImage (PDFium/SkiaSharp) backend.** CanvasNet.Pdf
  is fully managed, with its only transitive dependency being `System.Numerics.Tensors`, confirmed
  to carry no native binaries. Adopting it removes the per-runtime-identifier native asset, the
  publish-matrix and single-file-extraction concerns, and the process-wide rasterization lock that a
  native rasterizer required.
- **No `<RuntimeIdentifier>` in the library project, and nothing RID-specific to resolve.** The
  package is RID-agnostic end to end: a framework-dependent reference and a self-contained
  single-file publish behave the same way on every supported platform, because there is no
  per-runtime-identifier native asset to select at publish or run time.
- **Thread-safety is not documented by CanvasNet.Pdf either way.** This package does not rely on any
  shared mutable rasterization state: `PageRenderer` opens, uses, and disposes its own
  `PdfDocument` per call. See the font-catalog residual risk recorded under Risk Control Measures
  above.
- **Trim and AOT compatibility are unverified for this stack.** They are not claimed. `PublishTrimmed`
  and AOT are left off, and this constraint is recorded rather than worked around; explicit,
  reflection-free registration keeps single-file publish viable regardless.
