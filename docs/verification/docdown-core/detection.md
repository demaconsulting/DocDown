## Detection Subsystem Verification Design

This document describes the verification strategy for the Detection subsystem, which names a
document's format from its file-name extension — falling back to a leading-byte content signature —
and reports how it reached that answer.

### Verification Approach

Detection is verified through subsystem integration tests that exercise its single unit,
`FormatSniffer`, from the perspective of a caller who hands the subsystem a stream and a file name and
reads back a `FormatDetection`. Tests reside in `DetectionTests.cs` under the `Detection` folder of
`DemaConsulting.DocDown.Core.Tests`, and their method names begin with `Detection_`.

No mocking occurs at the subsystem boundary. The subsystem has no injected collaborators beyond the
.NET base class library and no filesystem or network dependency, so tests drive the real
`FormatSniffer` with in-memory `MemoryStream` inputs. No test constructs an archive or container of
any kind, because the subsystem no longer opens one: a `.docx` input is verified as *arbitrary bytes
named `report.docx`*, which is exactly what the extension-primary rule promises to handle. Isolated
unit behavior, including the argument guards and the confidence contract, is covered separately in
*FormatSniffer Verification Design*.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: in-memory streams only
- **Mocking**: none; the real unit runs over real streams
- **Isolation**: each test constructs its own stream; no shared state or filesystem access

### Acceptance Criteria

Per IEC 62304 §5.5.2, a Detection subsystem test run passes when every scenario below returns the
correct format, basis, and confidence for its input; when malformed or unrecognized input yields an
`Unknown` detection rather than an exception; and when the source stream's position is restored after
inspection. Any wrong classification, thrown exception on malformed input, or moved stream position is
a failure.

### Test Scenarios

#### Format is identified from the extension, with a content-signature fallback

**Tests**: `Detection_FormatIdentification_PdfSignature_IdentifiesPdf`,
`Detection_FormatIdentification_HtmlSignature_IdentifiesHtml`,
`Detection_FormatIdentification_TextExtension_IdentifiesText`,
`Detection_FormatIdentification_DocxExtension_IdentifiesDocxByExtension`

Proves the subsystem names a PDF and a text document from their extensions, names a Word document
from its `.docx` extension without opening it, and recovers an unnamed HTML document from its content
signature — covering both the primary and the fallback path. Evidence for
`DocDownCore-Detection-FormatIdentification`.

#### Detection basis is reported

**Tests**: `Detection_DetectionBasisReported_PdfExtension_ReportsExtensionBasis`,
`Detection_DetectionBasisReported_PdfSignature_ReportsContentSignatureBasis`

Proves the subsystem reports which signal identified the format — the extension when the name was
recognized, the content signature when it was not — so the evidence survives into `manifest.json`.
Evidence for `DocDownCore-Detection-DetectionBasisReported`.

#### Unrecognized content is reported as unknown

**Test**: `Detection_UnknownFormatReported_UnrecognizedContent_ReportsUnknown`

Proves the subsystem reports `Unknown` for content it cannot identify by either signal, letting the
pipeline fail cleanly rather than mis-routing the document. Evidence for
`DocDownCore-Detection-UnknownFormatReported`.

#### Inspection restores the stream position

**Test**: `Detection_NonDestructiveInspection_SeekableStream_RestoresStreamPosition`

Proves the subsystem restores the source stream to its original position after inspection, so the same
stream can be handed to an extractor afterwards. The test uses an unrecognized file name deliberately,
so the content-signature fallback actually reads bytes and the restoration is genuinely exercised
rather than trivially satisfied. Evidence for `DocDownCore-Detection-NonDestructiveInspection`.
