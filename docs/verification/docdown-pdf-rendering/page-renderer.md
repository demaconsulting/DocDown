## PageRenderer Verification Design

This document describes the unit-level verification strategy for `PageRenderer`, the single
native-interop seam.

### Verification Approach

`PageRenderer` is verified through unit tests in `PageRendererTests.cs` in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, with method names beginning with `PageRenderer_` (and
the companion `NativeProbeResult_` tests over its result value type).

These tests exercise the real PDFium raster and SkiaSharp PNG encode, so they are also the transitive
verification evidence for the PDFium and SkiaSharp OTS items. The serialization lock is verified
observably: several renders driven in parallel must all succeed, which they cannot do reliably if the
lock is absent. The unavailable-with-reason behavior is verified on the result value type, because a
missing native binary cannot be produced on a host where the stack is deployed.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Native stack**: deployed in the test host, so the render and probe scenarios exercise the real
  rasterizer
- **Filesystem**: none; documents are generated in memory and rendered to in-memory PNG bytes
- **Mocking**: none
- **Isolation**: stateless; the static seam is exercised directly

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PageRenderer` unit test run passes when a single page rasterizes to a valid
PNG of positive, portrait dimensions; when the probe reports available without throwing where the
native stack is deployed; when an unavailable result carries a non-empty reason and refuses an empty
one; when several parallel renders all produce valid PNGs; and when a null document is rejected at the
call.

### Test Scenarios

#### A single page rasterizes to a valid PNG

**Test**: `PageRenderer_Render_SinglePage_ReturnsValidPng`

Proves the real PDFium raster and SkiaSharp encode produce a valid PNG of positive, portrait
dimensions. Evidence for `DocDownPdfRendering-PageRenderer-RendersPageToPng`.

#### The probe reports available without throwing

**Test**: `PageRenderer_ProbeAvailability_DeployedNativeStack_ReportsAvailableWithoutThrowing`

Proves the probe does not throw and reports available (with no reason) where the native stack is
deployed. Evidence for `DocDownPdfRendering-PageRenderer-ProbeCheapAndNonThrowing`.

#### An unavailable result carries a reason

**Tests**: `NativeProbeResult_Unavailable_CarriesReason`, `NativeProbeResult_Unavailable_EmptyReason_Throws`

Prove an unavailable probe result carries the reason it was constructed with and refuses an empty one,
which is the mechanism by which a missing native binary is reported with a displayable cause. Evidence
for `DocDownPdfRendering-PageRenderer-ReportsUnavailableWithReason`.

#### Concurrent renders all succeed

**Test**: `PageRenderer_Render_ConcurrentCalls_AllProduceValidPng`

Proves several renders driven in parallel all produce valid PNGs, the observable proof of the
process-wide serialization lock that PDFium's thread-unsafety requires. Evidence for
`DocDownPdfRendering-PageRenderer-SerializesNativeCalls`.

#### A null document is rejected at the call

**Test**: `PageRenderer_Render_NullPdf_Throws`

Proves a null document is rejected up front rather than carried into the native call. Evidence for
`DocDownPdfRendering-PageRenderer-RejectsNullDocument`.
