### PowerPointOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `PowerPointOpenXmlReader`, the
SDK-to-model translation for a `.pptx`.

### Verification Approach

`PowerPointOpenXmlReader` is verified through unit tests in `OpenXml/PowerPointOpenXmlReaderTests.cs`
in `DemaConsulting.DocDown.Office.Tests`, exercised against decks synthesized at test time by
`TestData/PptxFixtures.cs`. Each test reads a generated deck from memory and asserts one facet of the
translation: slide order and ordinals, titles, body text, speaker notes, null notes for a notes-less
deck, and the embedded image with its slide association.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PresentationML decks generated at test time; every title, body line, speaker note, and
  image name is synthetic
- **Filesystem**: none; each deck is read from an in-memory stream
- **Mocking**: none; the SDK is exercised against real generated decks
- **Isolation**: each test builds its own deck

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointOpenXmlReader` unit test run passes when the reader returns the
slides in presentation order with their 1-based ordinals and titles; extracts each slide's body text
lines separate from the title; reads the speaker notes from the notes body placeholder; yields null
notes for a deck with no notes; and resolves an embedded image with its bytes, media type, naming
source, and referring slide. Any reordered or dropped slide, misread or truncated notes, contaminated
narration, or lost slide association is a failure.

### Test Scenarios

#### Slides are returned in presentation order

**Test**: `PowerPointOpenXmlReader_Read_DeckWithNotes_ReturnsSlidesInOrder`

Proves the slides come out in presentation order with their 1-based ordinals and their titles.
Evidence for `DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-PreservesSlideOrder` and
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ExtractsTitle`.

#### Slide body text is extracted separate from the title

**Test**: `PowerPointOpenXmlReader_Read_Slide_ExtractsBodyTextLines`

Proves the reader extracts a slide's body text lines from its non-title shapes, and that the title
text does not appear among them. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ExtractsBodyText`.

#### Speaker notes are extracted from the notes body placeholder

**Test**: `PowerPointOpenXmlReader_Read_Slide_ExtractsSpeakerNotes`

Proves the reader reads each slide's speaker notes from the notes slide's body placeholder, recovering
the narration no render can supply. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ExtractsSpeakerNotes`.

#### A notes-less deck yields null notes

**Test**: `PowerPointOpenXmlReader_Read_DeckWithoutNotes_YieldsNullNotes`

Proves a deck with no speaker notes yields slides whose notes are null, so a genuine absence is
distinguishable from a missed read. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-YieldsNullNotesWhenAbsent`.

#### A legacy slide comment is read with its author resolved

**Test**: `PowerPointOpenXmlReader_Read_DeckWithComment_ReadsCommentWithResolvedAuthor`

Proves a slide's legacy comments part reaches the model with the comment text and the author name
resolved through the presentation's comment-author list rather than the bare numeric identifier the
comment itself carries. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ResolvesCommentAuthors`.

#### Comments stay with the slide that carries them

**Test**: `PowerPointOpenXmlReader_Read_DeckWithCommentsOnTwoSlides_AttributesCommentsToOwningSlide`

Proves a two-slide deck with a different comment and author on each slide reports each remark against the
slide ordinal that carries it, so attribution is proven rather than assumed from a single-slide case.
Evidence for `DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsSlideComments`.

#### A deck with no comments part yields no comments

**Test**: `PowerPointOpenXmlReader_Read_DeckWithoutComments_YieldsNoComments`

Proves the ordinary case — a deck nobody reviewed — yields slides with no comments rather than an error.
Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsSlideComments` and
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ExcludesModernPersonaComments`.

#### Modern persona comments are counted without being read

**Tests**: `PowerPointOpenXmlReader_Read_DeckWithModernComments_CountsThemWithoutReadingThem`,
`PowerPointOpenXmlReader_Read_DeckWithComment_CountsNoModernComments`

Prove a deck carrying a `PowerPointCommentPart` and no legacy comments part yields no comments while
reporting `ModernCommentCount` and `ModernCommentSlideCount`, and that an ordinary legacy-commented
deck reports zero modern comments. The pair is what makes the boundary observable rather than merely
documented: before this, such a deck was indistinguishable from one nobody commented on. Asserting
the legacy deck's zero alongside the modern deck's count is what keeps the counter from becoming a
count of comments in general. Nothing asserts any modern comment's text or author, because the
reader deliberately reads neither. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-CountsModernPersonaComments` and
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ExcludesModernPersonaComments`.

#### Table text is read as one line per row

**Test**: `PowerPointOpenXmlReader_Read_DeckWithTable_ReadsRowsAsLines`

Proves a table held in a graphic frame reaches the slide's text lines as one line per row, with the
cells of a row joined by a pipe in reading order. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsTableText`.

#### Grouped shape text is read, including nested groups

**Test**: `PowerPointOpenXmlReader_Read_DeckWithGroupedShapes_ReadsNestedGroupText`

Proves text in a group shape and in a group nested inside it is read in shape-tree order. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsGroupedShapeText`.

#### SmartArt node text is read

**Test**: `PowerPointOpenXmlReader_Read_DeckWithSmartArt_ReadsNodeText`

Proves a SmartArt diagram's node text is resolved through its diagram data part and reaches the
slide's text lines. Evidence for `DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsSmartArtText`.

#### A line break reads as a space and field text is kept

**Test**: `PowerPointOpenXmlReader_Read_DeckWithBreakAndField_KeepsBreakAndFieldText`

Proves the words either side of a soft line break stay apart and a field's text appears in the line.
Evidence for `DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-ReadsBreakAndFieldText`.

#### A corrupt package is wrapped

**Test**: `PowerPointOpenXmlReader_Read_CorruptPackage_ThrowsExtractionException`

Proves bytes that are not an Open XML package raise `PowerPointExtractionException`. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-WrapsUnreadablePackage`.

#### A null stream is rejected

**Test**: `PowerPointOpenXmlReader_Read_NullStream_Throws`

Proves a null stream raises `ArgumentNullException`. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlReader-RejectsNullStream`.
