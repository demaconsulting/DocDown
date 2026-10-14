# DemaConsulting.DocDown.Word Verification Design

This document describes the subsystem-level verification strategy for `DemaConsulting.DocDown.Word`, the Word
extraction subsystem.

## Verification Approach

`DemaConsulting.DocDown.Word` is verified through subsystem-level integration tests in `DocDownWordTests.cs` and
`WordGoldenTests.cs`, and through subsystem and unit tests in `DemaConsulting.DocDown.Office.Tests`,
running on xUnit v3 across net8.0, net9.0, and net10.0.

### Extraction scenarios prove the expected layout is written

Most subsystem-level extraction scenarios end with `ContractAssert.LayoutPresent`, which proves the
expected output layout exists on disk for the same run whose content, notes, metadata, or summary
is being asserted. This is the highest-value assertion at subsystem level: a successful or unreadable
result still has to produce the layout the rest of DocDown expects. The exceptions are
`DocDownWord_Extract_Docx_SelectsOpenXml`, which asserts only which backend was selected,
`DocDownWord_Extract_SameDocumentTwice_ProducesIdenticalOutput`, which compares artifact bytes between two runs,
and the golden tests, which compare the normalized `summary.txt` against a committed golden instead.

### One backend, one reader, one emitter

The subsystem ships one backend: the managed Open XML reader. It reads a document into the
reader-neutral model and drives the shared emitter, so every mapping decision is made in one place
and can be proved either from a hand-built model or from a real extraction. Legacy binary `.doc`
content has no reader here and none anywhere in DocDown, so the request produces an unreadable
result with a plain explanation rather than an exception surfacing to the caller.

### Content inventory and short notes are both exercised

Most of the reporting behavior under test now falls into one of two buckets. The content inventory
states what reached the output, including deliberate zero counts for categories the extractor
looked for. Short extraction notes are reserved for attempted steps the extractor could not
complete, such as chart-part reading, flattened merged or nested table structure, or comments whose
only content is a picture or ink. The suite
therefore covers both clean extractions and honest incomplete-step reporting.

### Test fixtures are generated; the self-test probe is committed

Every document the suite's tests use is built at test time by the Open XML SDK writer in
`TestData/DocxFixtures.cs`, orchestrated through `TestData/WordTestHarness.cs`. The legacy `.doc`
scenario writes a placeholder byte sequence whose extension drives format detection, because the
selection path never opens the file. No test fixture is committed.
The one committed binary is the backend's self-test probe: a real document authored in Microsoft
Word, embedded in the subsystem so the self-test reads what that application emits.

### Golden summaries pin representative outputs

Five representative extractions render `summary.txt` with the single managed backend, normalize
the machine-specific lines (including the run-varying timestamp), and compare the remainder byte for
byte against committed
goldens under `test/.../golden/`. Setting `DOCDOWN_UPDATE_GOLDEN=1` rewrites a golden, and
regeneration is refused in CI.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input document and the
  extraction output
- **Inputs**: WordprocessingML documents generated at test time by the Open XML SDK writer, plus a
  short byte sequence for the legacy `.doc` unreadable scenario; no committed binary fixtures and
  no network access
- **Mocking**: none — every subsystem test drives the real engine and the real Open XML backend
- **Determinism**: the golden tests normalize the run-varying timestamp line; the repeat-run scenario compares
  `content.md`, `metadata.json`, and the image file names, which carry no timestamp
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a subsystem-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception reaching the caller.
- A generated `.docx` produces the expected DocDown layout, real markdown table output, a Document
  Control section, `ExtractionOutcome.Produced`, and no extraction notes for the clean scenario.
- The summary and manifest make reviewer comments discoverable through content inventory rather than
  only through a character count.
- Reviewer comments are written to `review-comments.md` and are not rendered into `content.md`, and a
  document with an empty comment still produces its content and its real comments.
- A document embedding a chart produces `ExtractionOutcome.Produced` together with one short note
  explaining that chart parts were not read.
- Embedded images are written and linked from the content, and a vector image is written unchanged
  when the document stores one.
