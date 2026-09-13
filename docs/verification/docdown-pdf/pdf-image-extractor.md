## PdfImageExtractor Verification Design

This document describes the unit-level verification strategy for `PdfImageExtractor`, which writes a
PDF's embedded images and accounts for the ones it cannot deliver.

### Verification Approach

`PdfImageExtractor` is verified through unit tests in `PdfImageExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfImageExtractor_`.

The unit is driven directly against pages opened from the generated fixtures, writing through the
shared `RecordingSink`. The sink retains the exact bytes and the exact `ImageHint` it was handed,
which is what makes this the right level at which to verify provenance: the system tests prove the
label reaches the manifest, and these prove the label *originated here*, per image, from the code
that also chose the bytes.

**The honest-provenance invariant is treated with the same rigor the repository gives the contract
verifier.** Its statement is: for every image, the bytes handed to the sink, the media type declared
for them, and the transform reported about them agree with each other and with what actually
happened. A test that only observed the label would be satisfied by an extractor that reports a
constant, so the passthrough scenario asserts **byte identity against the fixture's retained source
JPEG** rather than against anything the extractor produced. That comparison is what makes the claim
falsifiable: an extractor that silently re-encoded while still reporting a passthrough fails on the
bytes, not on the wording. The re-encoding scenario is its mandatory counterpart, proving the other
direction is reachable and distinct.

The parser is not mocked, for the same reason as elsewhere: the behavior under test is the encoding
decision taken over a real document's real image dictionaries.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: pages from PDFs generated at test time, including two assembled byte by byte — one
  carrying a JPEG 2000 image alongside a decodable JPEG, one carrying a JBIG2 image alongside the same
  JPEG; no committed binaries, no network access
