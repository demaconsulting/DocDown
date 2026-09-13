## PDFium Verification

This document provides the verification evidence for the PDFium OTS software item. Requirements for
this OTS item are defined in the PDFium OTS Software Requirements document.

### Required Functionality

PDFium is the native page rasterizer, reached transitively through PDFtoImage. It must load for the
current runtime identifier and rasterize a PDF page to a non-blank buffer of plausible dimensions.

### Verification Approach

**PDFium is verified by transitive evidence from the `DocDown.Pdf.Rendering` test suite.** Per the
software-items standard, a dedicated OTS test project is required only *if no other verification
evidence is available*. That is not the case: PDFium is the native engine that produces the pixels of
every rendered page, and the tests named below load it for the current runtime identifier and exercise
its raster output directly. No `test/OtsSoftwareTests/` project is created, because it would re-test
the same native path the rendering suite already exercises.

The evidence is limited to the specific tests that load and exercise PDFium's rasterization — the
probe test that proves it loads for the current runtime identifier, and the render tests that prove it
produces a raster of plausible dimensions. No broader set is claimed.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf.Rendering` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix where the PDFium native
binary for that runtime identifier is deployed. Every fixture is generated at test time.

### Test Scenarios

#### The native binary loads and rasterizes a page

**Tests**: `PageRenderer_Render_SinglePage_ReturnsValidPng`,
`PageRenderer_ProbeAvailability_DeployedNativeStack_ReportsAvailableWithoutThrowing`,
`DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`

The probe test proves the PDFium native binary loads for the current runtime identifier without
throwing. The two render tests prove that, once loaded, it rasterizes a page to a buffer whose header
dimensions are plausible for the page and DPI — that is, a non-blank raster of the expected size.
Together they are direct evidence for both required properties. Evidence for `DocDown-OTS-PDFium`.
