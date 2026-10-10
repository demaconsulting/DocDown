## PdfPageRenderingExtractor

![DemaConsulting.DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PdfPageRenderingExtractor` is the backend the engine selects and invokes when a PDF is detected and
page rendering is requested. Its single responsibility is to deliver the full DocDown output for a
PDF — text, embedded images, document metadata, and rendered page images — while carrying the
rasterization cost only when rendering is actually asked for.

It exists because the engine selects exactly one backend. A rendering-only backend would leave the
managed PDF backend's work undone, so this unit must deliver the managed artifacts too, obtaining
them by delegation and adding rendered pages itself. Rendering is always available, since
CanvasNet.Pdf is fully managed, so `ProbeAvailability()` is unconditional.

### Data Model

`PdfPageRenderingExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state. Its only field is the rasterization function it
drives, which defaults to `PageRenderer.Render`; an internal constructor accepts a substitute so a
test can inject a faulting renderer and exercise the per-page note path. It also owns the private
`DelegatedExtractionContext` it uses to run the managed backend against the same sink with a
rendering-suppressed options clone.

### Key Methods

- **`ExtractAsync(DocumentSource, IExtractionContext)`** — buffers the source once; delegates the
  managed aspects to a `PdfDocumentExtractor` with `RenderPages` forced off; records the
  `pages.renderer` environment fact; and, only when `options.RenderPages` is `true`, rasterizes the
  requested pages, isolating each page's faults. The guard matters because selection may hand this
  backend a non-rendering request when it is the only registered candidate for `.pdf` (a host that
  calls `AddPdfRendering()` without the managed backend), and rasterization must not run uninvited
  in that case. Returns `Produced` unless the delegated managed extractor reports `Unreadable`.
  Precondition: `source` and `context` non-null. Postcondition: when rendering was requested, every
  selected page is either written or named in a plain note; when it was not, no page is rasterized.
- **`ProbeAvailability()`** — unconditionally returns `ExtractorAvailability.Available(
  providesRenderedPages: true)`, since CanvasNet.Pdf is a fully-managed dependency resolved at
  restore time with nothing to probe for at run time. Never rasterizes.
- **Descriptor members (`Id`, `DisplayName`, `SupportedFormats`, and `Priority`)** — expose the
  stable identity selection and reporting use when this backend participates in an extraction.
- **`GetSelfTestCases()`** — contributes one case, `pdf-rendering.renderRoundTrip`, named distinctly
  from the managed backend's `pdf.parseRoundTrip` and `pdf.pageRendering` cases so existing
  case-name assertions stay valid.

### Error Handling

If the delegated managed extraction reports `Unreadable`, this unit returns `Unreadable` unchanged
and does not attempt page rendering. When page rendering was requested but no page was selected to
render — an empty document, or a page range matching nothing — this unit reports that outcome
itself, as the single note "Page rendering was requested and a renderer was available, but no pages
were produced.", rather than relying on Core to infer it: Core cannot tell this unit's own
explanation apart from an unrelated note the delegated managed backend may already have recorded
during its earlier content phase, so this unit records the note from its own selected-page count
instead. During rendering, cancellation propagates; every other per-page rasterization fault is
caught in place, reported as `Page N could not be rasterized.`, and the run continues. No render
fault reaches the caller as an exception.

### Dependencies

- **DemaConsulting.DocDown.Core** — the extractor contract, the sink, the options, and the self-test types.
- **DemaConsulting.DocDown.Pdf** — `PdfDocumentExtractor`, delegated to for the managed aspects; and PdfPig
  (transitively) for counting pages to honor a page range.
- **PageRenderer** — the rasterization seam. See *PageRenderer Design*.

### Callers

The engine, once per extraction it selects this backend for. The registration seam constructs it via
a factory.
