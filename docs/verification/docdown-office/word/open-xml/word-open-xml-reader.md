## WordOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlReader`, the unit that
turns an Open XML document into the backend-neutral model.

### Verification Approach

`WordOpenXmlReader` is verified through unit tests in `OpenXml/WordOpenXmlReaderTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, with method names beginning with `WordOpenXmlReader_`.

The unit is driven directly, outside the engine, by opening a fixture stream and calling `Read()`
against a fresh reader instance. This is the level at which the reader's contract lives: the
returned `WordDocumentModel` is what the rest of the subsystem works on, so asserting the model's
shape names one reader behavior rather than one subsystem outcome.

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
tables map into structured blocks; when a table's first row is recognized as a header row when flagged with the
spelling Word writes and is not recognized for a value outside the valid spellings; when a header carrying a
revision and classification builds a
Document Control section; when a footer carrying only page-number fields is omitted from that
section and counted on the model; when an identical header shared across sections is emitted once;
when tracked changes render in the accepted view; when a password-protected container raises
`WordExtractionException`; when a comment's author and text are collected; when a comment's anchor
resolves into a location hint naming its heading and quoting its text, degrading gracefully when
the document anchors it partially or not at all; when title, author, and
producer page count are surfaced from document metadata and omitted where undeclared; when core properties map
into the shared metadata; when footnotes, line and page breaks, nested-table pipes, hyperlink destinations, and
headers differing only by image are handled as documented; when a null stream is rejected and a package without
a main part raises `WordExtractionException`; and when image naming text is chosen from authored sources,
captions, object names, or nearby headings according to the documented order.

### Test Scenarios

#### Styled paragraphs, lists, and tables become structured blocks

**Test**: `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

Proves the model's body carries a Heading 1 block, at least one bulleted list item, and a table
with three rows. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-ReadsStructuredBlocks`.

#### A table's header row is recognized from the flag's real spellings

**Tests**: `WordOpenXmlReader_Read_HeaderRowValTrue_RecognizedAsHeaderRow`,
`WordOpenXmlReader_Read_HeaderRowValMalformed_NotRecognizedAsHeaderRow`

Prove a first row marked `w:tblHeader w:val="true"` — the spelling Word itself writes, which the SDK's typed
accessor rejects — sets `FirstRowIsHeader` on the model's table, and that a row marked with a value outside
the ST_OnOff spellings does not, so a malformed flag is never silently reported as a header row. Supporting
evidence for the structured-block mapping above.

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

`WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewAndCountsChanges`

Proves the flattened body text contains `inserted-text` and does not contain `removed-text`, so
the accepted view is what reached the model, and that both revisions are counted. Evidence for
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

#### An anchored comment's location names its heading and quotes the anchored text

**Test**: `WordOpenXmlReader_Read_AnchoredComment_LocationNamesHeadingAndSnippet`

Proves a comment bracketed by `w:commentRangeStart` and `w:commentRangeEnd` over a run under the
heading `Overview` resolves `Location` to exactly `§Overview — "Body with a comment anchor."`, so a
reader given the comment away from the document can find the text it annotates. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ResolvesCommentLocation`.

#### Each comment's location resolves from its own anchor, or from none

**Test**: `WordOpenXmlReader_Read_MultipleComments_LocationsResolveIndependently`

Proves three comments anchored three different ways resolve independently: a bracketed range under
`Scope` yields the heading plus an ellipsized snippet of the long anchored run; a bare
`w:commentReference` point under `Limitations` brackets no text and degrades to `§Limitations`; and
a comment the document anchors nowhere yields a null `Location` rather than borrowing a neighbour's.
A reader that resolved anchors by position rather than by id, or that invented a location for the
unanchored comment, fails here. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ResolvesCommentLocation` (independent resolution) and
`DocDownWord-OpenXml-WordOpenXmlReader-DegradesCommentLocation`.

#### The anchor snippet accumulator never grows past its stated bound

**Test**: `WordOpenXmlReader_CommentAnchorAppendText_BoundsAccumulatorRegardlessOfRunSize`

Proves the private `CommentAnchor.AppendText` truncates to `SnippetMaxLength` plus one lookahead
character on every call, so neither a single run far longer than the bound nor several runs appended
across calls can grow the accumulator past it. The final rendered `Location` already re-truncates to
the same bound regardless, so this invariant is otherwise unobservable from the public surface; the
test reaches the private type through reflection, following this codebase's existing precedent for
asserting an internal bound the public API cannot exercise directly. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-BoundsCommentSnippet`.

