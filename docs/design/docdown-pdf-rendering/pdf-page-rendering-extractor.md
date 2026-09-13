## PdfPageRenderingExtractor

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PdfPageRenderingExtractor` is the backend the engine selects and invokes when a PDF is detected and
page rendering is requested. Its single responsibility is to deliver the full DocDown output for a
PDF — text, embedded images, document metadata, and rendered page images — while carrying the native
rasterization cost only when rendering is actually asked for.

It exists because the engine selects exactly one backend. A rendering-only backend would leave the
managed PDF backend's work undone, so this unit must deliver the managed artifacts too, obtaining
them by delegation and adding rendered pages itself. It tells selection that rendered pages are
usable here only through `ProbeAvailability()`.

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
  `pages.renderer` environment fact; rasterizes the requested pages, isolating each page's faults;
  and returns `Produced` unless the delegated managed extractor reports `Unreadable`. Precondition:
  `source` and `context` non-null. Postcondition: every selected page is either written or named in
  a plain note.
- **`ProbeAvailability()`** — asks `PageRenderer` whether the native stack can load, cheaply and
  without throwing, and maps the answer to `ExtractorAvailability.Available(providesRenderedPages:
  true)` when rendering is usable here or `ExtractorAvailability.Unavailable(reason)` when it is not.
  Never rasterizes.
- **Descriptor members (`Id`, `DisplayName`, `SupportedFormats`, and `Priority`)** — expose the
  stable identity selection and reporting use when this backend participates in an extraction.
- **`GetSelfTestCases()`** — contributes one case, `pdf-rendering.renderRoundTrip`, named distinctly
  from the managed backend's `pdf.parseRoundTrip` and `pdf.pageRendering` cases so existing
  case-name assertions stay valid.

### Error Handling

If the delegated managed extraction reports `Unreadable`, this unit returns `Unreadable` unchanged
and does not attempt page rendering. The ordinary "renderer unavailable" path is handled before this
method runs, through `ProbeAvailability()`. During rendering, cancellation propagates; every other
per-page rasterization fault is caught in place, reported as `Page N could not be rasterized.`, and
the run continues. No render fault reaches the caller as an exception.

### Dependencies

- **DocDown.Core** — the extractor contract, the sink, the options, and the self-test types.
- **DocDown.Pdf** — `PdfDocumentExtractor`, delegated to for the managed aspects; and PdfPig
  (transitively) for counting pages to honor a page range.
- **PageRenderer** — the native rasterization seam. See *PageRenderer Design*.

### Callers

The engine, once per extraction it selects this backend for. The registration seam constructs it via
a factory.
