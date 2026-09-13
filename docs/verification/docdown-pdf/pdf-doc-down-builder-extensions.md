## PdfDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `PdfDocDownBuilderExtensions`, the
registration seam that adds the PDF backend to a builder.

### Verification Approach

`PdfDocDownBuilderExtensions` is verified through unit tests in
`PdfDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Pdf.Tests`, with method names
beginning with `PdfDocDownBuilderExtensions_`.

Nothing is mocked: a real builder is used and a real engine is built from it, because the observable
behavior under test is exactly what a host observes. The registration's effect is asserted through
the engine's own descriptor list rather than through any internal state, so the test verifies the
promise made to a host rather than an implementation detail.

The reflection-free property is verified **structurally** rather than by observing an absence. A test
that merely ran the call and saw it succeed would pass equally well against a scanning
implementation. Instead the extension method's own signature is inspected: it takes only the builder
and returns only the builder, so there is no type name, assembly name, or path for a scanning
implementation to resolve, and the absence of any parser type on the seam is asserted at the same
time.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none; no document is extracted and no file is read
- **Filesystem**: none
- **Mocking**: none; a real builder and real engines are used
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfDocDownBuilderExtensions` unit test run passes when the call registers
exactly one extractor, identified as the PDF backend and declaring the PDF format and the supported
capabilities; when it returns the same builder instance it was called on; when two engines built from
one registration each receive a working backend; when the registration surface takes and returns only
the builder and mentions no parser type; and when a missing builder is rejected at the call. More
than one registered backend, a different builder instance returned, or a deferred null check is a
failure.

### Test Scenarios

#### The call registers exactly one PDF backend

**Test**: `PdfDocDownBuilderExtensions_AddPdf_EmptyBuilder_RegistersTheSinglePdfExtractor`

Proves an engine built from a builder with one registration carries exactly one backend, identified
as the PDF one, declaring the PDF format and the three supported capabilities selection ranks on.
Evidence for `DocDownPdf-PdfDocDownBuilderExtensions-RegistersExtractor`.

#### The builder is returned for chaining

**Test**: `PdfDocDownBuilderExtensions_AddPdf_AnyBuilder_ReturnsTheSameBuilderForChaining`

Proves the same instance comes back, so a host registering several backends can express that as one
readable configuration sequence. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### Each engine receives its own backend

**Test**: `PdfDocDownBuilderExtensions_AddPdf_TwoEngines_EachReceivesItsOwnExtractor`

Proves registration is deferred to build time: two engines built from one registration each carry a
PDF backend that probes as available. This is what lets a host configure a builder it never builds
without paying for the backend. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-ReflectionFree`.

#### The registration surface uses no reflection

**Test**: `PdfDocDownBuilderExtensions_AddPdf_RegistrationSurface_UsesNoReflection`

Proves the property structurally. The extension method takes only the builder and returns only the
builder, so no type, assembly, or path is available for it to resolve; and no parser type appears on
the seam, so a host can reference it without the parser entering its own compilation. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-ReflectionFree`.

#### A missing builder is rejected at the call

**Test**: `PdfDocDownBuilderExtensions_AddPdf_NullBuilder_ThrowsArgumentNullException`

Proves the failure happens where the mistake was made, naming the offending call site rather than
surfacing later against configuration the host wrote correctly. Evidence for
`DocDownPdf-PdfDocDownBuilderExtensions-RejectsNullBuilder`.
