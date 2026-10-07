## CanvasNet Verification

This document provides the verification evidence for the CanvasNet OTS software item. Requirements
for this OTS item are defined in the CanvasNet OTS Software Requirements document.

### Required Functionality

CanvasNet is the managed canvas and codec library both `DocDown.Pdf.Rendering` and
`DocDown.PowerPoint.Rendering` use to encode a rasterized page or slide surface as PNG through its
`PngCodec.Save` API.

### Verification Approach

**CanvasNet is verified by transitive evidence from the `DocDown.Pdf.Rendering` and
`DocDown.PowerPoint.Rendering` test suites.** This is stated explicitly because it is a deliberate
choice rather than an omission. Per the software-items standard, a dedicated OTS test project is
required only *if no other verification evidence is available*. That is not the case here: CanvasNet
is a runtime library on the critical path of every rendered page and every rendered slide, and the
tests named below each drive its PNG encoding end to end, on three target frameworks, across the
full CI operating-system matrix, since CanvasNet carries no runtime-identifier-specific asset to
limit where it runs.

To avoid overclaiming, the evidence is the specific tests that actually produce a PNG through
`PngCodec.Save` — no broader set is cited. Tests that only register a backend, assert a descriptor,
or inspect a public surface are not claimed here, because they execute none of CanvasNet's code. No
`test/OtsSoftwareTests/` project is created, because it would re-test the same library path the
rendering suites already exercise against real documents.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf.Rendering` and `DocDown.PowerPoint.Rendering`
test runs: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0, across the full CI
operating-system matrix. Every PDF fixture is generated at test time; the PowerPoint fixture is the
embedded `probe.pptx` resource. Neither depends on network access.

### Test Scenarios

#### Managed PNG encoding of a rasterized page

**Tests**: `PageRenderer_Render_SinglePage_ReturnsValidPng`,
`PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`,
`DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`

The first calls CanvasNet's `PngCodec.Save` directly through `PageRenderer` and proves it writes a
valid PNG for a single rasterized page. The second proves the same call path inside a real extraction
writes a page PNG. The third proves it end to end through the engine, producing a valid PNG of
plausible dimensions for the requested DPI. Together they are direct evidence that CanvasNet encodes a
rasterized surface as a real, decodable PNG. Evidence for `DocDown-OTS-CanvasNet`.

#### Managed PNG encoding of a rasterized slide

**Tests**: `SlideRenderer_Render_SingleSlide_ReturnsValidPng`,
`PowerPointPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`,
`DocDownPowerPointRendering_Render_ProbePresentation_ProducesValidPngPages`

The first calls CanvasNet's `PngCodec.Save` directly through `SlideRenderer` and proves it writes a
valid PNG for a single rasterized slide. The second proves the same call path inside a real
extraction writes a slide PNG. The third proves it end to end through the engine, producing a valid
PNG of plausible dimensions for the requested DPI. Together they are direct evidence that CanvasNet
encodes a rasterized slide surface as a real, decodable PNG, on the same codec path as the PDF
backend. Evidence for `DocDown-OTS-CanvasNet`.