- A legacy binary `.doc` produces `ExtractionOutcome.Unreadable` with a plain unsupported-format
  explanation and the expected output layout.
- Five representative summaries match their committed goldens after normalization.
- Merged or nested table structure, charts, and comments whose only content is a picture are each reported as a
  short note; an empty document is reported as a zero-count text inventory entry; tracked changes render in the
  accepted view; and a vector image is written unchanged.
- The managed extractor reports itself available and states that rendered pages are not provided, and its
  self-test cases pass and skip as documented.
- Headings render as markdown heading levels, with the markup-escaping, list, inline-formatting, and footnote
  behavior proved at the unit level.
- Null arguments are rejected, a cancelled token stops the extraction, and declared title, author, and producer
  page count are reported while undeclared metadata is omitted.

## Test Scenarios

Each scenario corresponds to one subsystem requirement or provides supporting subsystem evidence.
Platform requirements are covered by the source-filtered runs of the clean-layout scenario.

### The default engine registers the Word backend

**Test**: `AddWord_OnBuilder_RegistersOpenXmlBackend`

Proves the one-liner a host uses to add Word support registers the Open XML backend and registers
nothing else. Evidence for `DocDownWord-Registration`.

### Contract layout is produced for a generated document

**Test**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`

Proves a clean extraction: the expected layout, a real GFM table in `content.md`, a
`## Document Control` heading, the Open XML backend selected, `ExtractionOutcome.Produced`, and no
notes. Evidence for `DocDownWord-Extraction`, `DocDownWord-Tables`, and under source filters the
six `DocDownWord-Platform-*` requirements.

### Reviewer comments are named in the summary outline

**Test**: `DocDownWord_Extract_DocxWithComment_SummaryOutlineNamesTheComments`

Proves the summary and manifest name the document's comments and distinct comment authors, so a
consumer can tell from the inventory that reviewer commentary is present. Supporting evidence for
the Markdown subsystem's content-inventory behavior.

### Reviewer comments are routed to review-comments.md, not content

**Test**: `DocDownWord_Extract_DocxWithComment_WritesReviewCommentsNotContent`

Proves a reviewer comment appears in `review-comments.md` with its author and is absent from
`content.md`. Supporting evidence for the Markdown subsystem's comment-reporting behavior.

### A document with an empty comment still produces its content

**Test**: `DocDownWord_Extract_DocxWithEmptyComment_StillProducesContentAndRealComment`

Proves a comment with no text is dropped by the reader rather than aborting the extraction, while the
document's content and its real comment are still produced. Supporting evidence for the OpenXml
subsystem's comment-reading behavior.

### Document Control is preserved from an engineering-style document

**Test**: `DocDownWord_Extract_EngineeringStyleDocx_PreservesRevisionInDocumentControl`

Proves the extracted content contains `## Document Control`, preserves the revision and
classification text, and does not surface page-number furniture as body-like content. Evidence for
`DocDownWord-DocumentControl`. The placement requirement `DocDownWord-DocumentControlPlacement` is proved by
`WordMarkdownWriter_Write_DocumentControl_PlacedAfterTitleHeading`.

### Embedded charts are reported as a short extraction note

**Test**: `DocDownWord_Extract_DocxWithChart_ReportsChartNote`

Proves a document embedding a chart still completes as `Produced` and reports one note explaining
that the extractor does not read chart parts, so chart data does not vanish silently. Evidence for
`DocDownWord-ChartNotes`.

### Embedded images are written and linked from the content

**Test**: `DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem`

Proves the embedded PNG is written byte-identically to the fixture source and linked from the
content with an `images/` path. Evidence for `DocDownWord-EmbeddedImages` and
`DocDownWord-ImageProvenance`.

### The managed backend is selected for a modern document

**Test**: `DocDownWord_Extract_Docx_SelectsOpenXml`

Proves a modern Word document selects the managed Open XML backend. Supporting evidence for the
OpenXml subsystem selection path.

### A legacy binary document is reported unreadable with a plain explanation

**Test**: `DocDownWord_Extract_LegacyDoc_IsUnreadableWithUnsupportedFormatExplanation`

