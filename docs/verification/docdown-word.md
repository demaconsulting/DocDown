# DocDown.Word Verification Design

This document describes the system-level verification strategy for `DocDown.Word`, the Word
extraction package.

## Verification Approach

`DocDown.Word` is verified through system-level integration tests in `DocDownWordTests.cs` and
`WordGoldenTests.cs`, and unit tests per unit, all in `DemaConsulting.DocDown.Word.Tests`, running on
xUnit v3 across net8.0, net9.0, and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with `ContractAssert.NoViolations`, which runs Core's
contract verifier over the produced folder and reports any disagreement between what the manifest
claims and what is on disk. This is the highest-value assertion available to this package: it makes a
dishonest extraction a test failure rather than a review finding, and it applies to degraded and
failed runs as well as clean ones. The reconciliation covers the `report.doc` scenario where the
legacy binary format is refused, so the full-layout claim on a structured failure is machine-checked
alongside the successful ones.

### One backend, one reader, one emitter

The package ships one backend: the managed Open XML reader. It reads a document into the
reader-neutral model and drives the content emitter, so every mapping decision is made in exactly one
place and can be proved from a hand-built model with no document behind it. A legacy binary `.doc`
has no reader here and none anywhere in DocDown, so the request fails with a structured, reasoned
outcome — never with an exception, and never with a remedy that promises a capability that does not
exist.

### Degradation is the common case under test

Most scenarios below assert an honest, explained shortfall rather than a clean success, because that
is what this package's real usage looks like. It ships no renderer, so every page-rendering request
degrades. It ships no imaging stack, so a force-PNG request that cannot be honored is written through
and explained rather than refused. A GFM table cannot express a merged span or a nested cell, so
those are flattened and counted. A footer that carries only page-numbering fields is omitted and
recorded as an informational diagnostic — not a gap and not a degrade — rather than repeated per
page. Treating each of these as an ordinary row of the matrix
keeps the honest paths as well tested as the clean one.

### Fixtures are generated, never committed

Every document the suite uses is built at test time by the Open XML SDK writer in
`TestData/DocxFixtures.cs`, orchestrated through `TestData/WordTestHarness.cs`. The legacy `.doc`
scenario writes a placeholder byte sequence whose extension drives format detection, because the
selection path never opens the file: no registered backend supports the format. No binary `.docx` or
`.doc` is committed, so the repository stays text-only and no question arises about the provenance or
licensing of a sample document.

### Golden summaries pin representative outputs

Four representative extractions render their `summary.txt` under a fixed timestamp and a fixed
backend, replace three host-environment lines and the two absolute paths with placeholders, and
compare the remainder byte for byte against committed goldens under `test/.../golden/`. Setting
`DOCDOWN_UPDATE_GOLDEN=1` rewrites a golden, and regeneration is refused when a CI environment is
detected — so a golden can never silently self-heal. The mechanism mirrors the Core
`SummaryWriterGoldenTests` precedent.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input document and the
  extraction output
- **Inputs**: WordprocessingML documents generated at test time by the Open XML SDK writer, plus a
  short byte sequence for the legacy `.doc` refusal scenario; no committed binary fixtures and no
  network access
- **Mocking**: none — every test drives the real engine and the real Open XML backend
- **Determinism**: a fixed `TimestampUtc` is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations.
- No adverse document — protected, legacy, or empty — causes an exception to escape to the caller,
  and every one of them still produces the full output layout.
- Every image written is described truthfully: its file extension, its manifest media type, and its
  recorded provenance all agree with the bytes on disk; every image not written in the requested
  form is counted and explained.
- Every structural shortfall a GFM table cannot express is counted and named as a structural gap,
  and every header or footer that carries only page-numbering fields, and every empty header or
  footer, is omitted and recorded as an informational diagnostic that neither raises a gap nor
  degrades the run.
- The legacy binary `.doc` format is refused with a coded structured failure whose remedy states
  that DocDown does not support legacy binary formats, never an exception and never an instruction
  the reader could act on and fail at.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.
- Four representative extractions produce a `summary.txt` byte-identical to its committed golden
  after the three host-environment lines and the two absolute paths are normalized.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences
