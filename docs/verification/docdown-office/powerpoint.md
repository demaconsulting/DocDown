# DocDown.PowerPoint Verification Design

This document describes the system-level verification strategy for `DocDown.PowerPoint`, the
PowerPoint extraction package.

## Verification Approach

`DocDown.PowerPoint` is verified through system-level integration tests in `DocDownPowerPointTests.cs`,
unit tests per unit in `DemaConsulting.DocDown.Office.Tests`, and release-time self-tests for the
real COM automation boundary.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with layout assertions proving the invariant output
folders and files are present on disk. This is the highest-value assertion available to this package:
it makes a dishonest extraction a test failure rather than a review finding, and it applies to both
clean `Produced` runs and `Unreadable` runs. The legacy `.ppt` refusal scenario is checked the same
way, so the structured unreadable layout is verified alongside successful modern-deck extractions.
Beyond the layout, each scenario asserts the extraction outcome, the inventory counts written into
`summary.txt` and `manifest.json`, and the exact set of notes the run recorded.

### Two backends, one reader, one emitter

The package ships two backends. The managed Open XML backend reads a deck into the reader-neutral
model and drives the content emitter, so every mapping decision is made in exactly one place and can
be proved from a hand-built model with no deck behind it. The COM backend reuses that same content
path by delegation and adds a rendered image of each slide; everything it does apart from talking to
Microsoft PowerPoint is exercised cross-platform by injecting a stub `IPowerPointAutomation`, so
delegation, rendering, per-slide fault isolation, and rendering-fact reconciliation are all proved in
CI. The real COM adapter is the one boundary CI cannot reach; its correctness in a deployed
environment is proven by release-time self-tests.

### Speaker-notes inventory is the headline reporting property

Speaker notes are the half of a deck no render can supply, so the suite asserts they are extracted
from the file itself for every slide, in presentation order, alongside the slide text and title. The
whole-deck absence of notes is verified as an inventory fact rather than as a separate shortfall:
`PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureWithoutNote` proves the emitter reports
the zero and records no note, and
`DocDownPowerPoint_Extract_DeckWithoutNotes_ReportsZeroNotesInSummaryAndManifest` proves the zero
reaches both `summary.txt` and `manifest.json` end to end, with the run still a clean `Produced`
result.

### Rendering is captured when available and noted honestly when not

The COM backend renders every slide through the injected stub and adds one page per slide; a slide the
renderer cannot produce becomes a one-sentence `ExtractionNote` while the remaining slides still
render. The availability probe is proved to report unavailable off Windows with a declarative reason
that never instructs an installation, so a machine without PowerPoint produces an honest selection
outcome rather than a broken render.

### Test fixtures are generated; the self-test probe is committed

Every deck the suite uses is built at test time by the Open XML SDK writer in `TestData/PptxFixtures.cs`,
and the rendering path is driven by `TestData/StubPowerPointAutomation.cs`. The legacy `.ppt`
scenario writes a placeholder byte sequence whose extension drives format detection, because the
selection path never opens the file: no registered backend supports the format. No fixture is
committed.
The one committed binary is the backend's self-test probe: a real deck authored in Microsoft
PowerPoint, embedded in the package so the self-test reads what that application emits.
The suite's own fixtures stay generated. No `.pptx` or
`.ppt` beyond that probe is committed, so every slide title, body line, speaker note,
image name, and rendered payload in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input deck and the
  extraction output
- **Inputs**: PresentationML decks generated at test time by the Open XML SDK writer, plus a short
  byte sequence for the legacy `.ppt` refusal scenario; no committed binary fixtures and no network
  access
- **Mocking**: none for the managed integration scenarios; the COM backend is driven through an
  injected stub `IPowerPointAutomation` so the whole rendering path is exercised with no Microsoft
  Office present
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception or wrong return value.
- Every extraction scenario produces the expected DocDown layout on disk, and the notes the run
  records are exactly the notes the scenario expects.
- Every successful deck extraction completes as `Produced`, including a deck with no speaker notes, a
  deck whose only image is an EMF, and a deck with no slides.
- Every slide's text, title, and speaker notes reach the output, extracted from the file itself, in
  presentation order.
- The content inventory reports the looked-for document structure honestly, including
  `0 sets of speaker notes` for a notes-less deck.
- The rendering backend renders every slide when the stub adapter succeeds, and records a plain note
  naming a slide the renderer could not produce while continuing with the remaining slides.
- The COM run's composed report is internally consistent: the delegated backend's "rendering not
  provided" fact is suppressed and the authoritative renderer fact is present.
- The legacy binary `.ppt` format is refused with an `Unreadable` result carrying a plain explanation
  that states DocDown does not support legacy binary Office formats, never an installation
  instruction.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.

### The default engine registers both PowerPoint backends

**Test**: `AddPowerPoint_RegistersOpenXmlAndComBackends`

Proves the one-liner the host uses to add PowerPoint support registers both the managed Open XML
backend and the COM automation backend on the resulting engine. Evidence for
`DocDownPowerPoint-Registration`.

### Backend selection stays explicit and observable

**Tests**: `PowerPointOpenXmlExtractor_Descriptor_MatchesContract`,
`PowerPointComExtractor_Descriptor_MatchesContract`

