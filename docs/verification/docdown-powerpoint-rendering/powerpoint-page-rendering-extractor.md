## PowerPointPageRenderingExtractor Verification Design

This document describes the unit-level verification strategy for `PowerPointPageRenderingExtractor`,
the superset backend that delegates the managed aspects and rasterizes the requested slides.

### Verification Approach

`PowerPointPageRenderingExtractor` is verified through unit tests in
`PowerPointPageRenderingExtractorTests.cs` in `DemaConsulting.DocDown.PowerPoint.Rendering.Tests`,
with method names beginning with `PowerPointPageRenderingExtractor_`.

The descriptor and availability are asserted directly on a constructed instance, protecting the
stable public identity and the unconditional-availability contract. The delegation and per-slide
failure scenarios run through the real `DocDownEngine`, so the backend is exercised exactly as
production selects and invokes it. The per-slide failure and slide-count failure paths are each
exercised by injecting, through the internal constructors, a rasterization or slide-count function
that throws — the only reliable way to drive those paths without depending on a genuine
CanvasNet.Pptx fault.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Pptx is fully managed, so the delegation and self-test scenarios
  rasterize for real on every host; the fault scenarios substitute a throwing delegate
- **Filesystem**: a per-test `TempScratch` folder holds input and output
- **Mocking**: none, except the injected throwing render/slide-count functions for the fault
  scenarios
- **Isolation**: each test owns its temporary folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointPageRenderingExtractor` unit test run passes when the backend's
public identity stays stable; when it unconditionally reports rendered-page support without
throwing; when a render delegates the managed aspects and writes valid slide PNGs; when a per-slide
render fault or a slide-count fault each become a plain note without an exception reaching the
caller; when a null render function is rejected at construction; and when it contributes a
distinctly named render round-trip case that always passes.

### Test Scenarios

#### The descriptor stays stable

**Test**: `PowerPointPageRenderingExtractor_Descriptor_DeclaresStableIdentity`

Proves the identifier (`powerpoint-rendering`), display name, supported PPTX format, and priority
(`5`) stay stable for selection and reporting.

#### Availability is reported unconditionally, without throwing

**Test**: `PowerPointPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReportsUnconditionallyAvailable`

Proves the extractor always reports rendered-page support without throwing, since CanvasNet.Pptx
carries no environment-dependent failure mode. Evidence for
`DocDownPowerPointRendering-PowerPointPageRenderingExtractor-ReportsUnconditionalAvailability`.

#### The managed aspects are delegated and slides are written

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend writes the delegated managed content (`content.md`, images, metadata) from
`PowerPointOpenXmlExtractor` and a valid slide PNG for each slide from its own rasterization, in one
run. Evidence for `DocDownPowerPointRendering-PowerPointPageRenderingExtractor-DelegatesManagedAspects`
and `DocDownPowerPointRendering-PowerPointPageRenderingExtractor-WritesRenderedPages` (the latter also
evidenced by the system scenario
`DocDownPowerPointRendering_Render_ProbePresentation_ProducesValidPngPages`).

#### A per-slide fault becomes a note without throwing

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_SlideRenderFaults_ReportsNoteWithoutThrowing`

Proves an injected rasterization fault records the plain note `Slide N could not be rasterized.` for
each affected slide, produces no image for that slide, still returns `Produced`, and does not throw.
Evidence for `DocDownPowerPointRendering-PowerPointPageRenderingExtractor-ReportsSlideFailuresAsNotes`.

#### A slide-count fault is reported without losing delegated content

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_SlideCountFaults_ReportsNoteAndKeepsDelegatedContent`

Proves a fault reading the slide count is recorded as the single note
`Slides could not be counted, so no slide images were rendered.` while the delegated managed content
is still written, so one failed step does not discard output the delegate already produced. Evidence
for `DocDownPowerPointRendering-PowerPointPageRenderingExtractor-ReportsSlideCountFailureAsNote`.

#### A null render function is rejected at construction

**Test**: `PowerPointPageRenderingExtractor_Construct_NullRenderFunction_Throws`

Proves a missing rasterization function is rejected where the mistake is made, at construction,
rather than surfacing later as a null-reference fault during extraction.

#### A distinctly named self-test case always passes

**Test**: `PowerPointPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_PassesRenderCase`

Proves the backend contributes exactly one case, named `powerpoint-rendering.renderRoundTrip` in
category `powerpoint-rendering`, that passes unconditionally since CanvasNet.Pptx carries no
environment-dependent failure mode. Evidence for
`DocDownPowerPointRendering-PowerPointPageRenderingExtractor-ContributesSelfTests`.
