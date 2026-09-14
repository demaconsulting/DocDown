### PowerPointContentEmitter Verification Design

This document describes the unit-level verification strategy for `PowerPointContentEmitter`, the
single emission path from the deck model to the output contract.

### Verification Approach

`PowerPointContentEmitter` is verified through unit tests in
`Markdown/PowerPointContentEmitterTests.cs` in `DemaConsulting.DocDown.PowerPoint.Tests`, driven from
hand-built deck models through a recording sink. Building the model by hand is what lets each mapping
decision be proved in isolation: the per-slide title, text, and notes; the inline image links and
their suppression; the speaker-notes inventory; the vector-image passthrough; the empty-deck
inventory; and the single content flow with links resolving on disk.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `PowerPointDeckModel` instances with synthetic slides, titles, notes, and
  image references
- **Filesystem**: a per-test `TempScratch` folder for the scenarios that resolve links on disk; none
  for the in-memory recording-sink scenarios
- **Mocking**: a recording sink captures content, parts, images, notes, and features; the emitter
  itself is the real unit
- **Isolation**: each test builds its own model and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointContentEmitter` unit test run passes when it writes each slide's
title, body text, and speaker notes; links a slide's images inline only when a path was returned and
never leaves a dangling link when images are suppressed; writes vector metafiles unchanged with no
PowerPoint-specific note; reports the content outline including zero-count speaker notes for a
notes-less deck; writes empty content plus zero-count inventory for an empty deck; and writes the deck
as a single flow or one part per slide with links resolving on disk. Any silent loss, dangling link,
or wrong inventory count is a failure.

### Test Scenarios

#### Title, body text, and speaker notes are written

**Test**: `PowerPointContentEmitter_Emit_WritesTitleBodyAndNotes`

Proves the emitter writes each slide's title as its heading, its body text under it, and its speaker
notes marked as notes. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesTitle`,
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesSlideText`, and
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesNotes`.

#### A deck with no speaker notes is stated as a counted zero in the inventory

**Tests**: `PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureWithoutNote`,
`PowerPointContentEmitter_Emit_NotesPresent_ReportsSpeakerNotesFeature`

Prove a deck carrying no speaker notes reports the note-set count as `0` in the content outline and
records no note, while a deck with notes reports the positive count and still records no note.
Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceInInventory`.

#### A slide's images are written and linked inline

**Tests**: `PowerPointContentEmitter_Emit_WithRasterImages_WritesThroughSink`,
`PowerPointContentEmitter_Emit_SlideImage_LinksInlineFromContent`,
`PowerPointContentEmitter_Emit_ImagesSuppressed_NoDanglingLink`,
`PowerPointContentEmitter_Emit_ImagesDisabled_WritesNone`

Prove the emitter writes embedded raster images through the sink, links a slide's image at its point
of occurrence when the sink returned a path, leaves no dangling link when images are suppressed, and
writes none when image emission is disabled. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-LinksImages`.

#### A vector image is written unchanged with no note

**Test**: `PowerPointContentEmitter_Emit_VectorImage_WritesWithoutNote`

Proves an EMF or WMF metafile is written unchanged and records no PowerPoint-specific note. Evidence
for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesVectorImagesWithoutNote`.

#### An empty deck is reported by empty content and zero-count inventory

**Test**: `PowerPointContentEmitter_Emit_EmptyDeck_WritesEmptyContentAndZeroInventory`

Proves a deck with no slides writes empty content, reports zero pages, and reports zero-count
inventory for the looked-for document structure. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsEmptyDeckInInventory`.

#### The content outline is reported from the model

**Tests**: `PowerPointContentEmitter_Emit_Deck_ReportsContentFeaturesIncludingSpeakerNotes`,
`PowerPointContentEmitter_Emit_NoImages_ReportsZeroInlineImagesWithoutNote`

Prove the outline counts — slides, slide titles, sets of speaker notes, and inline images — are
reported from the model, including zero inline images for an image-free deck. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsContentFeatures`.

#### The deck is written as one flow whose image links resolve on disk

**Test**: `PowerPointContentEmitter_Emit_ImageLinks_ResolveOnDisk`

Proves the deck is written as one `content.md` and that its inline image links resolve on disk from
the scratch root. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesSingleFlow`.
