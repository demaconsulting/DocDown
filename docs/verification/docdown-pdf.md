# DocDown.Pdf Verification Design

This document describes the system-level verification strategy for `DocDown.Pdf`, the managed PDF
extraction package.

## Verification Approach

`DocDown.Pdf` is verified through system-level integration tests in `DocDownPdfTests.cs` and unit
tests per unit, all in `DemaConsulting.DocDown.Pdf.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### Every extraction scenario checks the persisted contract artifacts

Each end-to-end extraction scenario asserts the persisted output artifacts that matter for this
backend: `content.md`, `manifest.json`, `summary.txt`, extracted image files when present, and the
standard folder layout. The tests compare those artifacts directly rather than inferring behavior
from internal calls, which keeps the system evidence at the same surface a consumer reads.

### Inventory and notes replace judgment-oriented reporting

The suite now treats ordinary document absences as inventory questions, not as failure signals. A
zero-page PDF is expected to report zero counted features. A scanned page with no glyphs is expected
to produce image-backed output without an extraction note. Plain notes are reserved for attempted
steps that could not complete, such as an undecodable image or a forced PNG output mode that the
extractor could not honor.

### Test fixtures are generated; the self-test probe is committed

Every PDF the suite uses is built at test time, either through PdfPig's document writer or by
assembling bytes in code for adverse cases such as encrypted, malformed, JPEG 2000, and JBIG2
fixtures. No fixture is committed.
The one committed binary is the backend's self-test probe: a real document authored in the
application that produces the format, embedded in the package so the self-test reads what that
application emits.

### Text assertions are property-based, not golden

Reading-order recovery and paragraph segmentation are heuristic, so the text scenarios assert
properties of the emitted markdown rather than exact golden files. Byte-exact comparison is reserved
for the determinism scenario, where repeated runs are expected to be identical.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: each system test owns a `TempScratch` folder for the generated input PDF and the
  extraction output
- **Inputs**: PDFs generated at test time; no committed binaries and no network access
- **Mocking**: none for system tests; the real engine, real backend, and real parser are exercised
- **Determinism**: the run-varying timestamp line is normalized so repeated runs are byte-comparable
- **Isolation**: each test owns and disposes its own scratch folder

## Acceptance Criteria

Per IEC 62304 Section 5.7.2, a system-level `DocDown.Pdf` test run passes when:

- Normal extractions return `ExtractionOutcome.Produced`, parser faults return
  `ExtractionOutcome.Unreadable`, and no unexpected exception reaches the caller.
- Every extraction scenario writes the standard layout the consumer expects.
- The content document preserves page order, page boundaries, and inline image links where images
  were written.
- The manifest records truthful image media types and transform provenance for every written image.
- A zero-page document reports zero counted content features rather than an explanatory note.
- A scanned document with no glyphs still produces useful output and does not invent an extraction
  problem that did not occur.
- An undecodable image, a size-limited image, and an unhonored PNG request each produce a plain
  factual note naming what happened.
- A page-rendering request against the managed PDF backend produces no rendered pages and states the
  limitation plainly.
- Source-filtered platform requirements are evidenced by the matching CI source and do not accept a
  result from another platform or runtime.
- Two runs over the same document in the same environment produce byte-identical artifacts.

## Test Scenarios

### Contract layout is produced for a text PDF

**Test**: `DocDownPdf_Extract_SimpleTextPdf_ProducesContractLayout`

Proves a clean extraction: produced outcome, no failure, no notes, readable text in `content.md`,
and the standard artifact layout. This is also the anchor for the platform requirements. Evidence
for `DocDownPdf-Registration` and `DocDownPdf-TextContent`.

### Page count and all pages' text are reported

**Test**: `DocDownPdf_Extract_MultiPagePdf_ReportsPageCountAndAllText`

Proves the manifest records the real page count and the content document carries every page's text in
reading order. Evidence for `DocDownPdf-DocumentMetadata` and `DocDownPdf-TextContent`.

### A stored JPEG round-trips byte-identically as a passthrough

**Test**: `DocDownPdf_Extract_DctImage_RoundTripsByteIdenticalAsPassthrough`

Proves the written file is the exact JPEG embedded in the fixture and that the manifest records the
truthful media type and passthrough transform. Evidence for `DocDownPdf-EmbeddedImages` and
`DocDownPdf-ImageProvenance`.

### A compressed-sample image is labeled as decoded to PNG

**Test**: `DocDownPdf_Extract_FlateImage_IsLabeledDecodedToPng`

Proves a sample-backed image is written as PNG and labeled as a decode-and-re-encode rather than as
a passthrough. Evidence for `DocDownPdf-EmbeddedImages` and `DocDownPdf-ImageProvenance`.

### An undecodable image becomes a plain note naming the encoding

**Test**: `DocDownPdf_Extract_UndecodableImage_ReportsPlainNoteNamingEncoding`

Proves a mixed document still produces its decodable image, reports the undecodable image with a
one-sentence note naming the encoding and the `n of m` count, and records the same note in the
persisted artifacts. Evidence for `DocDownPdf-UndecodableImagesUseNotes`.

### A JPEG 2000 image is written as `.jp2` with no extra note

**Test**: `DocDownPdf_Extract_Jpeg2000Image_WritesJp2Passthrough`

Proves the JPEG 2000 codestream is written unchanged under an extension describing its bytes,
labeled as a passthrough, counted as extracted, and not accompanied by an explanatory note because
that extraction step completed successfully. Evidence for `DocDownPdf-EmbeddedImages` and
`DocDownPdf-ImageProvenance`.

### An embedded JPEG is written in the document's own encoding

**Test**: `DocDownPdf_Extract_JpegImage_WritesDocumentsOwnEncoding`

Proves the written image remains truthfully typed, is written in the encoding the document stored it
in, and needs no note because nothing was attempted that could not complete. Evidence for
`DocDownPdf-ImagesKeepSourceEncoding`.

### A size-limit skip is reported as its own note

**Test**: `DocDownPdf_Extract_ImageOverSizeLimit_ReportsSeparateSkipNote`

Proves a caller-supplied size limit produces a distinct note and does not masquerade as a decode
failure. Evidence for `DocDownPdf-ImageSizeLimitsUseNotes`.

### Disabled images are suppressed with a note and no dangling links

**Test**: `DocDownPdf_Extract_ImagesDisabled_SuppressesImagesWithNote`

Proves deliberate image suppression leaves no written images, records the suppression plainly, and
keeps `content.md` free of links to missing files. Evidence for `DocDownPdf-ImageSuppressionUsesNote`.

### A scanned PDF still produces image-backed output

**Test**: `DocDownPdf_Extract_ScannedPdfWithNoTextLayer_ProducesImageBackedOutput`

Proves a page with no glyphs still yields page markers and extracted images, omits body text that is
not present, and carries no extraction note because DocDown completed the steps it attempted.
Evidence for `DocDownPdf-ScannedPdfProducesImageBackedOutput`.

### A page-rendering request produces a plain note and no rendered pages

**Test**: `DocDownPdf_Extract_PagesRequested_ProducesPlainNoteAndNoRenderedPages`

Proves the managed PDF backend writes no rendered-page files and records the limitation with a plain
note rather than with any judgment about the document. Evidence for
`DocDownPdf-PageRenderingRequestUsesNote`.

### An encrypted document returns an unreadable result with the full layout written

**Test**: `DocDownPdf_Extract_EncryptedPdf_ReturnsUnreadableResultAndWritesFullLayout`

Proves an encrypted PDF becomes a structured unreadable result, carries the parser's explanation,
and still writes the standard layout. Evidence for `DocDownPdf-ProtectedDocument`.

### A malformed document returns an unreadable result with the full layout written

**Test**: `DocDownPdf_Extract_MalformedPdf_ReturnsUnreadableResultAndWritesFullLayout`

Proves a malformed PDF becomes a structured unreadable result with explanatory text and the standard
layout. Evidence for `DocDownPdf-MalformedDocument`.

### A page-less document reports zero-count inventory

**Test**: `DocDownPdf_Extract_ZeroPagePdf_ProducesZeroCountInventory`

Proves a valid zero-page PDF stays produced, carries no extraction note, and records zero counts for
pages, headings, paragraphs, and inline images. Evidence for
`DocDownPdf-EmptyDocumentReportsZeroCountInventory`.

### A requested page range restricts the extraction

**Test**: `DocDownPdf_Extract_PageRangeRequested_RestrictsToRange`

Proves only the requested page's content appears in `content.md`. Evidence for `DocDownPdf-PageRange`.

### Repeated extraction is byte-identical

**Test**: `DocDownPdf_Extract_SameDocumentTwice_ProducesByteIdenticalArtifacts`

Proves every artifact produced by one run matches the second run byte for byte. Evidence for
`DocDownPdf-Determinism`.

### Every outcome writes the standard layout

**Test**: `DocDownPdf_Extract_AnyOutcome_WritesStandardLayout`

Proves the standard layout exists across clean, note-bearing, zero-page, and unreadable scenarios.
This is supporting evidence for the system's persisted contract behavior.

### No PDF-parser type reaches the public API

**Test**: `DocDownPdf_PublicApi_AllPublicMembers_ExposeNoPdfPigTypes`

Proves PdfPig types do not leak onto the public API surface. Evidence for
`DocDownPdf-ContainedDependency`.

### The package ships no native assets

**Tests**: `DocDownPdf_Package_BuildOutput_ContainsNoNativeAssets` (xUnit),
`DocDownPdf_Package_ContainsNoNativeAssets` (FileAssert, `packages` tag)

Proves both the build output and the packed artifact remain managed-only. The build-output check is
a unit test because it reads the compiler's output, which the test run already produced; the package
check runs against the `.nupkg` the build produced, because packing is a build activity rather than
something a test should perform. Evidence for `DocDownPdf-ManagedOnly`.

### Self-validation cases are exposed and run

**Test**: `DocDownPdf_SelfValidation_RegisteredEngine_ReportsPdfCases`

Proves the backend contributes its parse round-trip case and its page-rendering skip case to the
registered engine. Evidence for `DocDownPdf-SelfValidation`.
