## PdfDocumentExtractor Verification Design

This document describes the unit-level verification strategy for `PdfDocumentExtractor`, the backend
the engine selects and invokes for a PDF.

### Verification Approach

`PdfDocumentExtractor` is verified through unit tests in `PdfDocumentExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfDocumentExtractor_`.

The unit is driven directly, outside the engine, against a shared `RecordingSink` and a small
stand-in extraction context. That combination keeps the verification focused on what the extractor
itself reports: document information, environment facts, markdown content, notes, and content
features.

The parser is not mocked. The tests open real generated PDFs so page selection, metadata reading,
image delegation, and text delegation are exercised against genuine parser behavior.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PDFs generated at test time by `PdfFixtures`; no committed binaries and no network
  access
- **Filesystem**: none for extraction scenarios; the self-test scenario uses a per-test
  `TempScratch` work folder
- **Mocking**: the sink and extraction context are test doubles; the parser and delegated units are
  real
- **Isolation**: each test constructs its own extractor, sink, and source document

### Acceptance Criteria

Per IEC 62304 Section 5.5.2, a `PdfDocumentExtractor` unit test run passes when the documented
identity, display name, supported format, and priority are stable; when the availability probe is
cheap, side-effect free, non-throwing, and always available for the managed backend; when document
information and PDF document metadata are reported from the source document; when parser and
page-rendering environment facts are recorded; when a requested page range restricts both the
extraction and the reviewer comments reported from it; when reviewer annotations are reported as
review comments located by page and an annotation-free document reports none; when the content
inventory reports factual zero counts for text-free or page-less inputs; when parser
faults propagate for Core to convert into unreadable results; and when the self-test cases report a
passing parse round trip and a skipped page-rendering case. Any hidden parser fault, invented count,
or I/O performed by the availability probe is a failure.

### Test Scenarios

#### Declared descriptor matches the supported contract

**Test**: `PdfDocumentExtractor_Descriptor_DeclaredProperties_MatchTheSupportedContract`

Proves the extractor's identity, display name, supported format, and priority match the documented
surface. Evidence for `DocDownPdf-PdfDocumentExtractor-DescribesSupportedContract`.

#### The availability probe is unconditional, cheap, and side-effect free

**Test**: `PdfDocumentExtractor_ProbeAvailability_ManagedOnlyBackend_ReportsAvailableWithoutIo`

Proves repeated probes stay within budget, report the backend available, do not throw, and do not
report rendered-page support. Evidence for `DocDownPdf-PdfDocumentExtractor-ProbesManagedAvailability`.

#### Document metadata is reported from the document

**Test**: `PdfDocumentExtractor_ExtractAsync_DocumentWithMetadata_ReportsTitleAuthorAndPageCount`

Proves the title, author, and page count recorded on the sink come from the source document.
Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsDocumentInfo`.

#### Parser and page-rendering environment facts are recorded

**Test**: `PdfDocumentExtractor_ExtractAsync_AnyDocument_ReportsEnvironmentFacts`

Proves the environment facts name the managed parser and state plainly that page rendering is not
provided by this extractor. Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsEnvironmentFacts`.

#### A requested page range restricts the extraction

**Test**: `PdfDocumentExtractor_ExtractAsync_PageRange_ExtractsOnlyTheRequestedPages`

Proves only the requested pages appear in the content and the recorded part count matches the slice
actually extracted. Evidence for `DocDownPdf-PdfDocumentExtractor-HonorsPageRange`.

#### Reviewer annotations are reported as review comments located by page

**Test**: `PdfDocumentExtractor_ExtractAsync_AnnotatedDocument_ReportsReviewComments`

Proves every remark the annotated fixture carries reaches the sink through the review-comment
channel, each located as `Page {n}`. The location wording is asserted here, not at the annotation
extractor, because this unit owns it. Evidence for
`DocDownPdf-PdfDocumentExtractor-ReportsReviewComments`.

#### A page range restricts reviewer comments along with the content

**Test**: `PdfDocumentExtractor_ExtractAsync_PageRange_RestrictsReviewComments`

Proves comments and content are driven from the same page selection, so a comment can never arrive
from a page the output does not contain. Evidence for
`DocDownPdf-PdfDocumentExtractor-ReportsReviewComments` and
`DocDownPdf-PdfDocumentExtractor-HonorsPageRange`.

#### A document with no annotations reports no review comments

**Test**: `PdfDocumentExtractor_ExtractAsync_DocumentWithoutAnnotations_ReportsNoReviewComments`

Proves nothing is reported for an uncommented document, which is what leaves Core with no reason to
write a review artifact. Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsReviewComments`.

#### Text-free and page-less documents report zero-count inventory

**Tests**: `PdfDocumentExtractor_ExtractAsync_NoTextLayer_WritesImageBackedContentWithZeroTextCounts`,
`PdfDocumentExtractor_ExtractAsync_ZeroPageDocument_ReportsZeroCountInventoryWithoutThrowing`

Proves the extractor reports content-feature counts from the extraction walk itself, including zero
headings and paragraphs for a scanned page and all-zero counts for a zero-page document. Evidence
for `DocDownPdf-PdfDocumentExtractor-ReportsContentInventory`.

#### Comments and their authors are inventoried from a single annotation pass

**Test**: `PdfDocumentExtractor_ExtractAsync_AnnotatedDocument_InventoriesComments`

Proves an annotated document reports `comments` and `distinct comment authors` as looked-for content
features whose counts agree exactly with the review comments the same extraction reported. Reviewer
commentary leaves `content.md` entirely, so the inventory is the only place the summary says a PDF
carries any. Asserting the counts against the reported comments rather than against a literal is
what pins the single-pass arrangement: the annotations are read once and that one result feeds both
the inventory and the comments, so the two can never describe different walks of the same pages.
Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsContentInventory`.

#### Parser faults propagate with their explanation intact

**Tests**: `PdfDocumentExtractor_ExtractAsync_MalformedDocument_PropagatesParserFaultForCore`,
`PdfDocumentExtractor_ExtractAsync_EncryptedDocument_PropagatesParserFaultForCore`

Proves the extractor does not suppress or translate parser faults before Core can convert them.
Evidence for `DocDownPdf-PdfDocumentExtractor-MapsParserFailures`.

#### Self-test cases run and report honestly

**Test**: `PdfDocumentExtractor_GetSelfTestCases_DeployedBackend_ReturnsParseAndRenderingCases`

Proves the extractor contributes both documented self-test cases and that the page-rendering case is
reported as skipped rather than as failed. Evidence for
`DocDownPdf-PdfDocumentExtractor-ContributesSelfTests`.

#### A null context is rejected

**Test**: `PdfDocumentExtractor_ExtractAsync_NullContext_ThrowsArgumentNullException`

Proves the extraction context is mandatory. This is a defensive test with no linked requirement.
