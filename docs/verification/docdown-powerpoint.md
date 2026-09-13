# DocDown.PowerPoint Verification Design

This document describes the system-level verification strategy for `DocDown.PowerPoint`, the PowerPoint
extraction package.

## Verification Approach

`DocDown.PowerPoint` is verified through system-level integration tests in `DocDownPowerPointTests.cs`, and
unit tests per unit, all in `DemaConsulting.DocDown.PowerPoint.Tests`, running on xUnit v3 across net8.0,
net9.0, and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with Core's contract verifier over the produced folder,
which reports any disagreement between what the manifest claims and what is on disk. This is the
highest-value assertion available to this package: it makes a dishonest extraction a test failure rather
than a review finding, and it applies to degraded and failed runs as well as clean ones. The reconciliation
covers the legacy `.ppt` refusal scenario, so the full-layout claim on a structured failure is
machine-checked alongside the successful ones.

### Two backends, one reader, one emitter

The package ships two backends. The managed Open XML backend reads a deck into the reader-neutral model and
drives the content emitter, so every mapping decision is made in exactly one place and can be proved from a
hand-built model with no deck behind it. The COM backend reuses that same content path by delegation and
adds a rendered image of each slide; everything it does apart from talking to Microsoft PowerPoint is
exercised cross-platform by injecting a stub `IPowerPointAutomation`, so slide-text delegation, per-slide
fault isolation, rendering-fact reconciliation, and outcome mapping are all proved in CI. The real COM
adapter is the one boundary CI cannot reach; its correctness in a deployed environment is proven by
release-time self-tests. A legacy binary `.ppt` has no reader here and none anywhere in DocDown, so the
request fails with a structured, reasoned outcome — never with an exception, and never with a remedy that
promises a capability that does not exist.

### The speaker-notes guarantee is the headline property under test

Speaker notes are the half of a deck no render can supply, so the suite asserts they are extracted from the
file itself for every slide, in presentation order, alongside the slide text and title. The whole-deck
**absence** of notes is verified as an inventory fact rather than a shortfall: the count of note sets is
reported whether or not any exist, so a reader can tell "every notes slide was read and there are none"
from "notes are not something DocDown counts". `PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureNotGapOrDiagnostic`
proves the emitter reports the zero and raises neither a gap nor a diagnostic, and
`DocDownPowerPoint_Extract_DeckWithoutNotes_ReportsZeroNotesInSummaryAndManifest` proves the zero reaches
both `summary.txt` and `manifest.json` end to end, with the run still a clean success. This supersedes the
earlier expectation, recorded against `DocDownPowerPoint-SpeakerNotesAbsenceGap`, that the absence be a
counted gap: that requirement was authored before the owner established that DocDown reports what was
extracted and where and makes no acceptability judgement about the content, and is superseded by
`DocDownPowerPoint-SpeakerNotesAbsenceInventory`. The `PPTX0002` diagnostic that carried the same absence
is retired.

### Rendering is captured when available and honest when not

The COM backend renders every slide through the injected stub and adds one page per slide; a slide the
renderer cannot produce becomes a counted, named `pages` gap and a `PPTX0004` diagnostic while the remaining
slides still render. The availability probe is proved to report unavailable off Windows with a declarative
reason that never instructs an installation, so a machine without PowerPoint produces an honest selection
outcome rather than a broken render.

### Fixtures are generated, never committed

Every deck the suite uses is built at test time by the Open XML SDK writer in `TestData/PptxFixtures.cs`,
and the rendering path is driven by `TestData/StubPowerPointAutomation.cs`. The legacy `.ppt` scenario
writes a placeholder byte sequence whose extension drives format detection, because the selection path never
opens the file: no registered backend supports the format. No binary `.pptx` or `.ppt` is committed, so the
repository stays text-only and no question arises about the provenance or licensing of a sample deck. Every
slide title, body line, speaker note, image name, and rendered payload in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input deck and the extraction
  output
- **Inputs**: PresentationML decks generated at test time by the Open XML SDK writer, plus a short byte
  sequence for the legacy `.ppt` refusal scenario; no committed binary fixtures and no network access