it. Platform requirements are covered by the source-filtered runs of the layout scenario.

### The default engine registers the Word backend

**Test**: `AddWord_OnBuilder_RegistersOpenXmlBackend`

Proves the one-liner the host uses to add Word support registers the Open XML backend on the
resulting engine and registers nothing else, so the set of active backends is a decision readable in
host code rather than a deployment accident. Evidence for `DocDownWord-Registration`.

### Contract layout is produced for a generated document

**Test**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`

Proves a clean extraction: the full four-artifact layout, the document's content in `content.md`
carrying a real GFM table and a Document Control heading, the Open XML backend selected, no gaps,
and a complete result. This is also the anchor for the platform requirements. Evidence for
`DocDownWord-Extraction`, and under source filters the six `DocDownWord-Platform-*` requirements.

### A table renders as a GFM table

**Test**: `WordTableWriter_Write_FullTable_ProducesGfmWithHeaderAndAlignment`

Proves the header row, the delimiter row, and the body rows all appear as GFM. The full per-scenario
detail is given in the *Markdown* subsystem chapter and its *WordTableWriter* unit chapter. Evidence
for `DocDownWord-Tables`.

### Flattened cells are reported as a counted structural gap

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsCountedStructuralGap`

Proves a document with merged cells produces a partially-extracted structural gap with a positive
affected count and the `WORD0005` diagnostic, so the flattening the writer had to perform is a
statement in the output rather than a silent misrepresentation. Evidence for
`DocDownWord-StructuralFidelity`.

### Document Control is preserved from an engineering-style document

**Test**: `DocDownWord_Extract_EngineeringStyleDocx_PreservesRevisionInDocumentControl`

Proves the header's revision and classification survive under a `## Document Control` heading in the
content, and — crucially — that the page-number footer is omitted with a counted, reasoned gap
rather than repeated per page as `Page 1 of`. Evidence for `DocDownWord-DocumentControl` and
`DocDownWord-TrackedChanges` at system level; the reader-level detail is given in the
*WordOpenXmlReader* unit chapter.

### Tracked changes render as the accepted view

**Test**: `WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewWithDiagnostic`

Proves inserted runs appear in the model's text and deleted runs do not, and that the tracked-change
count records that a view was chosen. Evidence for `DocDownWord-TrackedChanges`.

### Embedded images are written and linked from the content

**Test**: `DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem`

Proves the embedded PNG is written to `images/` byte-identically to the fixture's source PNG — the
comparison is against the exact bytes the fixture embedded, so an extractor that silently re-encoded
would fail on the bytes — and that the content document links the file with the relative
`](images/` path fragment. Evidence for `DocDownWord-EmbeddedImages`.

### An embedded image's bytes and provenance agree

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the transform hint is passthrough, the media type is `image/png`, the pixel dimensions are
unstated because the stored extent is a display size in EMUs, and the bytes on the way to the sink
are byte-identical to the fixture's source PNG. Evidence for `DocDownWord-ImageProvenance`.

### A vector image is written as-is with a caveat

**Test**: `WordOpenXmlExtractor_Extract_VectorImage_WritesAsIsWithCaveat`

Proves an EMF part is written unchanged and the `WORD0006` diagnostic records that the image
survives in a format many viewers cannot render — the honest complement to the passthrough, mirroring
the PDF package's JPEG 2000 case. Evidence for `DocDownWord-VectorImages`.

### A force-PNG request that cannot be honored is explained

**Test**: `WordOpenXmlExtractor_Extract_ForcePng_ExplainsUnhonoredMode`

Proves both halves of the honest response: the image is still written under its truthful bytes and
type, and `WORD0007` plus a gap targeting `images/` names the requested mode explicitly rather than
mislabeling the format. Evidence for `DocDownWord-ImageOutput`.