#### A comment carrying no text is dropped, and a picture-only comment is counted

**Tests**: `WordOpenXmlReader_Read_EmptyComment_IsDroppedAndRealCommentSurvives`,
`WordOpenXmlReader_Read_ImageOnlyComment_IsDroppedAndCounted`

Prove a schema-valid `w:comment` carrying no runs never enters the model while a real comment in the
same document does, and that a comment whose only content is a drawing is likewise absent from
`Comments` but counted in `CommentsWithUnreadableContent`. Asserting the surviving comment alongside
the dropped one is what makes the guard falsifiable: a reader that dropped the whole comments part
would pass a test that only checked for absence. The split between dropped-silently and
dropped-and-counted is asserted directly, because a wholly empty comment lost nothing while a drawn
remark did. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-DropsTextlessComments` (the empty-comment test)
and `DocDownWord-OpenXml-WordOpenXmlReader-CountsUnreadableComments` (the picture-only test).

#### The title and author metadata are surfaced

**Test**: `WordOpenXmlReader_Read_Metadata_TitleAndAuthorSurfaced`

Proves the model's `Title` is `Quarterly Report` and its `Author` is `DocDown Test Suite`.
Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-SurfacesMetadata`.

#### A document declaring no metadata reports nothing

**Test**: `WordOpenXmlReader_Read_NoMetadata_ReportsNothing`

Proves a document with no core or extended properties yields an empty title and author and no producer
page count. Evidence for `DocDownWord-OpenXml-WordOpenXmlReader-OmitsUndeclaredMetadata`.

#### Core properties map into the shared metadata

**Test**: `WordOpenXmlReader_Read_CoreProperties_MapsSharedMetadata`

Proves the title, subject, keywords, and last-modified-by core properties appear as fields of the shared
document metadata. Evidence for `DocDownWord-DocumentMetadata`.

#### The producer-stated page count is reported

**Test**: `WordOpenXmlReader_Read_ProducerPageCount_FromExtendedProperties`

Proves the model's `ProducerPageCount` is `7`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlReader-ReportsProducerPageCount` and `DocDownWord-ProducerPageCount`.

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

#### A footnote is collected as a marker and a body

**Test**: `WordOpenXmlReader_Read_Footnote_CollectsMarkerAndBody`

Proves a footnote reference becomes an inline marker and its body is collected for the Footnotes
section. Evidence for `DocDownWord-Footnotes`.

#### A package without a main part raises a Word extraction exception

**Test**: `WordOpenXmlReader_Read_MissingMainPart_ThrowsWordExtractionException`

Proves a zip package lacking a main document part makes `Read()` throw `WordExtractionException`
rather than a raw parser exception. Evidence for `DocDownWord-MissingMainPart`.

#### A null stream is rejected

**Test**: `WordOpenXmlReader_Read_NullStream_ThrowsArgumentNullException`

Proves `Read(null)` throws `ArgumentNullException`. Evidence for
`DocDownWord-ArgumentAndCancellationHandling`.

#### Line and page breaks are read as break blocks

**Test**: `WordOpenXmlReader_Read_LineAndPageBreaks_RenderHardBreakAndPageBreakBlock`

Proves a line break becomes a hard break and a page break becomes a page-break block. Evidence for
`DocDownWord-BreakRendering`.

#### Nested-table pipes, hyperlink destinations, and same-text headers

**Tests**: `WordOpenXmlReader_Read_NestedTableWithPipe_EscapesEveryPipeInFlattenedCell`,
`WordOpenXmlReader_Read_HyperlinkWithParentheses_EscapesDestination`,
`WordOpenXmlReader_Read_SameHeaderTextDifferentImages_KeepsBothHeaders`

Prove every pipe in a flattened nested cell is escaped, parentheses in a hyperlink destination are
escaped, and two headers with identical text but different images are both kept. Evidence for
`DocDownWord-NestedTableEscaping`, `DocDownWord-LinkDestinationEscaping`, and
`DocDownWord-HeaderImageDistinction`.
