## CanvasNet.Pdf Verification

This document provides the verification evidence for the CanvasNet.Pdf OTS software item.
Requirements for this OTS item are defined in the CanvasNet.Pdf OTS Software Requirements document.

### Required Functionality

CanvasNet.Pdf is the managed rasterization API `DocDown.Pdf.Rendering` is built on. It must rasterize
a PDF page to an in-memory surface at a requested DPI through a fully managed API, with no native
asset and no runtime-identifier-specific resolution.

### Verification Approach

**CanvasNet.Pdf is verified by transitive evidence from the `DocDown.Pdf.Rendering` test suite.** This
is stated explicitly because it is a deliberate choice rather than an omission. Per the
software-items standard, a dedicated OTS test project is required only *if no other verification
evidence is available*. That is not the case here: unlike the repository's build-time tools,
CanvasNet.Pdf is a runtime library on the critical path of every rendered page, and the tests named
below each drive its managed rasterization API end to end, on three target frameworks, across the
full CI operating-system matrix, since it carries no runtime-identifier-specific asset to limit where
it runs.

To avoid overclaiming, the evidence is the specific tests that actually call CanvasNet.Pdf's
rasterization path — no broader set is cited. Tests that only register the backend, assert the
descriptor, or inspect the public surface are not claimed here, because they execute none of
CanvasNet.Pdf's code. No `test/OtsSoftwareTests/` project is created, because it would re-test the
same library path the rendering suite already exercises against real documents.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf.Rendering` test run: xUnit v3 under the .NET
SDK, targeting net8.0, net9.0, and net10.0, across the full CI operating-system matrix, since
CanvasNet.Pdf is fully managed and runs identically on every platform in the matrix.

### Test Scenarios

#### Managed rasterization of a page at a requested DPI

**Tests**: `PageRenderer_Render_SinglePage_ReturnsValidPng`,
`PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`,
`DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`

The first calls CanvasNet.Pdf's `PdfDocument.Render` directly through `PageRenderer` and proves it
rasterizes a valid surface for a single page, which `PngCodec.Save` then encodes to a valid PNG. The
second proves the same call path inside a real extraction writes a page PNG. The third proves it end
to end through the engine, producing a valid PNG of plausible dimensions for the requested DPI.
Together they are direct evidence that CanvasNet.Pdf rasterizes a page at a requested DPI through its
managed API, without DocDown naming any CanvasNet.Pdf type outside `PageRenderer.cs`. Evidence for
`DocDown-OTS-CanvasNetPdf`.