- **Mocking**: none for the managed integration scenarios — every test drives the real engine and the real
  Open XML backend; the COM backend is driven through an injected stub `IPowerPointAutomation` so the whole
  rendering path is exercised with no Microsoft Office present
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no unexpected
  exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations.
- No adverse deck — legacy, empty, or unreadable — causes an exception to escape to the caller, and every
  one of them still produces the full output layout.
- Every slide's text, title, and speaker notes reach the output, extracted from the file itself, in
  presentation order.
- The rendering backend renders every slide when the stub adapter succeeds, and reports a counted, named
  gap for any slide it cannot render while continuing with the rest.
- The COM run's composed report is internally consistent: the delegated backend's "rendering not provided"
  fact is suppressed and the authoritative renderer fact is present.
- The legacy binary `.ppt` format is refused with a coded structured failure whose remedy states that
  DocDown does not support legacy binary formats, never an exception and never an instruction the reader
  could act on and fail at.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching operating
  system or runtime; a result from another platform does not count.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
Platform requirements are covered by the source-filtered runs of the selection scenario.

### The default engine registers both PowerPoint backends

**Test**: `AddPowerPoint_RegistersOpenXmlAndComBackends`

Proves the one-liner the host uses to add PowerPoint support registers both the managed Open XML backend and
the COM automation backend on the resulting engine, so the set of active backends is a decision readable in
host code rather than a deployment accident. Evidence for `DocDownPowerPoint-Registration`.

### The Open XML backend is selected and produces the contract layout

**Test**: `DocDownPowerPoint_Extract_Pptx_SelectsOpenXml`

Proves a clean extraction: the managed Open XML backend is selected for a `.pptx`, the full output layout is
produced, and the contract verifier reports no violations. This is also the anchor for the platform
requirements. Evidence for `DocDownPowerPoint-Extraction`, and under source filters the six
`DocDownPowerPoint-Platform-*` requirements.

### Slide body text is extracted

**Test**: `PowerPointOpenXmlReader_Read_Slide_ExtractsBodyTextLines`

Proves a slide's body text lines are extracted from the file, separate from its title, so the words on a
slide reach the output with no rendering. Evidence for `DocDownPowerPoint-SlideText`.

### Slide titles are extracted and written

**Test**: `PowerPointContentEmitter_Emit_WritesTitleBodyAndNotes`

Proves a slide's title becomes its heading and its body and notes are written under it, so a reader can
navigate the deck by its titles. Evidence for `DocDownPowerPoint-SlideTitle`.

### Speaker notes are always extracted

**Test**: `PowerPointOpenXmlReader_Read_Slide_ExtractsSpeakerNotes`

Proves the speaker notes that never appear in any render are read from the notes slide's body placeholder for
each slide, recovered from the file itself. Evidence for `DocDownPowerPoint-SpeakerNotes`.

### A deck with no speaker notes is stated as a counted zero in the inventory

**Tests**: `PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureNotGapOrDiagnostic`,
`DocDownPowerPoint_Extract_DeckWithoutNotes_ReportsZeroNotesInSummaryAndManifest`

Proves a deck carrying no speaker notes reports `0 sets of speaker notes` in the summary's content outline
and in the manifest's `contentFeatures`, raises neither a gap nor a diagnostic, and still succeeds — so a
consuming agent can tell "every notes slide was read and there are none" from "notes are not something
DocDown counts" without that absence being dressed up as a shortfall of the extraction. Evidence for
`DocDownPowerPoint-SpeakerNotesAbsenceInventory`, which supersedes the earlier counted-gap requirement
`DocDownPowerPoint-SpeakerNotesAbsenceGap` (authored as intent before the governing principle that
DocDown makes no acceptability judgement about content) and the retired `PPTX0002` diagnostic.

### Slides are returned in presentation order

**Test**: `PowerPointOpenXmlReader_Read_DeckWithNotes_ReturnsSlidesInOrder`

Proves the slides come out in presentation order with their 1-based ordinals and their titles, because a
deck's argument is sequential. Evidence for `DocDownPowerPoint-SlideOrder`.

### Slides render when PowerPoint is available

**Test**: `PowerPointComExtractor_Extract_ViaStub_WritesNotesAndRendersEverySlide`

