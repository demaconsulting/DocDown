## SlideRenderer Verification Design

This document describes the unit-level verification strategy for `SlideRenderer`, the single
rasterization seam over CanvasNet.Pptx.

### Verification Approach

`SlideRenderer` is verified through unit tests in `SlideRendererTests.cs` in
`DemaConsulting.DocDown.PowerPoint.Rendering.Tests`, with method names beginning with
`SlideRenderer_`.

These tests exercise the real rasterizer and PNG encode through CanvasNet.Pptx and CanvasNet, so they
are also the transitive verification evidence for those OTS items. Thread-safety is verified
observably: several renders driven in parallel must all succeed, which is expected since each call
opens its own document with no shared mutable state in this unit.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Pptx, CanvasNet.Charts, and CanvasNet are fully managed and carry no
  runtime-identifier-specific asset, so the render scenarios exercise the real rasterizer on every
  host
- **Filesystem**: none; the embedded `probe.pptx` resource is loaded into memory and rendered to
  in-memory PNG bytes
- **Mocking**: none
- **Isolation**: stateless; the static seam is exercised directly

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `SlideRenderer` unit test run passes when a single slide rasterizes to a
valid PNG of positive dimensions; when a higher DPI produces a strictly larger image; when the
reported slide count of the probe presentation matches its real slide count; when several parallel
renders all produce valid PNGs; and when a null document is rejected at the call, for both rendering
and slide counting.

### Test Scenarios

#### A single slide rasterizes to a valid PNG

**Test**: `SlideRenderer_Render_SingleSlide_ReturnsValidPng`

Proves the real CanvasNet.Pptx raster and CanvasNet PNG encode produce a valid PNG of positive
dimensions. Evidence for `DocDownPowerPointRendering-SlideRenderer-RendersSlideToPng`.

#### A higher DPI produces a larger image

**Test**: `SlideRenderer_Render_HigherDpi_ProducesLargerImage`

Proves the requested DPI genuinely scales the raster: a higher-DPI render of the same slide is
strictly larger in both dimensions than a lower-DPI render. Evidence for
`DocDownPowerPointRendering-SlideRenderer-RendersSlideToPng`.

#### The probe presentation reports its real slide count

**Test**: `SlideRenderer_GetSlideCount_ProbePresentation_ReturnsSlideCount`

Proves the slide count CanvasNet.Pptx reports for the embedded probe presentation matches its known
slide count. Evidence for `DocDownPowerPointRendering-SlideRenderer-GetsSlideCount`.

#### Concurrent renders all succeed

**Test**: `SlideRenderer_Render_ConcurrentCalls_AllProduceValidPng`

Proves several renders driven in parallel all produce valid PNGs, the observable proof that opening
an independent `PptxDocument` per call, with no shared mutable state in this unit, lets concurrent
callers succeed without corrupting one another's output. Evidence for
`DocDownPowerPointRendering-SlideRenderer-ConcurrentCallsDoNotInterfere`.

#### A null document is rejected at the call

**Tests**: `SlideRenderer_Render_NullPptx_Throws`, `SlideRenderer_GetSlideCount_NullPptx_Throws`

Prove a null document is rejected up front, for both rendering and slide counting, rather than
carried into the rasterization call. Evidence for
`DocDownPowerPointRendering-SlideRenderer-RejectsNullDocument`.
