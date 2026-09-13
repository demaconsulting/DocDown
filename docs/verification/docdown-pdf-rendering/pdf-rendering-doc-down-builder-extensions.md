## PdfRenderingDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for
`PdfRenderingDocDownBuilderExtensions`, the registration seam that adds the rendering backend to a
builder.

### Verification Approach

`PdfRenderingDocDownBuilderExtensions` is verified through unit tests in
`PdfRenderingDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Pdf.Rendering.Tests`.

Nothing is mocked: a real builder is used and a real engine is built from it, because the observable
behavior under test is exactly what a host observes. The no-native-type property is verified
**structurally** by reflecting over every exported type's public members and failing if any signature
type belongs to the PDFtoImage or SkiaSharp assembly — a leak hidden inside a collection is caught as
readily as a bare parameter.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none; no document is extracted and no file is read
- **Filesystem**: none
- **Mocking**: none; a real builder and real engines are used
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfRenderingDocDownBuilderExtensions` unit test run passes when the call
registers exactly one rendering extractor; when it returns the same builder instance; when a missing
builder is rejected at the call; and when no PDFtoImage, PDFium, or SkiaSharp type appears on any
public member of the package's exported types.

### Test Scenarios

#### The call registers exactly one rendering backend

**Test**: `AddPdfRendering_OnBuilder_RegistersRenderingExtractor`

Proves an engine built from a builder with one registration carries exactly the rendering backend.
Evidence for `DocDownPdfRendering-PdfRenderingDocDownBuilderExtensions-RegistersExtractor` and, because
the factory registration resolves no type by name, for
`DocDownPdfRendering-PdfRenderingDocDownBuilderExtensions-ReflectionFree`.

#### The builder is returned for chaining

**Test**: `AddPdfRendering_OnBuilder_ReturnsSameBuilderForChaining`

Proves the same instance comes back, so registration reads as `AddPdf().AddPdfRendering()`. Evidence
for `DocDownPdfRendering-PdfRenderingDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### A missing builder is rejected at the call

**Test**: `AddPdfRendering_NullBuilder_Throws`

Proves the failure happens where the mistake was made. Evidence for
`DocDownPdfRendering-PdfRenderingDocDownBuilderExtensions-RejectsNullBuilder`.

#### No native rasterizer type reaches the public surface

**Test**: `PublicApi_AllPublicMembers_ExposeNoNativeRendererTypes`

Proves, by reflection over every exported type's members, that no PDFtoImage or SkiaSharp type appears
on the public surface, so a host can reference the seam without those types entering its compilation.
Evidence for `DocDownPdfRendering-PdfRenderingDocDownBuilderExtensions-NoNativeTypeOnSurface`.