Proves the COM backend delegates the guaranteed content — including the speaker notes no render can supply —
and adds one rendered page per slide through the automation seam, disposing the session. Evidence for
`DocDownPowerPoint-PageRendering`.

### A slide that cannot be rendered is a counted gap

**Test**: `PowerPointComExtractor_Extract_SlideRenderFails_ReportsCountedGap`

Proves a slide that fails to render becomes a counted `pages` gap and a `PPTX0004` diagnostic while the
remaining slides still render — never a silent absence. Evidence for `DocDownPowerPoint-SlideRenderFidelity`.

### Rendering availability is honest and instructs no installation

**Test**: `PowerPointComAvailability_Probe_NeverThrowsAndNeverInstructsInstallation`

Proves the availability probe is cheap, never throws, and never instructs an installation, so an environment
without PowerPoint is reported as an honest fact. Evidence for `DocDownPowerPoint-RenderingAvailability`.

### Embedded images are written and linked from the slide

**Test**: `DocDownPowerPoint_Extract_DeckWithImage_WritesEmbeddedImage`

Proves a deck's embedded image is written to `images/` and the extraction reconciles cleanly, with the slide
association recorded. Evidence for `DocDownPowerPoint-EmbeddedImages`.

### A vector image is written as-is with a caveat

**Test**: `DocDownPowerPoint_Extract_DeckWithVectorImage_Succeeds`

Proves an EMF or WMF metafile is written unchanged, `PPTX0003` records the readability caveat as an
informational diagnostic, and no images gap is opened — a well-formed vector-bearing deck does not degrade.
Evidence for `DocDownPowerPoint-VectorImages`.

### Charts the backend does not read are a counted gap

**Test**: `PowerPointContentEmitter_Emit_DeckWithCharts_ReportsCountedGap`

Proves a deck that embeds charts is reported with a counted gap naming them, with a remedy pointing at the
workbook route, so a chart's quantities are not dropped in silence. Evidence for `DocDownPowerPoint-Charts`.

### An empty deck degrades with a counted gap

**Test**: `PowerPointContentEmitter_Emit_EmptyDeck_ReportsGap`

Proves a deck with no slides degrades with a counted gap naming the absence, so an empty content document is
a stated fact rather than a mystery. Evidence for `DocDownPowerPoint-EmptyDeck`.

### The managed backend declares exactly the deliverable capabilities

**Test**: `PowerPointOpenXmlExtractor_Descriptor_MatchesContract`

Proves the managed descriptor's identity, priority, supported format, and declared capabilities — text,
embedded images, document metadata, and document structure — match the deliverable set, and — as an express
counterpart — that the rendered-pages capability is not declared. The COM descriptor's full superset
including rendered-pages is proved by `PowerPointComExtractor_Descriptor_MatchesContract`. Evidence for
`DocDownPowerPoint-DeclaredCapabilities`.

### The content outline is reported from the model

**Test**: `PowerPointContentEmitter_Emit_Deck_ReportsContentFeaturesIncludingSpeakerNotes`

Proves the outline counts — slides, slide titles, sets of speaker notes, and inline images — are reported
from the model, so a paste-in reader sees the deck's shape and that the notes are present. Evidence for
`DocDownPowerPoint-ContentOutline`.

### A legacy binary deck is refused with an honest remedy

**Test**: `DocDownPowerPoint_Extract_LegacyPpt_FailsWithUnsupportedFormatRemedy`

Proves an engine with the PowerPoint package registered fails a `.ppt` request with a structured failure and
a remedy that names the format and states plainly that DocDown does not support the legacy binary Office
formats — with no package named, no installation instruction, and no environment precondition, because no
such route exists. The full contract layout is still produced and reconciles cleanly. Evidence for
`DocDownPowerPoint-LegacyFormatRefusal`.

### Self-validation cases are exposed and run

**Test**: `PowerPointOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the managed backend contributes its cases, that the round-trip case genuinely passes in the
environment under test, and that the page-rendering capability the managed backend does not claim reports a
skip with a reason rather than a failure — the property a traceability pipeline depends on. Evidence for
`DocDownPowerPoint-SelfValidation`.
