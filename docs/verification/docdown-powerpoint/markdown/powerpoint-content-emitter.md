## PowerPointContentEmitter Verification Design

This document describes the unit-level verification strategy for `PowerPointContentEmitter`, the single
emission path from the deck model to the output contract.

### Verification Approach

`PowerPointContentEmitter` is verified through unit tests in `Markdown/PowerPointContentEmitterTests.cs` in
`DemaConsulting.DocDown.PowerPoint.Tests`, driven from **hand-built deck models** through a recording sink.
Building the model by hand — with no deck and no Open XML SDK behind it — is what lets each mapping decision
be proved in isolation: the per-slide title, text, and notes; the inline image links and their suppression;
the vector-image caveat; the charts-not-read gap; the empty-deck gap; the content outline; and the
single-flow-versus-per-part split with links resolving on disk.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `PowerPointDeckModel` instances with synthetic slides, titles, notes, and image
  references
- **Filesystem**: a per-test `TempScratch` folder for the scenarios that resolve links on disk; none for the
  in-memory recording-sink scenarios
- **Mocking**: a recording sink captures content, parts, images, diagnostics, gaps, and features; the
  emitter itself is the real unit
- **Isolation**: each test builds its own model and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointContentEmitter` unit test run passes when it writes each slide's title,
body text, and speaker notes; links a slide's images inline only when a path was returned and never leaves a
dangling link when images are suppressed; writes a vector image with an informational caveat and no gap;
reports a counted gap for charts the deck embeds and none for a chart-free deck; degrades with a counted gap
for an empty deck; reports the content outline including the speaker-notes count; and writes the deck as a
single flow or one part per slide with links resolving on disk. Any silent loss, dangling link, or wrong
gap-versus-diagnostic classification is a failure — save for the recorded speaker-notes divergence below.

### Test Scenarios

#### Title, body text, and speaker notes are written

**Test**: `PowerPointContentEmitter_Emit_WritesTitleBodyAndNotes`

Proves the emitter writes each slide's title as its heading, its body text under it, and its speaker notes
marked as notes. Evidence for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesTitle`,
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesSlideText`, and
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-WritesNotes`.

#### A deck with no speaker notes is stated as a counted zero in the inventory

**Test**: `PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureNotGapOrDiagnostic`

Proves a deck carrying no speaker notes reports the note-set count as `0` in the content outline and raises
neither a gap nor a diagnostic, with the complementary `PowerPointContentEmitter_Emit_NotesPresent_NoNotesGap`
proving the count is reported and no gap raised when notes are present. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceInInventory`, which supersedes
the earlier counted-gap requirement
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceGap` — authored as intent
before the governing principle that DocDown makes no acceptability judgement about a document's content —
together with the retired `PPTX0002` diagnostic.

#### A slide's images are linked inline, with no dangling link when suppressed

**Test**: `PowerPointContentEmitter_Emit_SlideImage_LinksInlineFromContent`

Proves a slide's image is linked inline at its point of occurrence when the sink returned a path;
`PowerPointContentEmitter_Emit_ImagesSuppressed_NoDanglingLink` proves no link is left pointing at nothing
when images are suppressed. Evidence for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-LinksImages`.

#### A vector image is written with an informational caveat

**Test**: `PowerPointContentEmitter_Emit_VectorImage_WritesWithInfoCaveat`

Proves an EMF or WMF metafile is written unchanged with a `PPTX0003` informational caveat and no gap.
Evidence for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsVectorImageCaveat`.

#### Charts the backend does not read are a counted gap

**Test**: `PowerPointContentEmitter_Emit_DeckWithCharts_ReportsCountedGap`

Proves a deck that embeds charts is reported with a counted `PPTX0005` gap naming them, while
`PowerPointContentEmitter_Emit_DeckWithoutCharts_ReportsNoChartGap` proves a chart-free deck reports none.
Evidence for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsChartsNotExtracted`.

#### An empty deck degrades with a counted gap

**Test**: `PowerPointContentEmitter_Emit_EmptyDeck_ReportsGap`

Proves a deck with no slides degrades with a counted gap naming the absence and writes an empty content
document rather than nothing. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsEmptyDeckGap`.

#### The content outline is reported from the model

**Test**: `PowerPointContentEmitter_Emit_Deck_ReportsContentFeaturesIncludingSpeakerNotes`

Proves the outline counts — slides, slide titles, sets of speaker notes, and inline images — are reported
from the model, so a paste-in reader sees the deck's shape and that the notes are present. Evidence for
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsContentFeatures`.

#### A large deck is written as one part per slide

**Test**: `PowerPointContentEmitter_Emit_PerPart_WritesSlideParts`

Proves the emitter honors the per-part split mode, writing one part per slide, with
`PowerPointContentEmitter_Emit_PerPartImageLinks_ResolveOnDisk` and
`PowerPointContentEmitter_Emit_AutoImageLinks_ResolveOnDisk` proving the inline links resolve on disk.
Evidence for `DocDownPowerPoint-Markdown-PowerPointContentEmitter-SplitsLargeDecks`.
