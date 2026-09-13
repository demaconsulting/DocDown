## PdfPageRenderingExtractor

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PdfPageRenderingExtractor` is the backend the engine selects and invokes when a PDF is detected and
page rendering is requested. Its single responsibility is to deliver the full DocDown output for a
PDF — text, embedded images, document metadata, and rendered page images — while carrying the native
rasterization cost only when rendering is actually asked for.

It exists because the engine selects exactly one backend. A rendering-only backend would lose
selection to the managed PDF backend and never render, so this unit declares the full superset of
capabilities and delivers all of them, obtaining the first three by delegation and adding the fourth
itself.

### Data Model

`PdfPageRenderingExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state. Its only field is the rasterization function it
drives, which defaults to `PageRenderer.Render`; an internal constructor accepts a substitute so a
test can inject a faulting renderer and exercise the per-page fault-isolation path. It owns the
internal `PdfRenderingDiagnosticCodes` (`PDFR0001` page-render-failed, `PDFR0002`
page-too-large/out-of-memory, `PDFR0003` native-fault-during-run) and the private
`DelegatedExtractionContext` it uses to run the managed backend against the same sink with a
rendering-suppressed options clone.

### Key Methods

- **`ExtractAsync(DocumentSource, IExtractionContext)`** — buffers the source once; delegates the
  managed aspects to a `PdfDocumentExtractor` with `RenderPages` forced off; records the
  `pages.renderer` environment fact; rasterizes the requested pages, isolating each page's faults;
  and returns degraded if the delegate degraded or any page failed. Precondition: `source` and
  `context` non-null. Postcondition: every requested page is either written or accounted for by a
  gap.
- **`ProbeAvailability()`** — asks `PageRenderer` whether the native stack can load, cheaply and
  without throwing, and maps the answer to an `ExtractorAvailability` carrying the full capabilities
  when available or a reason when not. Never rasterizes.
- **`Capabilities`** — declares `text | embeddedImages | documentMetadata | renderedPages`, the full
  superset selection must see for this backend to be chosen when rendering is requested.
- **`GetSelfTestCases()`** — contributes one case, `pdf-rendering.renderRoundTrip`, named distinctly
  from the managed backend's `pdf.parseRoundTrip` and `pdf.pageRendering` cases so existing
  case-name assertions stay valid.

### Error Handling

A parser fault from the delegated managed extraction (encrypted, malformed, truncated) propagates to
the engine, which converts it into a structured failure — this unit does not translate it. A per-page
rasterization fault is caught in place: cancellation propagates, an out-of-memory becomes `PDFR0002`,
a native-level fault becomes `PDFR0003`, and any other fault becomes `PDFR0001`; each failed page is
added to a single counted gap and the run continues. No render fault reaches the caller as an
exception.

### Dependencies

- **DocDown.Core** — the extractor contract, the sink, the options, and the self-test types.
- **DocDown.Pdf** — `PdfDocumentExtractor`, delegated to for the managed aspects; and PdfPig
  (transitively) for counting pages to honor a page range.
- **PageRenderer** — the native rasterization seam. See *PageRenderer Design*.

### Callers

The engine, once per extraction it selects this backend for. The registration seam constructs it via
a factory.
