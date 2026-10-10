## CanvasNet.Pptx Verification

This document provides the verification evidence for the CanvasNet.Pptx OTS software item.
Requirements for this OTS item are defined in the CanvasNet.Pptx OTS Software Requirements document.

### Required Functionality

CanvasNet.Pptx is the managed rasterization API `DemaConsulting.DocDown.PowerPoint.Rendering` is built on. It must
rasterize a PowerPoint slide to an in-memory surface at a requested DPI through a fully managed API,
with no native asset and no runtime-identifier-specific resolution.

### Verification Approach

**CanvasNet.Pptx is verified by transitive evidence from the `DemaConsulting.DocDown.PowerPoint.Rendering` test
suite.** This is stated explicitly because it is a deliberate choice rather than an omission. Per the
software-items standard, a dedicated OTS test project is required only *if no other verification
evidence is available*. That is not the case here: unlike the repository's build-time tools,
CanvasNet.Pptx is a runtime library on the critical path of every rendered slide, and the tests named
below each drive its managed rasterization API end to end, on three target frameworks, across the
full CI operating-system matrix, since it carries no runtime-identifier-specific asset to limit where
it runs.

To avoid overclaiming, the evidence is the specific tests that actually call CanvasNet.Pptx's
rasterization path — no broader set is cited. Tests that only register the backend, assert the
descriptor, or inspect the public surface are not claimed here, because they execute none of
CanvasNet.Pptx's code. No `test/OtsSoftwareTests/` project is created, because it would re-test the
same library path the rendering suite already exercises against a real presentation.

### Test Environment

The evidence is produced by the standard `DemaConsulting.DocDown.PowerPoint.Rendering` test run: xUnit v3 under the
.NET SDK, targeting net8.0, net9.0, and net10.0, across the full CI operating-system matrix, since
CanvasNet.Pptx is fully managed and runs identically on every platform in the matrix.

### Test Scenarios

#### Managed rasterization of a slide at a requested DPI

**Tests**: `SlideRenderer_Render_SingleSlide_ReturnsValidPng`,
`PowerPointPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`,
`DocDownPowerPointRendering_Render_ProbePresentation_ProducesValidPngPages`

The first calls CanvasNet.Pptx's `PptxDocument.Render` directly through `SlideRenderer` and proves it
rasterizes a valid surface for a single slide, which `PngCodec.Save` then encodes to a valid PNG. The
second proves the same call path inside a real extraction writes a slide PNG. The third proves it end
to end through the engine, producing a valid PNG of plausible dimensions for the requested DPI.
Together they are direct evidence that CanvasNet.Pptx rasterizes a slide at a requested DPI through
its managed API, without DocDown naming any CanvasNet.Pptx type outside `SlideRenderer.cs`. Evidence
for `DocDown-OTS-CanvasNetPptx`.
