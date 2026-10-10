## PowerPointPageRenderingExtractor

![DemaConsulting.DocDown.PowerPoint.Rendering Structure](DocDownPowerPointRenderingView.svg)

### Purpose

`PowerPointPageRenderingExtractor` is the backend the engine selects and invokes when a PPTX is
detected and page rendering is requested. Its single responsibility is to deliver the full DocDown
output for a PowerPoint presentation — text, embedded images, document metadata, and rendered slide
images — while carrying the rasterization cost only when rendering is actually asked for.

It exists because the engine selects exactly one backend. A rendering-only backend would leave the
managed PowerPoint backend's work undone, so this unit must deliver the managed artifacts too,
obtaining them by delegation and adding rendered slides itself. Rendering is always available, since
CanvasNet.Pptx is fully managed, so `ProbeAvailability()` is unconditional.

### Data Model

`PowerPointPageRenderingExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state. Its fields are the rasterization function it
drives (default `SlideRenderer.Render`) and the slide-count function it drives (default
`SlideRenderer.GetSlideCount`); internal constructors accept substitutes so a test can inject a
faulting renderer or counter and exercise the per-slide note path and the count-failure path without
depending on CanvasNet.Pptx actually failing.

### Key Methods

- **`ExtractAsync(DocumentSource, IExtractionContext)`** — buffers the source once; delegates the
  managed aspects to a `PowerPointOpenXmlExtractor` with `RenderPages` forced off via a
  `DelegatedExtractionContext`; records the `pages.renderer` environment fact; and, only when
  `options.RenderPages` is `true`, rasterizes the requested slides, isolating each slide's faults.
  The guard matters because selection may hand this backend a non-rendering request when it is the
  only registered candidate for `.pptx` (a host that calls `AddPowerPointRendering()` without the
  managed backend), and rasterization must not run uninvited in that case. Returns `Produced`
  unless the delegated managed extractor reports `Unreadable`. Precondition: `source` and `context`
  non-null. Postcondition: when rendering was requested, every selected slide is either written or
  named in a plain note; when it was not, no slide is rasterized.
- **`ProbeAvailability()`** — unconditionally returns `ExtractorAvailability.Available(
  providesRenderedPages: true)`, since CanvasNet.Pptx is a fully-managed dependency resolved at
  restore time with nothing to probe for at run time. Never rasterizes.
- **Descriptor members (`Id`, `DisplayName`, `SupportedFormats`, and `Priority`)** — expose the
  stable identity (`"powerpoint-rendering"`, `"PowerPoint slides (CanvasNet.Pptx)"`, `[Pptx]`,
  priority `5`) selection and reporting use when this backend participates in an extraction.
  The engine's selection step first narrows candidates to those that provide rendered pages
  whenever rendering is requested, so this backend's priority only decides ordering against other
  page-rendering-capable candidates; against the managed `PowerPointOpenXmlExtractor`'s priority
  `10` when rendering is not requested, the managed backend wins, which is correct because this
  backend's extra rasterization cost is unwanted in that case.
- **`GetSelfTestCases()`** — contributes one case, `powerpoint-rendering.renderRoundTrip`, named
  distinctly from the managed backend's own self-test cases so existing case-name assertions stay
  valid.

### Error Handling

If the delegated managed extraction reports `Unreadable`, this unit returns `Unreadable` unchanged
and does not attempt slide rendering. A slide-count fault is caught separately: the sink already has
the delegated content, so it is reported as the single note
`Slides could not be counted, so no slide images were rendered.` and the extraction still returns
`Produced`. When the slide count succeeds but selects zero slides to render — an empty presentation,
or a page range matching nothing — this unit reports that outcome itself, as the single note "Page
rendering was requested and a renderer was available, but no pages were produced.", rather than
relying on Core to infer it: Core cannot tell this unit's own explanation apart from an unrelated
note the delegated managed backend may already have recorded during its earlier content phase, so
this unit records the note from its own selected-slide count instead. During rendering, cancellation
propagates; every other per-slide rasterization fault —
`InvalidDataException`, `ArgumentOutOfRangeException`, `OutOfMemoryException`, and a final
catch-all covering any other exception CanvasNet.Pptx throws (including its own
`PptxUnsupportedFeatureException`, caught generically rather than named here so this unit does not
need its own reference to that CanvasNet.Pptx type) — is caught in place, reported as `Slide N could
not be rasterized.`, and the run continues. No render fault reaches the caller as an exception.

### Dependencies

- **DemaConsulting.DocDown.Core** — the extractor contract, the sink, the options, and the self-test types.
- **DemaConsulting.DocDown.Office** — `PowerPointOpenXmlExtractor`, delegated to for the managed aspects.
- **SlideRenderer** — the rasterization seam. See *SlideRenderer Design*.

### Callers

The engine, once per extraction it selects this backend for. The registration seam constructs it via
a factory.
