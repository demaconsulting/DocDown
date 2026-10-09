## VisioPageRenderingExtractor

![DemaConsulting.DocDown.Visio.Rendering Structure](DocDownVisioRenderingView.svg)

### Purpose

`VisioPageRenderingExtractor` is the backend the engine selects and invokes when a VSDX/VSDM is
detected and page rendering is requested. Its single responsibility is to deliver the full DocDown
output for a Visio drawing — page names, shape text, directed connector topology, embedded images,
document metadata, and rendered page images — while carrying the rasterization cost only when
rendering is actually asked for.

It exists because the engine selects exactly one backend. A rendering-only backend would leave the
managed Visio backend's work undone, so this unit must deliver the managed artifacts too, obtaining
them by delegation and adding rendered pages itself. Rendering is always available, since
CanvasNet.Vsdx is fully managed, so `ProbeAvailability()` is unconditional.

### Data Model

`VisioPageRenderingExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state. Its fields are the rasterization function it
drives (default `PageRenderer.Render`) and the page-count function it drives (default
`PageRenderer.GetPageCount`); internal constructors accept substitutes so a test can inject a
faulting renderer or counter and exercise the per-page note path and the count-failure path without
depending on CanvasNet.Vsdx actually failing.

### Key Methods

- **`ExtractAsync(DocumentSource, IExtractionContext)`** — buffers the source once; delegates the
  managed aspects to a `VisioOpenXmlExtractor` with `RenderPages` forced off via a
  `DelegatedExtractionContext`; records the `pages.renderer` environment fact; and, only when
  `options.RenderPages` is `true`, rasterizes the requested pages, isolating each page's faults.
  The guard matters because selection may hand this backend a non-rendering request when it is the
  only registered candidate for `.vsdx`/`.vsdm` (a host that calls `AddVisioRendering()` without the
  managed backend), and rasterization must not run uninvited in that case. Returns `Produced`
  unless the delegated managed extractor reports `Unreadable`. Precondition: `source` and `context`
  non-null. Postcondition: when rendering was requested, every selected page is either written or
  named in a plain note; when it was not, no page is rasterized.
- **`ProbeAvailability()`** — unconditionally returns `ExtractorAvailability.Available(
  providesRenderedPages: true)`, since CanvasNet.Vsdx is a fully-managed dependency resolved at
  restore time with nothing to probe for at run time. Never rasterizes.
- **Descriptor members (`Id`, `DisplayName`, `SupportedFormats`, and `Priority`)** — expose the
  stable identity (`"visio-rendering"`, `"Visio pages (CanvasNet.Vsdx)"`, `[Vsdx, Vsdm]`,
  priority `5`) selection and reporting use when this backend participates in an extraction.
  The engine's selection step first narrows candidates to those that provide rendered pages
  whenever rendering is requested, so this backend's priority only decides ordering against other
  page-rendering-capable candidates; against the managed `VisioOpenXmlExtractor`'s priority
  `10` when rendering is not requested, the managed backend wins, which is correct because this
  backend's extra rasterization cost is unwanted in that case.
- **`GetSelfTestCases()`** — contributes one case, `visio-rendering.renderRoundTrip`, named
  distinctly from the managed backend's own self-test cases so existing case-name assertions stay
  valid.

### Error Handling

If the delegated managed extraction reports `Unreadable`, this unit returns `Unreadable` unchanged
and does not attempt page rendering. A page-count fault is caught separately: the sink already has
the delegated content, so it is reported as a single note and the extraction still returns
`Produced`. When the page count succeeds but selects zero pages to render — an empty drawing, or a
page range matching nothing — this unit reports that outcome itself, rather than relying on Core to
infer it: Core cannot tell this unit's own explanation apart from an unrelated note the delegated
managed backend may already have recorded during its earlier content phase, so this unit records the
note from its own selected-page count instead. During rendering, cancellation propagates; every
other per-page rasterization fault is caught in place, reported as `Page N could not be
rasterized.`, and the run continues. No render fault reaches the caller as an exception.

### Dependencies

- **DemaConsulting.DocDown.Core** — the extractor contract, the sink, the options, and the self-test types.
- **DemaConsulting.DocDown.Office** — `VisioOpenXmlExtractor`, delegated to for the managed aspects.
- **PageRenderer** — the rasterization seam. See *PageRenderer Design*.

### Callers

The engine, once per extraction it selects this backend for. The registration seam constructs it via
a factory.