Proves an engine with the Word subsystem registered returns `ExtractionOutcome.Unreadable` for a
`.doc`, states plainly that DocDown does not support the legacy binary Office formats, and still
produces the expected layout. Evidence for `DocDownWord-LegacyFormatUnreadable`.

### Representative summaries match their committed goldens

**Tests**:

- `WordGolden_Simple_MatchesCommittedGolden`
- `WordGolden_TablesAndImages_MatchesCommittedGolden`
- `WordGolden_MergedCellsFlattened_MatchesCommittedGolden`
- `WordGolden_DocumentControl_MatchesCommittedGolden`
- `WordGolden_ReviewComments_MatchesCommittedGolden`

Prove five representative extractions — a simple two-section document, a document with tables and
images, one whose merged cells produce a flattening note, an engineering-style document with a
Document Control section, and a document with reviewer comments — render a normalized `summary.txt`
that is byte-identical to its committed golden. Evidence for
`DocDownWord-Determinism`, which the golden comparison supports as a one-run comparison of the
normalized summary rather than a repeated-run comparison of every artifact.

### Footnotes are carried into the content

**Tests**:

- `WordOpenXmlReader_Read_Footnote_CollectsMarkerAndBody`
- `WordOpenXmlExtractor_Extract_Footnote_WritesMarkerAndFootnotesSection`

Prove a footnote reference renders as an inline marker and its body appears under a Footnotes
section. Evidence for `DocDownWord-Footnotes`.

### A package without a main part is unreadable

**Tests**:

- `WordOpenXmlReader_Read_MissingMainPart_ThrowsWordExtractionException`
- `WordOpenXmlExtractor_Extract_MissingMainPart_IsUnreadable`

Prove a zip package lacking a main document part raises `WordExtractionException` at the reader and
is reported unreadable by the extractor. Evidence for `DocDownWord-MissingMainPart`.

### Null arguments are rejected

**Tests**:

- `WordOpenXmlReader_Read_NullStream_ThrowsArgumentNullException`
- `WordOpenXmlExtractor_Extract_NullArguments_Throw`
- `WordOpenXmlImageReader_Resolve_NullArguments_Throw`

Prove null arguments throw `ArgumentNullException`. Evidence for `DocDownWord-ArgumentAndCancellationHandling`.

### A cancelled token stops the extraction

**Test**: `WordOpenXmlExtractor_Extract_CancelledToken_ThrowsOperationCanceled`

Proves a cancelled token makes the extractor throw `OperationCanceledException` rather than return a
partial extraction. Evidence for `DocDownWord-CancellationHandling`.

### Breaks render as rules and hard line breaks

**Tests**:

- `WordOpenXmlReader_Read_LineAndPageBreaks_RenderHardBreakAndPageBreakBlock`
- `WordOpenXmlExtractor_Extract_PageBreak_WritesHorizontalRule`
- `WordTableWriter_Write_CellWithHardBreakRun_BecomesSingleBreak`

Prove a line break renders as a hard break, a page break as a horizontal rule, and a hard break
inside a table cell as one `<br>`. Evidence for `DocDownWord-BreakRendering`.

### Flattened nested-table text escapes pipes

**Test**: `WordOpenXmlReader_Read_NestedTableWithPipe_EscapesEveryPipeInFlattenedCell`

Proves every pipe in a flattened nested cell is escaped. Evidence for
`DocDownWord-NestedTableEscaping`.

### Headers with different images stay distinct

**Test**: `WordOpenXmlReader_Read_SameHeaderTextDifferentImages_KeepsBothHeaders`

Proves headers with identical text but different image bytes are both kept. Evidence for
`DocDownWord-HeaderImageDistinction`.

### Hyperlink destinations are escaped

**Test**: `WordOpenXmlReader_Read_HyperlinkWithParentheses_EscapesDestination`

Proves parentheses in a destination are percent-encoded. Evidence for
`DocDownWord-LinkDestinationEscaping`.

### Images resolve from the correct part

**Tests**:

