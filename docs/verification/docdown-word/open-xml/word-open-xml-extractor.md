## WordOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlExtractor`, the managed
backend the engine selects and invokes for a `.docx`.

### Verification Approach

`WordOpenXmlExtractor` is verified through integration tests in `OpenXml/WordOpenXmlExtractorTests.cs`
in `DemaConsulting.DocDown.Word.Tests`, with method names beginning with `WordOpenXmlExtractor_`.

The unit is driven **through the engine end to end**, not against a stand-in context, because its
observable contract is what a host sees: the descriptor's declared capabilities, the produced
folder, the diagnostics, and the gaps. Every extraction ends with `ContractAssert.NoViolations`, so
the reported gaps match what is on disk on the same run that asserts the diagnostic code and gap
reason — a defect that emitted the wrong count or wrote the wrong file would fail the reconciliation
in the same scenario.

The SDK is not mocked. The documents are the real generated fixtures from
`TestData/DocxFixtures.cs`, so the unit is exercised against genuine `.docx` files rather than
against a simulation of one.

Diagnostic codes are asserted by their pinned identifier — `WORD0001`, `WORD0005`, `WORD0006`,
`WORD0007` — because the diagnostic-code table is a contract downstream consumers branch on. The
page-rendering scenario additionally asserts that the rendered `summary.txt` does not carry the
forbidden-remedy substring (case-insensitive), because the gap's remedy must state a fact about the
environment rather than instruct a step the reader could try and fail at.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by `TestData/DocxFixtures.cs`
- **Filesystem**: a per-test `TempScratch` folder holds the fixture file and the output folder
- **Mocking**: none; the SDK is exercised against real documents and the sink is Core's real
  writer through the engine
- **Isolation**: each test constructs its own engine and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlExtractor` unit test run passes when the descriptor's identity,
priority, supported format, and capabilities are exactly the deliverable set with the
rendered-pages capability absent; when a merged-cell document produces a partially-extracted
structural gap with a positive affected count and the `WORD0005` diagnostic; when a force-PNG
request produces `WORD0007` and a gap targeting `images/` whose reason names the request; when a
vector image is written and `WORD0006` records the readability caveat; when an empty document
degrades with `WORD0001`; when a page-rendering request degrades with a `Pages`-kind gap whose
reason names the fact declaratively and the rendered summary carries no remedy the reader could act
on and fail at;
when per-part mode produces a `parts/` folder split at each Heading 1; when a header logo identical
to a body image reuses one deduplicated image path; and when the extractor contributes exactly two
self-test cases with the round-trip case passing and the page-rendering case skipped, and no case
failing. Any undeclared capability, any missing diagnostic, any uncounted gap, or any contract
violation the verifier reports is a failure.

### Test Scenarios

#### Declared descriptor matches the supported contract

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndCapabilities`

Proves the identity is `word-openxml`, the priority is 10, the `.docx` format is supported, the
document-structure capability is declared, and — expressly — the rendered-pages capability is not.
The absence assertion is what keeps the descriptor from claiming a capability the backend cannot
deliver, so a page-rendering request can be negotiated up front rather than discovered as missing
output. Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-DeclaresCapabilities`.

#### Merged cells produce a counted structural gap

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsCountedStructuralGap`

Proves the flatten count the table writer returned surfaces as a structural gap of
`PartiallyExtracted` scope with a positive affected count and the `WORD0005` diagnostic, and that
the reported gap matches what is on disk. Evidence for
`DocDownWord-OpenXml-WordOpenXmlExtractor-ReportsStructuralGap`.

#### A force-PNG request that cannot be honored is explained

**Test**: `WordOpenXmlExtractor_Extract_ForcePng_ExplainsUnhonoredMode`

Proves the diagnostics carry `WORD0007` and a gap targeting `images/` whose reason names the
request — that PNG output was requested but cannot be produced — so the caller learns which images
defeated the request and why, without a mislabeled format. Evidence for
`DocDownWord-OpenXml-WordOpenXmlExtractor-ExplainsUnhonoredForcePng`.

#### A vector image is written as-is with a caveat

**Test**: `WordOpenXmlExtractor_Extract_VectorImage_WritesAsIsWithCaveat`

Proves the EMF part reaches `images/` as one file and `WORD0006` records the readability caveat, so
the image survives in the format the document carried while the caveat states what many viewers
cannot render. Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-WritesVectorImageWithCaveat`.

#### An empty document degrades with a no-text gap

**Test**: `WordOpenXmlExtractor_Extract_EmptyDocument_DegradesWithNoTextGap`

Proves the outcome degrades and `WORD0001` records the missing text, so an empty content document
is a stated fact rather than a mystery indistinguishable from a broken extractor. Evidence for
`DocDownWord-OpenXml-WordOpenXmlExtractor-ReportsEmptyDocumentGap`.

#### A page-rendering request degrades with a reasoned gap

**Test**: `WordOpenXmlExtractor_Extract_PagesRequested_DegradesWithReasonedGap`

Proves the outcome degrades, a gap of kind `Pages` names the fact declaratively — that this backend
does not render pages — and the rendered `summary.txt` does not carry the forbidden-remedy
substring, so
the remedy states an environment fact rather than an instruction the reader could act on and fail
at. Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-ReportsPageRenderingGap`.

#### Per-part mode splits at Heading 1

**Test**: `WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Proves a `parts/` folder exists and holds at least two files for a document with two top-level
headings, so the split honors the request rather than returning a single flow the caller would then
re-segment. Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-SplitsPerPart`.

#### A header logo is deduplicated with the body

**Test**: `WordOpenXmlReader_Read_HeaderWithLogo_ReusesDeduplicatedImagePath`

Proves a logo that appears in both the header and the body is written as one file rather than two,
and the contract verifier reports no violations. Ownership of the deduplication is the reader's,
but the observable effect — one file in `images/` — is exercised through the extractor here.
Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-ReusesDeduplicatedImagePath`.

#### Self-test cases run and report honestly

**Test**: `WordOpenXmlExtractor_SelfValidation_ReportsCases`

Proves the backend contributes exactly two cases under its category, that one result is passed and
one is skipped — the round-trip case genuinely passes in the environment under test, and the
capability this backend does not claim reports a skip with a reason — and that no case is failed.
Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-ContributesSelfTests`.
