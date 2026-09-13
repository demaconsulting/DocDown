## WordOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlReader`, the unit that
turns an Open XML document into the backend-neutral model.

### Verification Approach

`WordOpenXmlReader` is verified through unit tests in `OpenXml/WordOpenXmlReaderTests.cs` in
`DemaConsulting.DocDown.Word.Tests`, with method names beginning with `WordOpenXmlReader_`.

The unit is driven directly, outside the engine, by opening a fixture stream and calling `Read`
against a fresh reader instance. This is the level at which the reader's contract lives: the
returned `WordDocumentModel` is what the rest of the package works on, so asserting the model's
shape names one reader behavior rather than one system outcome.

The SDK is **not** mocked. Every fixture is a real `.docx` built at test time by the Open XML SDK
writer in `TestData/DocxFixtures.cs`, so the unit is exercised against genuine documents rather
than against a simulation of one. This matters especially for the header-and-footer scenarios,
because the reader's job is to detect field instructions in real WordprocessingML markup, and a
hand-built model would not exercise that detection at all.

The protected-document scenario asserts the exception type and its message text — `password-protected`
appears in the message — because a `WordExtractionException` with a generic message would defeat
the structured-failure contract the engine relies on. Detection is by the container signature, not
by reading anything the document declares, so the scenario uses a fixture whose bytes are an OLE
compound file rather than a Zip.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by `TestData/DocxFixtures.cs`, plus
  a short OLE-compound-file byte sequence for the protected-container scenario
- **Filesystem**: none; each fixture is read from a `MemoryStream`
- **Mocking**: none; the SDK is exercised against real documents
- **Isolation**: each test constructs its own reader and its own stream

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlReader` unit test run passes when styled paragraphs, lists, and
tables map into structured blocks with the correct kinds and depth; when a header carrying a
revision and classification builds a Document Control section whose blocks contain those strings;
when a footer that carries only page-numbering fields is omitted with the count and reason
recorded on the model; when a header shared across two sections is emitted once; when tracked
changes render in the accepted view with the tracked-change count recorded; when a
password-protected container raises `WordExtractionException` whose message names the condition;
when a comment's author and text are collected; when the declared title and author surface on the
model; and when the producer-stated page count is read from the extended properties. Any block
kind that flattens, any silent furniture omission, a header duplicated across sections, a deletion
retained in the accepted view, or a metadata value that appears where the document declared none is
a failure.

### Test Scenarios

#### Styled paragraphs, lists, and tables become structured blocks

**Test**: `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

Proves the model's body carries a Heading 1 block, at least one bulleted list item, and a Table
block whose model has `FirstRowIsHeader` set and three rows. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReadsStructuredBlocks`.

#### A revision-and-classification header produces a Document Control section

**Test**: `WordOpenXmlReader_Read_HeaderWithRevisionAndClassification_ProducesDocumentControlSection`

Proves a `Header` subsection appears in the model's Document Control collection and its blocks
contain the strings `Revision 2.1` and `CONFIDENTIAL` — the identifying furniture a reader expects
near the top. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-BuildsDocumentControl`.

#### A page-number-only footer is omitted as page furniture

**Test**: `WordOpenXmlReader_Read_FooterWithOnlyPageNumberFields_OmitsAsFurniture`

Proves the Document Control collection is empty, the page-furniture count is one, and the empty
count is zero. Detection is by the field instruction rather than by a regex over rendered text, so
the assertion here is the semantic one — the reader recognized a page-numbering field and dropped
its part as furniture, which the emitter later surfaces as an informational diagnostic rather than
a gap. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-OmitsFurnitureAsDiagnostic`.

#### An identical header across sections is emitted once

**Test**: `WordOpenXmlReader_Read_IdenticalHeaderAcrossSections_EmittedOnce`

Proves the Document Control collection carries one entry, and that the found-parts count is at
least two — so the reader saw both section references and consciously deduplicated them rather than
missing one. The pair of assertions is what keeps a deduplication indistinguishable from a
misdetection. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-DeduplicatesHeaders`.

#### Tracked changes render as the accepted view

**Test**: `WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewWithDiagnostic`

Proves the flattened body text contains `inserted-text` and does not contain `removed-text`, and
that the tracked-change count is two — the number of changes across insertion and deletion the
reader consumed to produce the accepted view. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-RendersAcceptedRevisions`.

#### A password-protected document raises a Word extraction exception

**Test**: `WordOpenXmlReader_Read_PasswordProtected_ThrowsWordExtractionException`

Proves `Read` throws `WordExtractionException` whose message contains `password-protected`
(case-insensitive) when given an OLE compound file rather than a Zip package. Detecting the
container signature is what keeps a protected document from raising an SDK exception the engine's
structured-failure conversion would not recognize. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-DetectsPasswordProtected`.

#### A comment's author and text are collected

**Test**: `WordOpenXmlReader_Read_Comment_CollectsAuthorAndText`

Proves the model carries one comment whose `Author` is `Reviewer` and whose flattened content
contains `clarify`. Both halves matter — the identity of the reviewer and what they said — so both
are asserted in the same scenario. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-CollectsComments`.

#### The title and author metadata are surfaced

**Test**: `WordOpenXmlReader_Read_Metadata_TitleAndAuthorSurfaced`

Proves the model's `Title` is `Quarterly Report` and its `Author` is `DocDown Test Suite` — the
strings the fixture's core properties declare. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-SurfacesMetadata`.

#### The producer-stated page count is reported

**Test**: `WordOpenXmlReader_Read_ProducerPageCount_FromExtendedProperties`

Proves the model's `ProducerPageCount` is `7` — the value the fixture's extended-properties `Pages`
element declares. Reading only the producer's own value, not a computed count, is what keeps the
page count honest for a format that does not paginate itself. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReportsProducerPageCount`.

#### An image's naming and description text is chosen from authored sources

**Tests**: `WordOpenXmlReader_Read_ImageWithDescription_UsesDescriptionForNameAltAndManifest`,
`WordOpenXmlReader_Read_ImageWithTitleOnly_UsesTitleAsDescriptive`,
`WordOpenXmlReader_Read_ImageWithObjectName_UsesPictureNameNotDocPrName`

Proves the reader chooses an image's naming hint, alt text, and manifest description from the author's
`wp:docPr/@descr`, then the drawing title, then the `pic:cNvPr/@name` object name — and never from the
auto-generated `wp:docPr/@name`, which the fixture sets to `Picture 1` precisely so a regression that
read it would fail. Each authored source is marked descriptive so it may serve as alt text. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-SelectsImageText`.

#### A caption's SEQ number is read structurally

**Test**: `WordOpenXmlReader_Read_ImageWithCaption_ReadsSeqNumberStructurally`

Proves an adjacent `Caption`-styled paragraph whose figure number is a `SEQ` field result reads as
`Figure 1: Widget assembly` — the number surfaced through the field-aware walker as a field result, not
by matching digits in rendered text. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReadsCaptionStructurally`.

#### A heading names an image but is never asserted as its description

**Tests**: `WordOpenXmlReader_Read_ImageWithOnlyHeading_NamesFromHeadingButKeepsNeutralAlt`,
`WordOpenXmlReader_Read_ImageWithNoSources_FallsBackToMediaNameWithoutDescription`

Proves the honesty rule at the reader boundary: an image whose only source is a nearby heading takes the
heading as its file-naming hint and records it in the manifest with a `heading` source, yet leaves the
alt text absent so the writer emits a neutral placeholder; an image with no source at all falls back to
the media name with no description. A heading such as "References" is therefore never presented as a
photo's description. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-HeadingNamesButNeutralAlt`.
