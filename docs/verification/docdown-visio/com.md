## Com Subsystem Verification Design

This document describes the verification strategy for the Com subsystem, the rendering seam: the
composing extractor, the availability probe, and the real automation adapter.

### Verification Approach

The Com subsystem is verified through unit tests in `Com/VisioComExtractorTests.cs` and
`Com/VisioComAvailabilityTests.cs` in `DemaConsulting.DocDown.Visio.Tests`.

Everything the COM backend does apart from talking to Microsoft Visio is exercised cross-platform by
injecting a stub `IVisioAutomation`: delegation to the managed backend that writes page names, shape
text, topology, images, and metadata; rendering of every page; pass-through of render resolution;
suppression of the delegated backend's contradictory rendering fact; per-page note reporting; and
null-factory and public-constructor probe behavior. The availability probe is asserted directly,
including its off-Windows path, which CI reaches wherever it runs on Linux and macOS.

The real automation adapter, `VisioAutomation`, is the one boundary CI cannot reach. Its observable
seam contract — render each page at the requested resolution, return one PNG-or-reason result per
page, and release its session — is proved in CI through the extractor tests above. The adapter's
internal correctness is proved by release-time self-tests, not by CI.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings generated at test time by `TestData/VsdxFixtures.cs`,
  materialized to a per-test `TempScratch` folder; the rendering path is driven by synthetic PNG
  payloads from the stub
- **Mocking**: a stub `IVisioAutomation` stands in for Microsoft Visio on every platform; a
  recording sink captures pages, notes, and environment facts
- **Isolation**: each test builds its own drawing and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Com subsystem test run passes when the extractor identifies the modern
Visio drawing formats, participates in selection below the managed backend, delegates the managed
content path, renders every page through the seam at the requested resolution, records a plain note
for a page it could not render while continuing, suppresses the delegated backend's `visio.pageRendering`
fact while emitting the authoritative renderer fact, and probes unavailable without throwing or
instructing an installation; and when the availability probe reports unavailable off Windows with a
declarative reason. Any silent page loss, contradictory rendering fact, throwing probe, or
install-instructing reason is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioComExtractor`, `VisioComAvailability`, and
`VisioAutomation` unit verification chapters, each naming the requirement it evidences.