### The managed backend declares exactly the deliverable capabilities

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndCapabilities`

Proves the descriptor's identity, priority, supported format, and declared capabilities — text,
embedded images, document metadata, and document structure — match the deliverable set, and — as an
express counterpart — that the rendered-pages capability is not declared. Evidence for
`DocDownWord-DeclaredCapabilities`.

### A page-rendering request degrades with a reasoned gap

**Test**: `WordOpenXmlExtractor_Extract_PagesRequested_DegradesWithReasonedGap`

Proves the outcome degrades, a gap of kind `Pages` names the fact declaratively — that this backend
does not render pages — and the rendered `summary.txt` issues no instruction the reader could act on
and fail at. The forbidden-remedy substring assertion mirrors the PDF precedent. Evidence for
`DocDownWord-NoPageRendering`.

### An empty document degrades with a no-text gap

**Test**: `WordOpenXmlExtractor_Extract_EmptyDocument_DegradesWithNoTextGap`

Proves an empty content document is neither a failure nor an unexplained empty result: the outcome
degrades, `WORD0001` names the missing text, and the full contract layout is still produced.
Evidence for `DocDownWord-EmptyDocument`.

### A per-part request splits at Heading 1

**Test**: `WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Proves per-part mode produces a `parts/` folder containing at least two files for a document with
two top-level headings, so the split honors the request rather than returning a single flow the
caller would then re-segment. Evidence for `DocDownWord-ContentSplitting`.

### A protected document raises a structured signal

**Test**: `WordOpenXmlReader_Read_PasswordProtected_ThrowsWordExtractionException`

Proves the encrypted container is detected and rejected with a `WordExtractionException` whose
message names the condition. The engine's conversion of that signal into a coded, structured
failure with the full layout is evidenced at Core's system level. Evidence for
`DocDownWord-ProtectedDocument`.

### Metadata is surfaced from the document

**Test**: `WordOpenXmlReader_Read_Metadata_TitleAndAuthorSurfaced`

Proves the declared title and author are read from the core properties as the document declared
them, so downstream indexing decisions rest on facts the document itself carries. Evidence for
`DocDownWord-DocumentMetadata`.

### Markdown rendering covers the model's vocabulary

**Test**: `WordMarkdownWriter_Write_Headings_RendersHashLevels`

Proves the heading-level mapping directly; the full per-element detail — lists, escaping, inline
formatting, image links, comments, footnotes, and Document Control placement — is given in the
*Markdown* subsystem chapter and its *WordMarkdownWriter* unit chapter. Evidence for
`DocDownWord-MarkdownRendering`.

### The diagnostic-code table is a pinned contract

**Test**: `WordDiagnosticCodes_Table_MatchesPinnedContract`

Proves the codes a consumer branches on — `WORD0001` through `WORD0009`, with their names — are
exactly the pinned contract, so a renumbering is a build failure rather than a silent surprise.
Evidence for `DocDownWord-DiagnosticCodeStability`.

### A legacy binary document is refused with an honest remedy

**Test**: `DocDownWord_Extract_LegacyDoc_FailsWithUnsupportedFormatRemedy`

Proves an engine with the Word package registered fails a `.doc` request with the
`NoExtractorForFormat` failure kind and a remedy that names the format and states plainly that
DocDown does not support the legacy binary Office formats — with no package named, no environment
precondition, and no install verb, because no such route exists. The full contract layout is still
produced and reconciles cleanly. Evidence for `DocDownWord-LegacyFormatRefusal`.

### Representative summaries match their committed goldens

**Tests**: `WordGolden_Simple_MatchesCommittedGolden`,
`WordGolden_TablesAndImages_MatchesCommittedGolden`,
`WordGolden_MergedCellsGap_MatchesCommittedGolden`,
`WordGolden_DocumentControl_MatchesCommittedGolden`

Prove four representative extractions — a simple two-section document, a document with tables and
images, one whose merged cells produce a counted gap, and an engineering-style document with a
Document Control section — render a `summary.txt` byte-identical to a
committed golden, after the three host-environment lines and the two absolute paths are normalized.
Two runs over the same document with a fixed timestamp produce byte-identical artifacts. Evidence
for `DocDownWord-Determinism`.

### Self-validation cases are exposed and run

**Test**: `WordOpenXmlExtractor_SelfValidation_ReportsCases`

Proves the backend contributes two cases under its category, that the round-trip case genuinely
passes in the environment under test, and that the capability the backend does not claim reports a
skip with a reason rather than a failure — the property a traceability pipeline depends on.
Evidence for `DocDownWord-SelfValidation`.
