### FormatSniffer Verification Design

This document describes the unit-level verification strategy for `FormatSniffer`, which identifies a
document's format from its file-name extension, uses the leading content bytes only as a fallback, and
reports the evidence behind the identification.

#### Verification Approach

`FormatSniffer` is verified in isolation through unit tests in `FormatSnifferTests.cs` under the
`Detection` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`FormatSniffer_`.

Nothing is mocked, and nothing needs to be: `FormatSniffer` has no collaborators at all. It is a static
class over a stream and a file name, so every test drives the real production code path directly with
in-memory `MemoryStream` content and no filesystem, network, or platform dependency. No test builds an
archive or container, because the unit no longer opens one.

The most important scenario is the deliberate conflict case: content that carries a strong `%PDF-`
signature behind a `.txt` file name. The unit must report `text`. That test is the executable statement
of the trust decision — if a future change re-introduced content sniffing ahead of the extension, it
would fail.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: in-memory streams only
- **Mocking**: none; the unit has no collaborators
- **Isolation**: each test constructs its own stream; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `FormatSniffer` unit test run passes when a recognized extension identifies the
format and outranks a contradicting content signature; when a file with no extension or an unrecognized
one falls back to a leading-byte signature; when unrecognizable input reports `Unknown`; when every
result carries the correct basis; when the stream position is restored after the
fallback path reads bytes and is untouched when the extension short-circuits; and when null or
non-seekable input is rejected with the documented exceptions. Any wrong classification, moved stream
position, or missing guard is a failure.

#### Test Scenarios

##### The file-name extension identifies the format

**Tests**: `FormatSniffer_Detect_ExtensionContradictsContent_TrustsExtension`,
`FormatSniffer_Detect_VsdxExtensionWithoutSignature_IdentifiesVsdxByExtension`,
`FormatSniffer_Detect_LegacyBinaryExtension_IdentifiesByExtension`,
`FormatSniffer_Detect_UpperCaseLogExtension_IdentifiesTextCaseInsensitively`

Proves the extension is the primary signal: it identifies a Visio document that carries no signature at
all, it identifies each legacy binary Office format (`.doc`, `.xls`, `.ppt`, `.vsd`) by name with the
correct format id and IANA media type, it matches case-insensitively, and it wins over a contradicting
`%PDF-` content signature. Evidence for `DocDownCore-Detection-FormatSniffer-ExtensionPrimary`.

##### A content signature is the fallback

**Tests**: `FormatSniffer_Detect_PdfSignatureWithoutFileName_IdentifiesPdfByContentSignature`,
`FormatSniffer_Detect_PdfSignatureWithUnknownExtension_FallsBackToContentSignature`,
`FormatSniffer_Detect_HtmlRootElement_IdentifiesHtmlByContentSignature`

Proves that when the name yields nothing — no name at all, or an extension the table does not know —
the sniffer recovers the format from a five-byte `%PDF-` header or an HTML root element. Evidence for
`DocDownCore-Detection-FormatSniffer-ContentSignature`.

##### Unrecognizable input reports unknown

**Tests**: `FormatSniffer_Detect_UnrecognizedContentNoFileName_ReportsUnknown`,
`FormatSniffer_Detect_EmptyStreamUnknownExtension_ReportsUnknown`

Proves the sniffer reports `Unknown` when neither the extension nor the content is recognized,
including for an empty stream. Evidence for `DocDownCore-Detection-FormatSniffer-UnknownFormat`.

##### The basis is reported

**Test**: `FormatSniffer_Detect_ExtensionMatch_ReportsExtensionBasis`

Proves an extension match reports the `Extension` basis, so the
record of *how* the format was determined reaches `manifest.json` intact. Evidence for
`DocDownCore-Detection-FormatSniffer-Basis`.

##### The stream position is preserved

**Tests**: `FormatSniffer_Detect_ContentFallbackFromOffset_RestoresStreamPosition`,
`FormatSniffer_Detect_ExtensionMatchFromOffset_LeavesStreamPositionUntouched`

Proves both remaining paths leave the caller's stream as they found it: the fallback path rewinds to
read the signature and then restores a non-zero entry position, and the extension path short-circuits
before reading anything, so the position is never disturbed. Evidence for
`DocDownCore-Detection-FormatSniffer-StreamPositionRestored`.

##### Invalid arguments are rejected

**Tests**: `FormatSniffer_Detect_NullContent_ThrowsArgumentNullException`,
`FormatSniffer_Detect_NonSeekableStream_ThrowsArgumentException`

Proves the unit rejects a null stream and a non-seekable stream with the precise exception types,
catching caller programming errors at entry. Both tests pass a recognized `document.pdf` name, so they
also prove the guards run *before* the extension short-circuit. These are defensive tests with no
linked requirement.