- **Filesystem**: none; every byte goes to the recording sink
- **Mocking**: the sink is a test double; the parser is real
- **Isolation**: each test opens its own document and its own sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfImageExtractor` unit test run passes when a stored JPEG is handed to the
sink as the exact bytes the document embedded, with a JPEG media type and a passthrough transform;
when a JPEG 2000 codestream is likewise handed over unchanged, typed `image/jp2`, labeled a
passthrough, and accompanied by a caveat saying many viewers and image libraries cannot read the
format; when a compressed-sample image is handed over as a PNG with a decoded-to-PNG transform
distinct from the passthrough label; when each image's dimensions, source page, and a stable reference
are reported; when an image that cannot be decoded at all is counted, named by encoding, and not
written; when the gap scope is unavailable if nothing was written and partially extracted otherwise;
when a size or dimension skip is reported separately from a decode failure and carries an actionable
remedy; when a PNG request that cannot be honored is explained, naming the encodings actually written,
and one that can is not; when disabling embedded images attempts nothing at all; and when a document
with no images still reports a zero found count. Any image written with a transform that disagrees
with how its bytes were produced, any image omitted without a count, any image written in a
narrowly-readable format without a caveat, and any conflation of a size skip with a decode failure is
a failure.

### Test Scenarios

#### A stored JPEG is passed through unchanged and labeled so

**Test**: `PdfImageExtractor_AddImage_DctImage_SetsPassthroughTransformHint`

Proves the bytes handed to the sink are byte-for-byte the JPEG the fixture embedded, that the media
type declared for them is JPEG, and that the transform reported is passthrough. The byte comparison
is the load-bearing part: without it the label would be a tautology. Evidence for
`DocDownPdf-PdfImageExtractor-JpegPassthrough` and
`DocDownPdf-PdfImageExtractor-RecordsTransformProvenance`.

#### A compressed-sample image is re-encoded and labeled so

**Test**: `PdfImageExtractor_AddImage_FlateImage_SetsDecodedToPngTransformHint`

The mandatory counterpart. Proves the bytes are handed over as a PNG, labeled as the re-encoding they
are, and explicitly not labeled a passthrough. Evidence for
`DocDownPdf-PdfImageExtractor-DecodesToPng` and
`DocDownPdf-PdfImageExtractor-RecordsTransformProvenance`.

#### Provenance fields are populated

**Test**: `PdfImageExtractor_AddImage_AnyImage_PopulatesHintProvenanceFields`

Proves the dimensions, the source page, and a stable reference are supplied, so a reader of the
output can find the image back in the source document and so two runs produce the same reference.
Evidence for `DocDownPdf-PdfImageExtractor-ReportsImageHints`.

#### A JPEG 2000 image is written unchanged as .jp2 and caveated

**Test**: `PdfImageExtractor_AddImage_JpxImage_WritesJp2PassthroughWithCaveat`

Proves the codestream handed to the sink is byte-for-byte the one the fixture embedded, typed
`image/jp2` and labeled a passthrough; that both images count as written, so the image is in the
extracted column and not the loss column; and that the run carries `PDF0004` and a gap warning that
many viewers and image libraries cannot read JPEG 2000, with `PDF0001` explicitly absent. The caveat
text is matched on whitespace-normalized prose, because the same sentence is wrapped when it reaches
`summary.txt` and a line-oriented match would report a false absence. Evidence for
`DocDownPdf-PdfImageExtractor-WritesJpeg2000WithCaveat`.

#### An undecodable encoding is counted and named

**Test**: `PdfImageExtractor_Extract_UndecodableEncoding_ReportsCountedGapAndWritesNothingForIt`

Proves the decodable image is delivered while the JBIG2 one is neither written nor silently omitted:
the found count is the true total, the gap names the encoding with its own count and the "n of m"
phrase, the affected item is enumerated, and `PDF0001` is emitted. JBIG2 rather than JPEG 2000 is the
fixture here because a JBIG2 segment carries no container — there is no extension under which its
bytes could honestly be written — which is what makes it the genuine loss case. Evidence for
`DocDownPdf-PdfImageExtractor-ReportsUndecodableEncodings`.

#### Nothing written yields an unavailable scope

**Test**: `PdfImageExtractor_Extract_NothingWritten_ReportsUnavailableScope`

Proves the scope follows what was actually written. Calling an empty folder partially extracted would
overstate the result and contradict the contract verifier's rule that a partial folder holds at least
one file, so this distinction is verified rather than assumed. Evidence for
`DocDownPdf-PdfImageExtractor-ReportsUndecodableEncodings`.

#### A size skip is distinguishable from a decode failure

**Tests**: `PdfImageExtractor_Extract_ImageOverByteLimit_ReportsSizeGapDistinctFromDecodeGap`,
`PdfImageExtractor_Extract_ImageOverDimensionLimit_SkipsWithCountedGap`

Proves a byte-budget skip produces a gap naming a limit, carrying a remedy the caller can act on, and
emitting no decode-failure diagnostic — and that a dimension limit behaves the same way with its own
count. Reporting the two conditions as one would offer a remedy that cannot work or hide one that
would. Evidence for `DocDownPdf-PdfImageExtractor-HonorsSizeLimits`.

#### A PNG request that cannot be honored is explained, and one that can is not

**Tests**: `PdfImageExtractor_Extract_ForcePngWithJpeg_ReportsUnhonoredModeGap`,
`PdfImageExtractor_Extract_ForcePngWithJpxImage_ReportsUnhonoredModeGapNamingJpx`,
`PdfImageExtractor_Extract_ForcePngWithFlateImage_ReportsNoUnhonoredGap`

Proves the image is still delivered, truthfully typed, and that `PDF0002` and a counted gap name the
encoding responsible; that a JPEG 2000 passthrough defeats the request in exactly the same way and is
named as `JPXDecode` rather than assumed to be JPEG; and, in the paired scenario, that a request which
*was* honored produces no gap at all. The last is what keeps the explanation meaningful rather than
unconditional noise. Evidence for `DocDownPdf-PdfImageExtractor-ExplainsUnhonoredForcePng`.

#### Disabling embedded images attempts nothing

**Test**: `PdfImageExtractor_Extract_ImagesDisabled_AttemptsNothing`

Proves no image, link, count, or gap of this unit's own is produced. The caller asked for none to be
attempted, not merely for none to be kept, and Core already owns the record of that decision.
Evidence for `DocDownPdf-PdfImageExtractor-HonorsSuppression`.

#### A document with no images still reports its denominator

**Test**: `PdfImageExtractor_Extract_NoImages_ReportsZeroFoundCount`

Proves the found count is reported even when it is zero, so the ledger can state zero of zero rather
than leaving the denominator unknown. This is a supporting scenario with no linked requirement.

#### A null sink is rejected

**Test**: `PdfImageExtractor_Extract_NullSink_ThrowsArgumentNullException`

Proves the unit's only output channel is mandatory. This is a defensive test with no linked
requirement.
