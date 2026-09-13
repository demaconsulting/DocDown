# DocDown.Pdf Verification Design

This document describes the system-level verification strategy for `DocDown.Pdf`, the PDF extraction
package.

## Verification Approach

`DocDown.Pdf` is verified through system-level integration tests in `DocDownPdfTests.cs` and
unit tests per unit, all in `DemaConsulting.DocDown.Pdf.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with `ContractAssert.NoViolations`, which runs
Core's contract verifier over the produced folder and reports any disagreement between what the
manifest claims and what is on disk. This is the highest-value assertion available to this package:
it makes a dishonest extraction a test failure rather than a review finding, and it applies to
degraded and failed runs as well as clean ones. A dedicated theory runs it across every fixture the
suite has, so no scenario is verified without it.

### Degradation is the common case under test

Most scenarios below assert an honest, explained shortfall rather than a clean success, because that
is what this package's real usage looks like. It ships no renderer, so every page-rendering request
degrades. It decodes no JPEG 2000, so a document containing one is delivered with a readability
caveat rather than cleanly; it decodes no JBIG2, and a JBIG2 image carries no container, so a document
containing one is partially extracted. Scanned documents carry no text. Protected and corrupt
documents fail. Treating these as the normal rows of the matrix — rather than as edge cases appended
after a happy path — is what keeps the honest paths as well tested as the clean one.

### Fixtures are generated, never committed

Every PDF the suite uses is built at test time, by the parser's own document writer for the ordinary
cases and byte by byte in code for the three the writer cannot produce (a protected document, one
carrying a JPEG 2000 image, and one carrying a JBIG2 image). No binary PDF is committed, so the
repository stays text-only and no question arises about the provenance or licensing of a sample
document. The JPEG the image fixtures embed, and the JPEG 2000 codestream, are retained as exact
constants, which is what lets each passthrough provenance claim be checked against the true input
rather than against the extractor's own output.

### Text assertions are property-based, not golden

Reading-order recovery and heading inference are heuristics, so asserting exact markdown would be
brittle against harmless segmentation changes. The text scenarios therefore assert properties —
expected substrings present, the relative order of known markers, structure the raw page text does
not have, links under the right page — and byte-exact comparison is reserved for the determinism
scenario, where both sides come from the same code.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input document and the
  extraction output
- **Inputs**: PDFs generated at test time; no committed binary fixtures and no network access
- **Mocking**: none for system tests, which drive the real engine and the real parser; unit tests use
  the shared `RecordingSink` so a unit's emissions can be inspected without running the writers
- **Determinism**: a fixed `TimestampUtc` is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations.
- No adverse document — protected, malformed, or page-less — causes an exception to escape to the
  caller, and every one of them still produces the full output layout.
- Every image written is described truthfully: its file extension, its manifest media type, and its
  recorded provenance all agree with the bytes on disk.
- Every image not written is counted and explained by a gap naming its reason, and every image
  written in a format many consumers cannot read carries a caveat that reaches `summary.txt`.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.
- Two runs over the same document with a fixed timestamp produce byte-identical artifacts.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
Platform requirements are covered by the source-filtered runs of the layout scenario.

### Contract layout is produced for a text PDF

**Test**: `DocDownPdf_Extract_SimpleTextPdf_ProducesContractLayout`

Proves a clean extraction: the full four-artifact layout, the document's text in `content.md`, the
PDF backend selected, no gaps, and a complete result. This is also the anchor for the platform
requirements. Evidence for `DocDownPdf-Registration`, `DocDownPdf-DeclaredCapabilities`, and
`DocDownPdf-TextContent`.

### Page count and all pages' text are reported

**Test**: `DocDownPdf_Extract_MultiPagePdf_ReportsPageCountAndAllText`

Proves the manifest carries the document's real page count and that every page's text appears in
reading order across pages. Evidence for `DocDownPdf-DocumentMetadata` and `DocDownPdf-TextContent`.

### A stored JPEG round-trips byte-identically as a passthrough

**Test**: `DocDownPdf_Extract_DctImage_RoundTripsByteIdenticalAsPassthrough`

Proves the passthrough claim rather than merely observing the label. The written file is compared
against the exact JPEG the fixture embedded, so an extractor that silently re-encoded while still
reporting a passthrough would fail on the bytes. The manifest's media type, provenance, and the
content document's link are asserted alongside. Evidence for `DocDownPdf-EmbeddedImages` and
`DocDownPdf-ImageProvenance`.

### A compressed-sample image is labeled as a re-encoding

**Test**: `DocDownPdf_Extract_FlateImage_IsLabeledDecodedToPng`

The counterpart to the scenario above, and the reason there are two: a test of one direction alone
would be satisfied by an extractor that reports a single constant. Together they prove both
directions end to end. Evidence for `DocDownPdf-EmbeddedImages` and `DocDownPdf-ImageProvenance`.

### An undecodable image becomes a counted gap naming its encoding

**Test**: `DocDownPdf_Extract_UndecodableImage_ReportsCountedGapNamingEncoding`

Proves the case where silence would be easiest and most damaging. A document holding one decodable
and one JBIG2 image yields one written image, a partial ledger entry reading one of two, a gap
naming the encoding and the count, the `PDF0001` diagnostic, and a summary that states the shortfall
in words. Evidence for `DocDownPdf-GapReporting`.

### A JPEG 2000 image is written as .jp2 with a readability caveat

**Test**: `DocDownPdf_Extract_Jpeg2000Image_WritesJp2PassthroughWithReadabilityCaveat`

Proves the other half of the encoding story, which is not a loss. A JPEG 2000 codestream is a real
image file, so it is written byte-identically under a `.jp2` extension, typed `image/jp2`, and
labeled a passthrough; both images count toward the ledger's obtained side, so "n of m" keeps meaning
what it says. The caveat — that many viewers and image libraries cannot read JPEG 2000 — is asserted
in `summary.txt` on whitespace-normalized text, because the summary wraps its prose and a
line-oriented match would report a false absence. Evidence for `DocDownPdf-EmbeddedImages` and
`DocDownPdf-ImageProvenance`.

### A PNG request that cannot be honored is explained

**Test**: `DocDownPdf_Extract_ForcePngWithJpegImage_ExplainsUnhonoredMode`

Proves both halves of the honest response. The file on disk still describes its own bytes — a `.jpg`
extension and a JPEG media type — and the run additionally explains, with `PDF0002` and a counted
gap, which images defeated the request and why, reporting degraded rather than claiming success.
Evidence for `DocDownPdf-ImageOutput`.

### A size-limit skip is distinguishable from a decode failure

**Test**: `DocDownPdf_Extract_ImageOverSizeLimit_ReportsSeparateSkipGap`

Proves the two conditions are never conflated: the gap names a limit, carries a remedy the caller can
act on, and no decode-failure diagnostic is emitted. Evidence for `DocDownPdf-ImageOutput`.

### Disabled images are suppressed with a gap and no dangling links

**Test**: `DocDownPdf_Extract_ImagesDisabled_SuppressesImagesWithGap`

Proves the deliberate absence is recorded and that the content document contains no link to an image
that was not written. Evidence for `DocDownPdf-ImageOutput`.

### A scanned PDF degrades with an honest no-text-layer gap

**Test**: `DocDownPdf_Extract_ScannedPdfWithNoTextLayer_DegradesWithHonestGap`

Proves the common scanned-document case: the run degrades rather than failing, `content.md` is
written rather than missing, `PDF0003` and a gap name the absent text layer, and the page's embedded
image is still extracted — so the result is degraded, not useless. Evidence for
`DocDownPdf-NoTextLayerDegradation`.

### A page-rendering request degrades and names the class of package that provides it

**Test**: `DocDownPdf_Extract_PagesRequested_DegradesWithDD0301AndNamesPackageClass`

Proves the engine records the unmet capability with its codes, no page is rendered, and the summary
states where rendering comes from. The summary text is compared with whitespace normalized, because
it wraps to a fixed width and a line-oriented match would pass or fail on where the wrap fell. The
test also asserts the summary issues no installation instruction that cannot succeed today. Evidence
for `DocDownPdf-NoPageRendering`.

### A protected document fails structurally with the full layout written

**Test**: `DocDownPdf_Extract_EncryptedPdf_FailsStructurallyAndWritesFullLayout`

Proves no exception escapes to the caller, the failure is coded and structured, its explanation names
the condition, and the complete layout is still produced. Evidence for
`DocDownPdf-ProtectedDocument`.

### A malformed document fails structurally with the full layout written

**Test**: `DocDownPdf_Extract_MalformedPdf_FailsStructurallyAndWritesFullLayout`

As above for a truncated document with no cross-reference table, additionally asserting the failed
artifacts carry explaining gaps. Evidence for `DocDownPdf-MalformedDocument`.

### A page-less document completes with explicit gaps

**Test**: `DocDownPdf_Extract_ZeroPagePdf_CompletesWithExplicitGaps`

Proves a valid but empty document is neither a failure nor an unexplained empty result: no failure is
recorded and a gap states that the document contains no pages. Evidence for
`DocDownPdf-EmptyDocument`.

### A requested page range restricts the extraction

**Test**: `DocDownPdf_Extract_PageRangeRequested_RestrictsToRange`

Proves only the requested page's content appears and the out-of-range pages' content does not.
Evidence for `DocDownPdf-PageRange`.

### Repeated extraction is byte-identical

**Test**: `DocDownPdf_Extract_SameDocumentTwice_ProducesByteIdenticalArtifacts`

Proves determinism over the whole artifact tree, not merely the two root files: every file produced
by the first run, including the extracted images and the gap text that names encodings and counts, is
compared byte for byte against the second. Evidence for `DocDownPdf-Determinism`.

### Reported gaps match the filesystem for every fixture

**Test**: `DocDownPdf_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

