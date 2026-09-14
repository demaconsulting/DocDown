## WordOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlReader`, the unit that
turns an Open XML document into the backend-neutral model.

### Verification Approach

`WordOpenXmlReader` is verified through unit tests in `OpenXml/WordOpenXmlReaderTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, with method names beginning with `WordOpenXmlReader_`.

The unit is driven directly, outside the engine, by opening a fixture stream and calling `Read()`
against a fresh reader instance. This is the level at which the reader's contract lives: the
returned `WordDocumentModel` is what the rest of the package works on, so asserting the model's
shape names one reader behavior rather than one system outcome.

The SDK is not mocked. Every fixture is a real `.docx` built at test time by the Open XML SDK
writer in `TestData/DocxFixtures.cs`, so the unit is exercised against genuine documents rather
than against a simulation of one.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by `TestData/DocxFixtures.cs`, plus
  a short OLE-compound-file byte sequence for the protected-container scenario
- **Filesystem**: none; each fixture is read from a `MemoryStream`
- **Mocking**: none; the SDK is exercised against real documents
- **Isolation**: each test constructs its own reader and stream

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlReader` test run passes when styled paragraphs, lists, and
tables map into structured blocks; when a header carrying a revision and classification builds a
Document Control section; when a footer carrying only page-number fields is omitted from that
section and counted on the model; when an identical header shared across sections is emitted once;
when tracked changes render in the accepted view; when a password-protected container raises
`WordExtractionException`; when a comment's author and text are collected; when title, author, and
producer page count are surfaced from document metadata; and when image naming text is chosen from
authored sources, captions, object names, or nearby headings according to the documented order.

### Test Scenarios

#### Styled paragraphs, lists, and tables become structured blocks

**Test**: `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

Proves the model's body carries a Heading 1 block, at least one bulleted list item, and a table
with three rows. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-ReadsStructuredBlocks`.

#### A revision-and-classification header produces a Document Control section

**Test**: `WordOpenXmlReader_Read_HeaderWithRevisionAndClassification_ProducesDocumentControlSection`

Proves a `Header` subsection appears in the model's Document Control collection and its blocks
contain `Revision 2.1` and `CONFIDENTIAL`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-BuildsDocumentControl`.

#### A page-number-only footer is omitted from Document Control

**Test**: `WordOpenXmlReader_Read_FooterWithOnlyPageNumberFields_OmitsAsFurniture`

Proves the Document Control collection is empty, the page-furniture count is one, and the empty
count is zero. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-OmitsPageFurnitureFromDocumentControl`.

#### An identical header across sections is emitted once

**Test**: `WordOpenXmlReader_Read_IdenticalHeaderAcrossSections_EmittedOnce`

Proves the Document Control collection carries one entry, and that the found-parts count is at
least two. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-DeduplicatesHeaders`.

#### Tracked changes render as the accepted view

**Test**:

`WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewWithDiagnostic`

Proves the flattened body text contains `inserted-text` and does not contain `removed-text`, so
the accepted view is what reached the model. Evidence for
`DocDownWord-TrackedChangesAcceptedView` and
`DocDownWord-OpenXml-WordOpenXmlReader-RendersAcceptedView`.

#### A password-protected document raises a Word extraction exception

**Test**: `WordOpenXmlReader_Read_PasswordProtected_ThrowsWordExtractionException`

Proves `Read()` throws `WordExtractionException` whose message contains `password-protected`
case-insensitively when given an OLE compound file rather than a Zip package. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-DetectsPasswordProtected`.

#### A comment's author and text are collected

**Test**: `WordOpenXmlReader_Read_Comment_CollectsAuthorAndText`

Proves the model carries one comment whose `Author` is `Reviewer` and whose flattened content
contains `clarify`. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-CollectsComments`.

#### The title and author metadata are surfaced

**Test**: `WordOpenXmlReader_Read_Metadata_TitleAndAuthorSurfaced`

Proves the model's `Title` is `Quarterly Report` and its `Author` is `DocDown Test Suite`.
Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-SurfacesMetadata`.

#### The producer-stated page count is reported

**Test**: `WordOpenXmlReader_Read_ProducerPageCount_FromExtendedProperties`

Proves the model's `ProducerPageCount` is `7`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReportsProducerPageCount`.

#### An image's naming and description text is chosen from authored sources

**Tests**: `WordOpenXmlReader_Read_ImageWithDescription_UsesDescriptionForNameAltAndManifest`,
`WordOpenXmlReader_Read_ImageWithTitleOnly_UsesTitleAsDescriptive`,
`WordOpenXmlReader_Read_ImageWithObjectName_UsesPictureNameNotDocPrName`

Prove the reader chooses an image's naming hint, alt text, and manifest description from the
author's description, drawing title, or picture object name, and never from the auto-generated
drawing name. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-SelectsImageText`.

#### A caption's SEQ number is read structurally

**Test**: `WordOpenXmlReader_Read_ImageWithCaption_ReadsSeqNumberStructurally`

Proves an adjacent caption whose figure number is a `SEQ` field result reads as
`Figure 1: Widget assembly`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReadsCaptionStructurally`.

#### A heading names an image but does not become its alt text

**Tests**: `WordOpenXmlReader_Read_ImageWithOnlyHeading_NamesFromHeadingButKeepsNeutralAlt`,
`WordOpenXmlReader_Read_ImageWithNoSources_FallsBackToMediaNameWithoutDescription`

Prove a nearby heading may seed the file name and manifest description while the alt text remains
neutral, and that an image with no text source falls back to its media name. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-HeadingNamesButNeutralAlt`.
