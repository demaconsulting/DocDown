## PdfDocumentExtractor Verification Design

This document describes the unit-level verification strategy for `PdfDocumentExtractor`, the backend
the engine selects and invokes for a PDF.

### Verification Approach

`PdfDocumentExtractor` is verified through unit tests in `PdfDocumentExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfDocumentExtractor_`.

The unit is driven directly, outside the engine, against a shared `RecordingSink` and a plain
stand-in context. That combination is chosen deliberately: the sink records every emission in call
order, so what the extractor actually reported — and in what sequence — can be asserted rather than
inferred from a manifest the writers produced afterwards; and the stand-in context supplies only the
options, sink, and token this unit reads, keeping the test scoped to the backend rather than to the
engine that normally assembles the context.

The parser is **not** mocked. The documents are the real generated fixtures, so the unit is exercised
against genuine PDFs rather than against a simulation of one; mocking the parser would verify only
that the extractor calls the API it was written to call.

Sources are supplied as streams rather than files so the unit's buffering of a possibly non-seekable
source is exercised on every scenario, not only on the one that asserts it.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PDFs generated at test time by `PdfFixtures`; no committed binaries, no network access
- **Filesystem**: none for extraction scenarios; the self-test scenario uses a per-test `TempScratch`
  folder for its context's work folder
- **Mocking**: the sink and the context are test doubles; the parser and the delegated units are real
- **Isolation**: each test constructs its own extractor, sink, and document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfDocumentExtractor` unit test run passes when the declared identity,
format, capabilities, and priority are exactly the supported set with page rendering absent; when the
availability probe reports available, performs no input or output, never throws, and stays far inside
its time budget under repeated calls; when the document's declared title, author, and page count are
reported and a blank metadata value is reported as absent rather than empty; when the parser and the
absence of page rendering are recorded as environment facts; when a requested page range restricts
what is extracted; when a page-rendering request, a missing text layer, and a page-less document each
produce their documented gap and degrade rather than fail; when a parser fault propagates with its
explanation intact; and when the self-test cases run with the parse case passing and the rendering
case skipped with a reason. Any undeclared capability, any probe that touches the filesystem, any
suppressed parser fault, or any missing gap is a failure.

### Test Scenarios

#### Declared descriptor matches the supported contract

**Test**: `PdfDocumentExtractor_Descriptor_DeclaredProperties_MatchTheSupportedContract`

Proves the identity, display name, format, and priority are the documented values, that the three
deliverable capabilities are declared, and — explicitly — that page rendering is not. Evidence for
`DocDownPdf-PdfDocumentExtractor-DeclaresCapabilities`.

#### The availability probe is unconditional, cheap, and side-effect free

**Test**: `PdfDocumentExtractor_ProbeAvailability_ManagedOnlyBackend_ReportsAvailableWithoutIo`

Proves the probe reports available with the full declared set and no unavailable reason, and that a
hundred consecutive probes stay well inside the per-probe budget the contract allows. Repetition is
used rather than a single call because the engine may probe often, and a probe that became expensive
only under repetition would still be a defect. Evidence for
`DocDownPdf-PdfDocumentExtractor-ProbeIsCheapAndSafe`.

#### Document metadata is reported from the document

**Test**: `PdfDocumentExtractor_ExtractAsync_DocumentWithMetadata_ReportsTitleAuthorAndPageCount`

Proves the title and author the document declares are reported verbatim and the page count is the
document's real one. Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsDocumentInfo`.

#### The parser and the absent rendering capability are recorded

**Test**: `PdfDocumentExtractor_ExtractAsync_AnyDocument_ReportsEnvironmentFacts`

Proves the environment record names what parsed the document and states that page rendering was not
available from this backend, so a later reader of a degraded result can explain it. Evidence for
`DocDownPdf-PdfDocumentExtractor-ReportsEnvironmentFacts`.

#### A requested page range restricts the extraction

**Test**: `PdfDocumentExtractor_ExtractAsync_PageRange_ExtractsOnlyTheRequestedPages`

Proves in-range pages appear, out-of-range pages do not, and the reported part count records the size
of the slice actually extracted. Evidence for `DocDownPdf-PdfDocumentExtractor-HonorsPageRange`.

#### A rendering request produces the package-class gap

**Test**: `PdfDocumentExtractor_ExtractAsync_RenderPagesRequested_ReportsPackageClassGap`

Proves the gap targets the page folder with an unavailable scope, names the class of package that
provides rendering, carries no remedy of its own — the engine supplies the environment-shaped one —
and issues no instruction the reader could act on and fail at. Evidence for
`DocDownPdf-PdfDocumentExtractor-ReportsRenderGap`.

#### A missing text layer is coded and explained

**Test**: `PdfDocumentExtractor_ExtractAsync_NoTextLayer_ReportsCodedGapAndDegrades`

Proves the run degrades with `PDF0003` and a gap targeting the content document whose reason names
the absent text layer, so automation and a human reader can each distinguish a scanned document from
a broken extractor. Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsNoTextLayerGap`.

#### A page-less document is explained rather than failed

**Test**: `PdfDocumentExtractor_ExtractAsync_ZeroPageDocument_ReportsStructureGapWithoutThrowing`

Proves no exception is raised and a structure gap states that the document contains no pages.
Evidence for `DocDownPdf-PdfDocumentExtractor-ReportsEmptyDocumentGap`.

#### Parser faults propagate with their explanation

**Tests**: `PdfDocumentExtractor_ExtractAsync_MalformedDocument_PropagatesParserFaultForCore`,
`PdfDocumentExtractor_ExtractAsync_EncryptedDocument_PropagatesParserFaultForCore`

Proves the extractor does not suppress a parser fault or flatten it into a successful outcome, and
that the fault carries an explanation — for the protected document, one that names the condition. The
engine's conversion of that fault into a coded, structured failure with the full layout is evidenced
at system level. Evidence for `DocDownPdf-PdfDocumentExtractor-MapsParserFailures`.

#### Self-test cases run and report honestly

**Test**: `PdfDocumentExtractor_GetSelfTestCases_DeployedBackend_ReturnsParseAndRenderingCases`

Proves both cases are contributed under this backend's category, that the parse round trip genuinely
passes in the environment under test, and that the capability this package does not claim reports a
skip with a reason rather than a failure. Evidence for
`DocDownPdf-PdfDocumentExtractor-ContributesSelfTests`.

#### A null context is rejected

**Test**: `PdfDocumentExtractor_ExtractAsync_NullContext_ThrowsArgumentNullException`

Proves the context — the extractor's only channel to the outside world — is mandatory. This is a
defensive test with no linked requirement.