Prove the managed and COM extractors expose distinct backend identities, stable priorities, and
page-rendering applicability through the selection surface the engine reads. Evidence for
`DocDownPowerPoint-BackendSelectionSurface`.

### The Open XML backend is selected and produces the contract layout

**Test**: `DocDownPowerPoint_Extract_Pptx_SelectsOpenXml`

Proves a clean extraction: the managed backend is selected for a `.pptx`, the full output layout is
produced, and the run completes as `Produced` with no notes recorded. Evidence for
`DocDownPowerPoint-Extraction`.

### Slide body text is extracted

**Test**: `PowerPointOpenXmlReader_Read_Slide_ExtractsBodyTextLines`

Proves a slide's body text lines are extracted from the file, separate from its title. Evidence for
`DocDownPowerPoint-SlideText`.

### Slide titles are extracted and written

**Test**: `PowerPointContentEmitter_Emit_WritesTitleBodyAndNotes`

Proves a slide's title becomes its heading and its body and notes are written under it. Evidence for
`DocDownPowerPoint-SlideTitle`.

### Speaker notes are always extracted

**Test**: `DocDownPowerPoint_Extract_DeckWithNotes_ExtractsSpeakerNotes`

Proves the speaker notes that never appear in any render are written into the content flow. Evidence
for `DocDownPowerPoint-SpeakerNotes`.

### A deck with no speaker notes is stated as a counted zero in the inventory

**Tests**: `PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureWithoutNote`,
`DocDownPowerPoint_Extract_DeckWithoutNotes_ReportsZeroNotesInSummaryAndManifest`

Prove a notes-less deck reports `0 sets of speaker notes` in the content outline and in the
manifest's `contentFeatures`, records no note, and still succeeds. Evidence for
`DocDownPowerPoint-SpeakerNotesAbsenceInventory`.

### Slides are returned in presentation order

**Test**: `PowerPointOpenXmlReader_Read_DeckWithNotes_ReturnsSlidesInOrder`

Proves the slides come out in presentation order with their 1-based ordinals and their titles.
Evidence for `DocDownPowerPoint-SlideOrder`.

### Slides render when PowerPoint is available

**Test**: `PowerPointComExtractor_Extract_ViaStub_WritesNotesAndRendersEverySlide`

Proves the COM backend delegates the guaranteed content and adds one rendered page per slide through
the automation seam, disposing the session. Evidence for `DocDownPowerPoint-PageRendering`.

### A slide that cannot be rendered becomes a plain note

**Test**: `PowerPointComExtractor_Extract_SlideRenderFails_ReportsNote`

Proves a slide that fails to render is named in a one-sentence note while the remaining slides still
render. Evidence for `DocDownPowerPoint-SlideRenderNotes`.

### Rendering availability is honest and instructs no installation

**Test**: `PowerPointComAvailability_Probe_NeverThrowsAndNeverInstructsInstallation`

Proves the availability probe is cheap, never throws, and never instructs an installation. Evidence
for `DocDownPowerPoint-RenderingAvailability`.

### Embedded images are written and linked from the slide

**Test**: `DocDownPowerPoint_Extract_DeckWithImage_WritesEmbeddedImage`

Proves a deck's embedded image is written to `images/` and the extraction reconciles cleanly, with
the slide association recorded. Evidence for `DocDownPowerPoint-EmbeddedImages`.

### A vector image is written unchanged

**Test**: `DocDownPowerPoint_Extract_DeckWithVectorImage_Succeeds`

Proves an EMF metafile is written unchanged and the run stays a clean `Produced` result with no
PowerPoint-specific note. Evidence for `DocDownPowerPoint-VectorImagePassthrough`.

### An empty deck is reported by empty content and zero-count inventory

**Test**: `PowerPointContentEmitter_Emit_EmptyDeck_WritesEmptyContentAndZeroInventory`

Proves a deck with no slides writes empty content and reports zero-count inventory for the looked-for
document structure. Evidence for `DocDownPowerPoint-EmptyDeckInventory`.

### The content outline is reported from the model

**Tests**: `PowerPointContentEmitter_Emit_Deck_ReportsContentFeaturesIncludingSpeakerNotes`,
`DocDownPowerPoint_Extract_DeckWithNotes_ReportsSpeakerNotesInventory`

Prove the outline counts — slides, slide titles, sets of speaker notes, and inline images — are
reported from the model and reach the end-to-end artifacts. Evidence for
`DocDownPowerPoint-ContentOutline`.

### A legacy binary deck is refused with a plain explanation

**Test**: `DocDownPowerPoint_Extract_LegacyPpt_FailsWithUnsupportedFormatExplanation`

Proves an engine with the PowerPoint package registered completes a `.ppt` request as `Unreadable`
with a plain explanation stating that DocDown does not support the legacy binary Office formats, with
no installation instruction. Evidence for `DocDownPowerPoint-LegacyFormatRefusal`.

### Self-validation cases are exposed and run

**Test**: `PowerPointOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the managed backend contributes its cases, that the round-trip case genuinely passes in the
environment under test, and that the page-rendering case reports a skip with a reason rather than a
failure. Evidence for `DocDownPowerPoint-SelfValidation`.
