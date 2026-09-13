## PdfDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `PdfDocDownBuilderExtensions`, the
registration seam that adds the PDF backend to a builder.

### Verification Approach

`PdfDocDownBuilderExtensions` is verified through unit tests in
`PdfDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Pdf.Tests`, with method names
beginning with `PdfDocDownBuilderExtensions_`.

Nothing is mocked. A real builder is used and real engines are built from it because the observable
behavior under test is exactly what a host consumes: the backend descriptor list and the chaining
surface.

The reflection-free property is verified structurally rather than by observing an absence. The tests
inspect the extension method signature itself and confirm it takes and returns only the builder.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none; no document is extracted and no file is read
- **Filesystem**: none
- **Mocking**: none; a real builder and real engines are used
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 Section 5.5.2, a `PdfDocDownBuilderExtensions` unit test run passes when `AddPdf`
registers exactly one PDF extractor factory; when the resulting engine descriptor reports the stable
PDF identity, supported format, priority, and page-rendering applicability surface; when the same
builder instance is returned for chaining; when separate engines each receive their own extractor
instance; when the registration seam mentions no parser type; and when a missing builder is rejected
at the call. More than one registered backend, a different builder returned, or any reflection-based
loading surface is a failure.

### Test Scenarios

#### The call registers exactly one PDF backend

**Test**: `PdfDocDownBuilderExtensions_AddPdf_EmptyBuilder_RegistersTheSinglePdfExtractor`

Proves a builder with one registration produces one PDF extractor descriptor with the documented
identity and format surface. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-RegistersSupportedPdfExtractor`.

#### The builder is returned for chaining

**Test**: `PdfDocDownBuilderExtensions_AddPdf_AnyBuilder_ReturnsTheSameBuilderForChaining`

Proves the same builder instance is returned. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### Each engine receives its own backend

**Test**: `PdfDocDownBuilderExtensions_AddPdf_TwoEngines_EachReceivesItsOwnExtractor`

Proves the registration stores a factory rather than a singleton instance. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-ReflectionFree`.

#### The registration surface uses no reflection

**Test**: `PdfDocDownBuilderExtensions_AddPdf_RegistrationSurface_UsesNoReflection`

Proves the extension method takes and returns only the builder and exposes no PdfPig type. Evidence
for `DocDownPdf-PdfDocDownBuilderExtensions-ReflectionFree`.

#### A missing builder is rejected at the call

**Test**: `PdfDocDownBuilderExtensions_AddPdf_NullBuilder_ThrowsArgumentNullException`

Proves the builder argument is mandatory. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-RejectsNullBuilder`.
