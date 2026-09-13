## Detection Subsystem Verification Design

This document describes the verification strategy for the Detection subsystem.

### Verification Approach

Detection is verified through subsystem tests in `DetectionTests.cs`, driven against the real
`FormatSniffer` with in-memory streams. The subsystem has no injected collaborators and does not touch
the filesystem, so no mocking is required.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Inputs**: in-memory `MemoryStream` instances only
- **Isolation**: each test creates its own stream and file name

### Acceptance Criteria

Per IEC 62304 §5.5.2, Detection passes when it identifies known formats from the documented signals,
reports the basis for the decision, returns `Unknown` for unrecognized input rather than throwing, and
restores the source stream position after inspection.

### Test Scenarios

#### Format is identified from the extension or fallback signature

**Tests**: `Detection_FormatIdentification_PdfSignature_IdentifiesPdf`,
`Detection_FormatIdentification_HtmlSignature_IdentifiesHtml`,
`Detection_FormatIdentification_TextExtension_IdentifiesText`,
`Detection_FormatIdentification_DocxExtension_IdentifiesDocxByExtension`

Proves both the extension-primary and content-signature fallback paths.

#### Detection basis is reported

**Tests**: `Detection_DetectionBasisReported_PdfExtension_ReportsExtensionBasis`,
`Detection_DetectionBasisReported_PdfSignature_ReportsContentSignatureBasis`

Proves the subsystem records whether the result came from the extension or the signature.

#### Unrecognized input is reported as unknown

**Test**: `Detection_UnknownFormatReported_UnrecognizedContent_ReportsUnknown`

Proves Detection refuses to guess when no supported signal matches.

#### Inspection restores the stream position

**Test**: `Detection_NonDestructiveInspection_SeekableStream_RestoresStreamPosition`

Proves Detection is transparent to the later extractor that will read the same stream.
