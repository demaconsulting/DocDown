## PdfPageRenderingExtractor Verification Design

This document describes the unit-level verification strategy for `PdfPageRenderingExtractor`, the
superset backend that delegates the managed aspects and rasterizes the requested pages.

### Verification Approach

`PdfPageRenderingExtractor` is verified through unit tests in `PdfPageRenderingExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, with method names beginning with
`PdfPageRenderingExtractor_`.

The descriptor and availability are asserted directly on a constructed instance, protecting the stable
public identity and the unconditional-availability contract. The delegation and per-page failure
scenarios run through the real `DocDownEngine`, so the backend is exercised exactly as production
selects and invokes it. The per-page failure path is exercised by injecting, through the internal
constructor, a rasterization function that throws — the only reliable way to drive that path without
depending on a genuine rasterization fault.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Pdf is fully managed, so the delegation and self-test scenarios rasterize
  for real on every host; the fault scenarios substitute a throwing renderer
- **Filesystem**: a per-test `TempScratch` folder holds input and output
- **Mocking**: none, except the injected throwing render/page-count functions for the fault scenarios
- **Isolation**: each test owns its temporary folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfPageRenderingExtractor` unit test run passes when the backend's public
identity stays stable; when it unconditionally reports rendered-page support without throwing; when a
render delegates the managed aspects and writes a valid page PNG; when rasterization is skipped
entirely when page rendering was not requested; when a per-page render fault or a page-count fault
each become a plain note without an exception reaching the caller; when page rendering is requested
but no page is selected to render, yielding the single note explaining that outcome itself; when a
null render function is rejected at construction; and when it contributes a distinctly named render
round-trip case that always passes.

### Test Scenarios

#### The descriptor stays stable

**Test**: `PdfPageRenderingExtractor_Descriptor_DeclaresStableIdentity`

Proves the identifier, display name, supported PDF format, and priority stay stable for selection and
reporting.

#### Availability is reported unconditionally, without throwing

**Test**: `PdfPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReportsUnconditionallyAvailable`

Proves the extractor always reports rendered-page support without throwing, since CanvasNet.Pdf
carries no environment-dependent failure mode. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ReportsUnconditionalAvailability`.

#### The managed aspects are delegated and pages are written

**Test**: `PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend writes `content.md` from the delegated managed backend and a valid page PNG from
its own rasterization, in one run. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-DelegatesManagedAspects` and
`DocDownPdfRendering-PdfPageRenderingExtractor-WritesRenderedPages` (the latter also evidenced by the
system scenario `DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`).

#### Rasterization is skipped when rendering was not requested

**Test**: `PdfPageRenderingExtractor_ExtractAsync_RenderNotRequested_WritesNoPages`

Proves that, even when this backend is the only one registered for `.pdf`, a plain (non-rendering)
request writes no `pages/` content and records no note, so selection handing this backend a
non-rendering request never costs a rasterization pass. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-SkipsRasterizationWhenNotRequested`.

#### A per-page fault becomes a note without throwing

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing`

Proves an injected rasterization fault records the plain note `Page 1 could not be rasterized.`,
produces no page, still returns `Produced`, and does not throw. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ReportsPageFailuresAsNotes`.

#### A page-count fault is reported without losing delegated content

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageCountFaults_ReportsNoteAndKeepsDelegatedContent`

Proves a fault reading the page count is recorded as a note while the delegated managed content is
still written, so one failed step does not discard output the delegate already produced.

#### An empty page selection is reported by this backend, not Core

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageRangeSelectsNoPage_ReportsEmptyPagesNote`

Proves a page range beyond the document's page count selects zero pages and this backend itself
records the single note "Page rendering was requested and a renderer was available, but no pages
were produced.", independent of any note the delegated managed backend may have already recorded.
Evidence for `DocDownPdfRendering-PdfPageRenderingExtractor-ReportsEmptySelectionAsNote`.

#### A null render function is rejected at construction

**Test**: `PdfPageRenderingExtractor_Construct_NullRenderFunction_Throws`

Proves a missing rasterization function is rejected where the mistake is made, at construction, rather
than surfacing later as a null-reference fault during extraction.

#### A distinctly named self-test case always passes

**Test**: `PdfPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_PassesRenderCase`

Proves the backend contributes exactly one case, named `pdf-rendering.renderRoundTrip` in category
`pdf-rendering`, that passes unconditionally since CanvasNet.Pdf carries no environment-dependent
failure mode. Evidence for `DocDownPdfRendering-PdfPageRenderingExtractor-ContributesSelfTests`.
