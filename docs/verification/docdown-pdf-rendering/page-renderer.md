## PageRenderer Verification Design

This document describes the unit-level verification strategy for `PageRenderer`, the single
rasterization seam over CanvasNet.Pdf.

### Verification Approach

`PageRenderer` is verified through unit tests in `PageRendererTests.cs` in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, with method names beginning with `PageRenderer_`.

These tests exercise the real rasterizer and PNG encode through CanvasNet.Pdf and CanvasNet, so they
are also the transitive verification evidence for those OTS items. Thread-safety is verified
observably: several renders driven in parallel from independent documents must all succeed, which is
expected since each call opens its own document with no shared mutable state in this unit.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Pdf and CanvasNet are fully managed and carry no runtime-identifier-
  specific asset, so the render scenarios exercise the real rasterizer on every host
- **Filesystem**: none; documents are generated in memory and rendered to in-memory PNG bytes
- **Mocking**: none
- **Isolation**: stateless; the static seam is exercised directly

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PageRenderer` unit test run passes when a single page rasterizes to a valid
PNG of positive, portrait dimensions; when the reported page count of a multi-page document matches
its real page count; when several parallel renders all produce valid PNGs; and when a null document
is rejected at the call, for both rendering and page counting.

### Test Scenarios

#### A single page rasterizes to a valid PNG

**Test**: `PageRenderer_Render_SinglePage_ReturnsValidPng`

Proves the real CanvasNet.Pdf raster and CanvasNet PNG encode produce a valid PNG of positive,
portrait dimensions. Evidence for `DocDownPdfRendering-PageRenderer-RendersPageToPng`.

#### A multi-page document reports its real page count

**Test**: `PageRenderer_GetPageCount_MultiPageDocument_ReturnsPageCount`

Proves the page count CanvasNet.Pdf reports for a generated multi-page document matches the number of
pages actually written. Evidence for `DocDownPdfRendering-PageRenderer-GetsPageCount`.

#### Concurrent renders all succeed

**Test**: `PageRenderer_Render_ConcurrentCalls_AllProduceValidPng`

Proves several renders driven in parallel all produce valid PNGs, the observable proof that opening an
independent `PdfDocument` per call, with no shared mutable state in this unit, lets concurrent callers
succeed without corrupting one another's output. Evidence for
`DocDownPdfRendering-PageRenderer-ConcurrentCallsDoNotInterfere`.

#### A null document is rejected at the call

**Tests**: `PageRenderer_Render_NullPdf_Throws`, `PageRenderer_GetPageCount_NullPdf_Throws`

Prove a null document is rejected up front, for both rendering and page counting, rather than carried
into the rasterization call. Evidence for `DocDownPdfRendering-PageRenderer-RejectsNullDocument`.
