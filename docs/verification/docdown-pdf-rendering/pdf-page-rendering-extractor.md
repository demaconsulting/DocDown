## PdfPageRenderingExtractor Verification Design

This document describes the unit-level verification strategy for `PdfPageRenderingExtractor`, the
superset backend that delegates the managed aspects and rasterizes the requested pages.

### Verification Approach

`PdfPageRenderingExtractor` is verified through unit tests in `PdfPageRenderingExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, with method names beginning with
`PdfPageRenderingExtractor_`.

The delegation and fault-isolation scenarios run through the real `DocDownEngine`, so the backend is
exercised exactly as production selects and invokes it. The per-page fault path is exercised by
injecting, through the internal constructor, a rasterization function that throws — the only reliable
way to drive that path without depending on the native renderer failing on cue. The descriptor and
probe are asserted directly on a constructed instance.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Native stack**: deployed in the test host, so the delegation and self-test scenarios rasterize
  for real; the fault scenario substitutes a throwing renderer
- **Filesystem**: a per-test `TempScratch` folder holds input and output
- **Mocking**: none, except the injected throwing render function for the fault scenario
- **Isolation**: each test owns its temporary folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfPageRenderingExtractor` unit test run passes when the backend declares the
four capabilities; when its probe reports available without throwing where the native stack is
deployed; when a render delegates the managed aspects and writes a valid page PNG; when a per-page
render fault becomes a counted gap without an exception reaching the caller; and when it contributes a
distinctly named, passing render round-trip case.

### Test Scenarios

#### The descriptor declares the full superset

**Test**: `PdfPageRenderingExtractor_Descriptor_DeclaresFullSupersetCapabilities`

Proves the identifier, PDF format, priority, and the four capabilities. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-DeclaresCapabilities`.

#### The probe is cheap, safe, and available here

**Test**: `PdfPageRenderingExtractor_ProbeAvailability_DeployedStack_ReportsAvailableWithoutThrowing`

Proves the probe does not throw and reports the full capability set where the native stack is
deployed. Evidence for `DocDownPdfRendering-PdfPageRenderingExtractor-ProbeIsCheapAndSafe`.

#### The managed aspects are delegated and pages are written

**Test**: `PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend writes `content.md` from the delegated managed backend and a valid page PNG from
its own rasterization, in one run. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-DelegatesManagedAspects` and
`DocDownPdfRendering-PdfPageRenderingExtractor-WritesRenderedPages` (the latter also evidenced by the
system scenario `DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`).

#### A per-page fault becomes a counted gap without throwing

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsCountedGapWithoutThrowing`

Proves an injected rasterization fault degrades the run with `PDFR0001` and a counted gap of affected
count one, produces no page, and does not throw. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-IsolatesPageFailures`.

#### A distinctly named self-test case passes here

**Test**: `PdfPageRenderingExtractor_GetSelfTestCases_DeployedStack_ContributesPassingRenderCase`

Proves the backend contributes exactly one case, named `pdf-rendering.renderRoundTrip` in category
`pdf-rendering`, that passes where the native stack is deployed. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ContributesSelfTests`.
