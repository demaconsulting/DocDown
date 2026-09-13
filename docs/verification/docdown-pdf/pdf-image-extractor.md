## PdfImageExtractor Verification Design

This document describes the unit-level verification strategy for `PdfImageExtractor`, which writes a
PDF's embedded images and records factual notes when an attempted image step cannot complete.

### Verification Approach

`PdfImageExtractor` is verified through unit tests in `PdfImageExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfImageExtractor_`.

The unit is driven directly against pages opened from generated fixtures, writing through the shared
`RecordingSink`. The sink retains the exact bytes and `ImageHint` values that reached it, which is
what makes this the right level to verify truthful media types, transform provenance, source-page
references, and note content.

The parser is not mocked. The behavior under test depends on real image dictionaries and real image
payloads, so the tests exercise the actual parser output the production code consumes.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: pages from PDFs generated at test time, including fixtures carrying JPEG 2000 and
  JBIG2 images
- **Filesystem**: none; all writes go to the recording sink
- **Mocking**: the sink is a test double; the parser is real
- **Isolation**: each test opens its own document and constructs its own sink

### Acceptance Criteria

Per IEC 62304 Section 5.5.2, a `PdfImageExtractor` unit test run passes when a stored JPEG is
handed to the sink unchanged and labeled as a passthrough; when a compressed-sample image is
handed over as PNG and labeled as decoded to PNG; when a JPEG 2000 codestream is written unchanged
as `image/jp2` with no extra note; when each written image records truthful provenance fields; when
an undecodable encoding produces a plain note naming the encoding and count; when size-based skips
produce their own note; when an unhonored PNG request produces a plain note naming the responsible
encodings and an honored request produces none; when disabled image extraction attempts nothing; and
when an image-free document returns zero counts without a note. Any mismatched media type,
misreported transform, silent image loss, or conflation of note categories is a failure.

### Test Scenarios

#### A stored JPEG is passed through unchanged and labeled so

**Test**: `PdfImageExtractor_AddImage_DctImage_SetsPassthroughTransformHint`

Proves the bytes handed to the sink are the exact embedded JPEG, typed as JPEG, and labeled as a
passthrough. Evidence for `DocDownPdf-PdfImageExtractor-JpegPassthrough` and
`DocDownPdf-PdfImageExtractor-RecordsTransformProvenance`.

#### A compressed-sample image is re-encoded and labeled so

**Test**: `PdfImageExtractor_AddImage_FlateImage_SetsDecodedToPngTransformHint`

Proves a sample-backed image is written as PNG and labeled as a decode-and-re-encode. Evidence for
`DocDownPdf-PdfImageExtractor-DecodesToPng` and
`DocDownPdf-PdfImageExtractor-RecordsTransformProvenance`.

#### Provenance fields are populated

**Test**: `PdfImageExtractor_AddImage_AnyImage_PopulatesHintProvenanceFields`

Proves source page, dimensions, and stable source reference are reported for each written image.
Evidence for `DocDownPdf-PdfImageExtractor-ReportsImageHints`.

#### A JPEG 2000 image is written unchanged as `.jp2`

**Test**: `PdfImageExtractor_AddImage_JpxImage_WritesJp2PassthroughWithoutExtraNote`

Proves a JPEG 2000 codestream is written unchanged, typed as `image/jp2`, labeled as a passthrough,
and does not produce an explanatory note because the extraction succeeded. Evidence for
`DocDownPdf-PdfImageExtractor-WritesJpeg2000Passthrough`.

#### An undecodable encoding is counted and named in a note

**Test**: `PdfImageExtractor_Extract_UndecodableEncoding_ReportsPlainNoteAndWritesNothingForIt`

Proves an undecodable image is not written silently and instead produces a one-sentence note naming
the encoding, count, and consequence. Evidence for
`DocDownPdf-PdfImageExtractor-NotesUndecodableEncodings`.

#### Size limits produce their own explanatory note

**Tests**: `PdfImageExtractor_Extract_ImageOverByteLimit_ReportsSizeNoteDistinctFromDecodeFailure`,
`PdfImageExtractor_Extract_ImageOverDimensionLimit_ReportsCountedSizeNote`

Proves byte and dimension limits each produce a size-oriented note rather than a decode-failure
note. Evidence for `DocDownPdf-PdfImageExtractor-HonorsSizeLimits`.

#### A PNG request that cannot be honored is explained, and one that can be honored is not

**Tests**: `PdfImageExtractor_Extract_ForcePngWithJpeg_ReportsUnhonoredModeNote`,
`PdfImageExtractor_Extract_ForcePngWithJpxImage_ReportsUnhonoredModeNoteNamingJpx`,
`PdfImageExtractor_Extract_ForcePngWithFlateImage_ReportsNoUnhonoredNote`

Proves the note names the encodings that prevented PNG output and that no note is emitted when the
requested PNG output mode was genuinely honored. Evidence for
`DocDownPdf-PdfImageExtractor-NotesUnhonoredForcePng`.

#### Disabling embedded images attempts nothing

**Test**: `PdfImageExtractor_Extract_ImagesDisabled_AttemptsNothing`

Proves the unit does not decode, write, count, or explain images when image extraction is disabled.
Evidence for `DocDownPdf-PdfImageExtractor-HonorsSuppression`.

#### A document with no images returns zero counts without a note

**Test**: `PdfImageExtractor_Extract_NoImages_ReturnsZeroCountsWithoutNote`

Proves an image-free document still reports zero found and written counts and emits no note. This is
supporting evidence for the inventory behavior consumed by `PdfDocumentExtractor`.

#### A null sink is rejected

**Test**: `PdfImageExtractor_Extract_NullSink_ThrowsArgumentNullException`

Proves the sink is mandatory. This is a defensive test with no linked requirement.
