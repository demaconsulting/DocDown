## Com Subsystem Verification Design

This document describes the verification strategy for the Com subsystem, the rendering seam: the
extractor, the availability probe, and the real automation adapter.

### Verification Approach

The Com subsystem is verified through unit tests in `Com/PowerPointComExtractorTests.cs` and
`Com/PowerPointComAvailabilityTests.cs` in `DemaConsulting.DocDown.PowerPoint.Tests`.

Everything the COM backend does apart from talking to Microsoft PowerPoint is exercised cross-platform
by injecting a stub `IPowerPointAutomation` (`TestData/StubPowerPointAutomation.cs`): the delegation
to the managed backend that writes slide text and speaker notes, the rendering of every slide, the
per-slide fault isolation that turns a failed slide into a plain note, the pass-through of the render
resolution to the seam, the suppression of the delegated backend's contradictory rendering fact, and
the null-factory and public-constructor probe behavior. The availability probe is asserted directly,
including its off-Windows path, which CI reaches wherever it runs on Linux and macOS.

The real automation adapter, `PowerPointAutomation`, is the one boundary CI cannot reach: it talks to
Microsoft PowerPoint over late-bound COM on Windows only. Its observable seam contract — render each
slide at the requested resolution, return one PNG-or-reason result per slide, and release its session
— is proved in CI through the extractor tests above. The adapter's internal correctness is proven by
release-time self-tests, not by CI.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PresentationML decks generated at test time by `TestData/PptxFixtures.cs`, materialized
  to a per-test `TempScratch` folder; the rendering path is driven by synthetic PNG payloads from the
  stub
- **Mocking**: a stub `IPowerPointAutomation` stands in for Microsoft PowerPoint on every platform; a
  recording sink captures the pages, notes, and environment facts
- **Isolation**: each test builds its own deck and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Com subsystem test run passes when the extractor exposes the expected backend
identity and lower priority, delegates the guaranteed content to the managed backend, renders every
slide through the seam at the requested resolution, records a plain note naming a slide that could not
be rendered while continuing with the remaining slides, suppresses the delegated backend's "rendering
not provided" fact while emitting the authoritative renderer fact, and probes unavailable without
throwing and without instructing an installation; and when the availability probe reports unavailable
off Windows with a declarative reason. Any silent slide loss, contradictory rendering fact, or
throwing probe is a failure.

### Test Scenarios

The per-unit scenarios are given in the `PowerPointComExtractor`, `PowerPointComAvailability`, and
`PowerPointAutomation` unit verification chapters, each naming the requirement it evidences.
