## VisioPageRenderingExtractor Verification Design

This document describes the unit-level verification strategy for `VisioPageRenderingExtractor`,
the superset backend that delegates the managed aspects and rasterizes the requested pages.

### Verification Approach

`VisioPageRenderingExtractor` is verified through unit tests in
`VisioPageRenderingExtractorTests.cs` in `DemaConsulting.DocDown.Visio.Rendering.Tests`, with
method names beginning with `VisioPageRenderingExtractor_`.

The descriptor and availability are asserted directly on a constructed instance, protecting the
stable public identity and the unconditional-availability contract. The delegation and per-page
failure scenarios run through the real `DocDownEngine`, so the backend is exercised exactly as
production selects and invokes it. The per-page failure and page-count failure paths are each
exercised by injecting, through the internal constructors, a rasterization or page-count function
that throws — the only reliable way to drive those paths without depending on a genuine
CanvasNet.Vsdx fault.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Vsdx is fully managed, so the delegation and self-test scenarios
  rasterize for real on every host; the fault scenarios substitute a throwing delegate
- **Filesystem**: a per-test `TempScratch` folder holds input and output
- **Mocking**: none, except the injected throwing render/page-count functions for the fault
  scenarios
- **Isolation**: each test owns its temporary folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioPageRenderingExtractor` unit test run passes when the backend's
public identity stays stable; when it unconditionally reports rendered-page support without
throwing; when a render delegates the managed aspects and writes a valid page PNG; when
rasterization is skipped and no note is recorded for a plain (non-rendering) request even as the
sole registered backend; when a per-page render fault or a page-count fault each become a plain
note without an exception reaching the caller; when page rendering is requested but no page is
selected to render, yielding the single note explaining that outcome itself; when a null render
function is rejected at construction; and when it contributes a distinctly named render
round-trip case that always passes.

### Test Scenarios

#### The descriptor stays stable

**Test**: `VisioPageRenderingExtractor_Descriptor_DeclaresStableIdentity`

Proves the identifier (`visio-rendering`), display name, supported VSDX/VSDM formats, and priority
(`5`) stay stable for selection and reporting.

#### Availability is reported unconditionally, without throwing

**Test**: `VisioPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReportsUnconditionallyAvailable`

Proves the extractor always reports rendered-page support without throwing, since CanvasNet.Vsdx
carries no environment-dependent failure mode. Evidence for
`DocDownVisioRendering-VisioPageRenderingExtractor-ReportsUnconditionalAvailability`.

#### The managed aspects are delegated and a page is written

**Test**: `VisioPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePng`

Proves the backend writes the delegated managed content (`content.md`, images, metadata) from
`VisioOpenXmlExtractor` and a valid page PNG from its own rasterization, in one run. Evidence for
`DocDownVisioRendering-VisioPageRenderingExtractor-DelegatesManagedAspects` and
`DocDownVisioRendering-VisioPageRenderingExtractor-WritesRenderedPages` (the latter also evidenced
by the system scenario `DocDownVisioRendering_Render_ProbeDrawing_ProducesValidPngPages`).

#### Rasterization is skipped when rendering was not requested

**Test**: `VisioPageRenderingExtractor_ExtractAsync_RenderNotRequested_WritesNoPages`

Proves that, even when this backend is the only one registered for `.vsdx`/`.vsdm`, a plain
(non-rendering) request writes no `pages/` content and records no note, so selection handing this
backend a non-rendering request never costs a rasterization pass. Evidence for
`DocDownVisioRendering-VisioPageRenderingExtractor-SkipsRasterizationWhenNotRequested`.

#### A per-page fault becomes a note without throwing

**Test**: `VisioPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing`

Proves an injected rasterization fault records the plain note `Page N could not be rasterized.` for
each affected page, produces no image for that page, still returns `Produced`, and does not throw.
Evidence for `DocDownVisioRendering-VisioPageRenderingExtractor-ReportsPageFailuresAsNotes`.

#### A page-count fault is reported without losing delegated content

**Test**: `VisioPageRenderingExtractor_ExtractAsync_PageCountFaults_ReportsNoteAndKeepsDelegatedContent`

Proves a fault reading the page count is recorded as the single note
`Pages could not be counted, so no page images were rendered.` while the delegated managed content
is still written, so one failed step does not discard output the delegate already produced. Evidence
for `DocDownVisioRendering-VisioPageRenderingExtractor-ReportsPageCountFailureAsNote`.

#### An empty page selection is reported by this backend, not Core

**Test**: `VisioPageRenderingExtractor_ExtractAsync_PageRangeSelectsNoPage_ReportsEmptyPagesNote`

Proves a page range beyond the drawing's page count selects zero pages and this backend itself
records the single note "Page rendering was requested and a renderer was available, but no pages
were produced.", independent of any note the delegated managed backend may have already recorded.
Evidence for `DocDownVisioRendering-VisioPageRenderingExtractor-ReportsEmptySelectionAsNote`.

#### A null render function is rejected at construction

**Test**: `VisioPageRenderingExtractor_Construct_NullRenderFunction_Throws`

Proves a missing rasterization function is rejected where the mistake is made, at construction,
rather than surfacing later as a null-reference fault during extraction.

#### A distinctly named self-test case always passes

**Test**: `VisioPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_PassesRenderCase`

Proves the backend contributes exactly one case, named `visio-rendering.renderRoundTrip` in
category `visio-rendering`, that passes unconditionally since CanvasNet.Vsdx carries no
environment-dependent failure mode. Evidence for
`DocDownVisioRendering-VisioPageRenderingExtractor-ContributesSelfTests`.
