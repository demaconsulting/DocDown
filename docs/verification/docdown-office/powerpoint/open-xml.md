## OpenXml Subsystem Verification Design

This document describes the verification strategy for the OpenXml subsystem, the managed PowerPoint
extraction backend: the extractor the engine selects, the reader that turns the presentation package
into the model, and the image reader that resolves the deck's embedded images.

### Verification Approach

The OpenXml subsystem is verified through unit tests in
`OpenXml/PowerPointOpenXmlExtractorTests.cs` and `OpenXml/PowerPointOpenXmlReaderTests.cs`, plus the
system-level scenarios in `DocDownPowerPointTests.cs`, all in `DemaConsulting.DocDown.Office.Tests`.

The reader is exercised against real generated decks from `TestData/PptxFixtures.cs`, not a
simulation, because its contract is the faithful translation of a genuine `.pptx` into the model: the
slide order, the titles, the body text, the speaker notes, and the embedded images with their slide
association. The extractor's selection surface — its identity, priority, supported format,
page-rendering applicability, and the probe result that leaves rendered-page support false — is
asserted directly, and its self-test cases are run to prove the round-trip case passes and the
page-rendering case skips. The image reader carries no dedicated test class; its behavior is proved
through the reader that drives it and the emitter scenarios that render its inline links.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PresentationML decks generated at test time by `TestData/PptxFixtures.cs`; every title,
  body line, speaker note, and image name in them is synthetic
- **Filesystem**: a per-test `TempScratch` folder for the system-level scenarios; none for the reader
  scenarios, which read a generated deck from memory
- **Mocking**: none; the SDK is exercised against real decks and the sink is Core's real writer
  through the engine
- **Isolation**: each test builds its own deck and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor's selection surface
matches the supported contract, the probe is unconditional and reports available without rendered-page
support, and the self-tests report a passing round trip and a skipped page-rendering case; when the
reader returns the slides in presentation order with their ordinals and titles, extracts each slide's
body text separate from its title, reads the speaker notes from the notes body placeholder, and yields
null notes for a notes-less deck; and when the image reader yields an embedded image and records its
slide association. Any wrong descriptor field, reordered or dropped slide, misread or truncated notes,
or lost slide association is a failure.

### Test Scenarios

The per-unit scenarios are given in the `PowerPointOpenXmlExtractor`, `PowerPointOpenXmlReader`, and
`PowerPointOpenXmlImageReader` unit verification chapters, each naming the requirement it evidences.
