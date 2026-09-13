## PdfPageRenderingExtractor Verification Design

This document describes the unit-level verification strategy for `PdfPageRenderingExtractor`, the
superset backend that delegates the managed aspects and rasterizes the requested pages.

### Verification Approach

`PdfPageRenderingExtractor` is verified through unit tests in `PdfPageRenderingExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, with method names beginning with
`PdfPageRenderingExtractor_`.

The descriptor and probe are asserted directly on a constructed instance, protecting the stable public
identity and the selection-time availability contract. The delegation and per-page failure scenarios
run through the real `DocDownEngine`, so the backend is exercised exactly as production selects and
invokes it. The per-page failure path is exercised by injecting, through the internal constructor, a
rasterization function that throws — the only reliable way to drive that path without depending on
the native renderer failing on cue.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Native stack**: deployed in the test host, so the delegation and self-test scenarios rasterize
  for real; the fault scenario substitutes a throwing renderer
- **Filesystem**: a per-test `TempScratch` folder holds input and output
- **Mocking**: none, except the injected throwing render function for the fault scenario
- **Isolation**: each test owns its temporary folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfPageRenderingExtractor` unit test run passes when the backend's public
identity stays stable; when its probe reports rendered-page support honestly without throwing; when a
render delegates the managed aspects and writes a valid page PNG; when a per-page render fault
becomes the plain note `Page N could not be rasterized.` without an exception reaching the caller; and
when it contributes a distinctly named render round-trip case that reports pass or skip honestly for
the current environment.

### Test Scenarios

#### The descriptor stays stable

**Test**: `PdfPageRenderingExtractor_Descriptor_DeclaresStableIdentity`

Proves the identifier, display name, supported PDF format, and priority stay stable for selection and
reporting.

#### The probe is cheap, safe, and reports rendered-page support honestly

**Test**: `PdfPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReflectsNativeStackWithoutThrowing`

Proves the probe does not throw, reports rendered-page support when the native stack is usable, and
otherwise reports an unavailable reason. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ProbeIsCheapAndSafe` and
`DocDownPdfRendering-PdfPageRenderingExtractor-ReportsRenderedPagesAvailability`.

#### The managed aspects are delegated and pages are written

**Test**: `PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend writes `content.md` from the delegated managed backend and a valid page PNG from
its own rasterization, in one run. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-DelegatesManagedAspects` and
`DocDownPdfRendering-PdfPageRenderingExtractor-WritesRenderedPages` (the latter also evidenced by the
system scenario `DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`).

#### A per-page fault becomes a note without throwing

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing`

Proves an injected rasterization fault records the plain note `Page 1 could not be rasterized.`,
produces no page, still returns `Produced`, and does not throw. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ReportsPageFailuresAsNotes`.

#### A distinctly named self-test case reports pass or skip honestly

**Test**: `PdfPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_ReportsHonestRenderCaseStatus`

Proves the backend contributes exactly one case, named `pdf-rendering.renderRoundTrip` in category
`pdf-rendering`, that reports pass or skip honestly for the current environment. Evidence for
`DocDownPdfRendering-PdfPageRenderingExtractor-ContributesSelfTests`.
