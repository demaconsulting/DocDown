### PowerPointOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `PowerPointOpenXmlReader`, the
SDK-to-model translation for a `.pptx`.

### Verification Approach

`PowerPointOpenXmlReader` is verified through unit tests in `OpenXml/PowerPointOpenXmlReaderTests.cs`
in `DemaConsulting.DocDown.PowerPoint.Tests`, exercised against decks synthesized at test time by
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