- `WordOpenXmlImageReader_Resolve_HeaderScopedImage_ResolvesAgainstHeaderPart`
- `WordOpenXmlImageReader_Resolve_MissingOrNonImageRelationship_ReturnsNull`
- `WordOpenXmlImageReader_Resolve_Candidates_RankAndGateAltText`

Prove header images resolve against the header part, missing or non-image relationships resolve to
nothing, and naming candidates are ranked with alt text gated. Evidence for `DocDownWord-ImageResolution`,
`DocDownWord-ImageRelationshipValidation`, and `DocDownWord-ImageNamingCandidateRanking` respectively.

### Protected, metadata, repeat-run, and plural chart cases

**Tests**:

- `DocDownWord_Extract_ProtectedDocx_IsUnreadableWithPasswordExplanation`
- `DocDownWord_Extract_DocxWithCoreProperties_WritesDocumentMetadata`
- `WordOpenXmlReader_Read_CoreProperties_MapsSharedMetadata`
- `DocDownWord_Extract_SameDocumentTwice_ProducesIdenticalOutput`
- `DocDownWord_Extract_DocxWithTwoCharts_ReportsPluralChartNote`

Prove a protected document is unreadable with an explanation, core properties reach
`metadata.json`, two extractions of one document are identical, and two charts yield a plural note.
Evidence for `DocDownWord-ProtectedDocument`, `DocDownWord-DocumentMetadata`,
`DocDownWord-Determinism`, and `DocDownWord-ChartNotes`. The repeat-run comparison covers `content.md`,
`metadata.json`, and the image file names.

### Producer page count and undeclared metadata

**Tests**: `WordOpenXmlReader_Read_ProducerPageCount_FromExtendedProperties`,
`WordOpenXmlReader_Read_NoMetadata_ReportsNothing`

Prove the page count is taken from the producer-stated extended property and that a document declaring no
title, author, or page count yields none of them. Evidence for `DocDownWord-ProducerPageCount` and
`DocDownWord-UndeclaredMetadataOmitted`.

### Merged or nested table structure is reported as a note

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsFlatteningNote`

Proves a document with merged cells completes as `Produced` and records a short note stating that the table
structure was flattened.
Evidence for `DocDownWord-TableStructureNotes`.

### Tracked changes render in the accepted view

**Test**: `WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewAndCountsChanges`

Proves inserted text is kept, deleted text is dropped, and the changes are counted. Evidence for
`DocDownWord-TrackedChangesAcceptedView`.

### A vector image is written as stored

**Test**: `WordOpenXmlExtractor_Extract_VectorImage_WritesAsIs`

Proves an EMF image is written to `images/` with no caveat note and the run reports
`ExtractionOutcome.Produced`; byte identity is proved at the unit level. Evidence for
`DocDownWord-VectorImagesWrittenAsIs`.

### The managed extractor reports availability

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndManagedAvailability`

Proves the descriptor reports the `.docx` format, priority 10, availability with no unavailable reason,
and that rendered pages are not provided. Evidence for `DocDownWord-ManagedAvailability` and
`DocDownWord-PageRenderingNotProvided`.

### An empty document reports a zero-count text inventory

**Test**: `WordOpenXmlExtractor_Extract_EmptyDocument_ProducesZeroCountTextInventory`

Proves an empty document still produces normal output with a zero-count text-block inventory entry and
no failure. Evidence for `DocDownWord-EmptyDocumentInventory`.

### Headings render as markdown heading levels

**Test**: `WordMarkdownWriter_Write_Headings_RendersHashLevels`

Proves heading styles render as hash levels. Evidence for `DocDownWord-MarkdownRendering`; the list,
inline-formatting, escaping, and footnote behavior it also names is proved by the Markdown unit
tests cited in the unit verification documents.

### The extractor contributes self-validation cases

**Test**: `WordOpenXmlExtractor_SelfValidation_ReportsCases`

Proves the backend contributes two cases, one that passes by reading back its embedded probe document
and one that is skipped for page rendering, with no failures. Evidence for `DocDownWord-SelfValidation`.