The reconciliation theory, run across every fixture the suite has — clean, degraded, and failed. This
is the scenario that makes the honesty invariant machine-checked rather than reviewed. Evidence for
`DocDownPdf-GapReporting`.

### No PDF-parser type reaches the public API

**Test**: `DocDownPdf_PublicApi_AllPublicMembers_ExposeNoPdfPigTypes`

Proves the containment by reflecting over every exported type's members and expanding generic
arguments and element types, so a leak hidden inside a collection is caught as readily as a bare
parameter. Evidence for `DocDownPdf-ContainedDependency`.

### The package ships no native assets

**Test**: `DocDownPdf_Package_BuildOutput_ContainsNoNativeAssets`

Proves the package's own build output — not the test host's, which legitimately carries test-platform
runtime assets of its own — has no runtime-identifier folder, no unmanaged binary of any platform,
and that every library present is a managed assembly. It also asserts the project declares no runtime
identifier, so no such asset can appear later. Evidence for `DocDownPdf-ManagedOnly`.

### Self-validation cases are exposed and run

**Test**: `DocDownPdf_SelfValidation_RegisteredEngine_ReportsPdfCases`

Proves the backend contributes cases an operator can run on demand, that the parse round trip passes
in the environment under test, and that the capability this package does not claim reports a reasoned
skip rather than a failure. Evidence for `DocDownPdf-SelfValidation`.
