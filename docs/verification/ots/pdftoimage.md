## PDFtoImage Verification

This document provides the verification evidence for the PDFtoImage OTS software item. Requirements
for this OTS item are defined in the PDFtoImage OTS Software Requirements document.

### Required Functionality

PDFtoImage is the managed rasterization API `DocDown.Pdf.Rendering` is built on. It must rasterize a
PDF page to an in-memory image at a requested DPI through a managed API, and serialize its native
calls because the underlying PDFium renderer is not thread-safe.

### Verification Approach

**PDFtoImage is verified by transitive evidence from the `DocDown.Pdf.Rendering` test suite.** This is
stated explicitly because it is a deliberate choice rather than an omission. Per the software-items
standard, a dedicated OTS test project is required only *if no other verification evidence is
available*. That is not the case here: unlike the repository's build-time tools, PDFtoImage is a
runtime library on the critical path of every rendered page, and the tests named below each drive its
managed rasterization API end to end, on three target frameworks, in every CI matrix combination where
the native stack is present.

To avoid overclaiming, the evidence is the specific tests that actually call PDFtoImage's rasterization
path — no broader set is cited. Tests that only register the backend, assert the descriptor, or inspect
the public surface are not claimed here, because they execute none of PDFtoImage's code. No
`test/OtsSoftwareTests/` project is created, because it would re-test the same library path the
rendering suite already exercises against real documents.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf.Rendering` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix where the PDFtoImage
native stack is deployed. Every fixture is a PDF generated at test time, so the evidence depends on no
committed binary and no network access.

### Test Scenarios

#### Managed rasterization of a page at a requested DPI

**Tests**: `PageRenderer_Render_SinglePage_ReturnsValidPng`,
`PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`,
`DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`

The first calls PDFtoImage's `Conversion.ToImage` directly and proves it returns a bitmap that encodes
to a valid PNG. The second proves the same call path inside a real extraction writes a page PNG. The
third proves it end to end through the engine, producing a valid PNG of plausible dimensions for the
requested DPI. Together they are direct evidence that PDFtoImage rasterizes a page at a requested DPI
through its managed API. Evidence for `DocDown-OTS-PDFtoImage`.
