## Com Subsystem Verification Design

This document describes the verification strategy for the Com subsystem, the rendering seam: the
full-superset extractor, the availability probe, and the real automation adapter.

### Verification Approach

The Com subsystem is verified through unit tests in `Com/VisioComExtractorTests.cs` and
`Com/VisioComAvailabilityTests.cs` in `DemaConsulting.DocDown.Visio.Tests`.

Everything the COM backend does apart from talking to Microsoft Visio is exercised **cross-platform** by
injecting a stub `IVisioAutomation` (`TestData/StubVisioAutomation.cs`): the delegation to the managed
backend that writes the page names, shape text, and directed topology, the rendering of every page, the
per-page fault isolation that turns a failed page into a counted gap, the pass-through of the render
resolution to the seam, the suppression of the delegated backend's contradictory rendering fact, and the
null-factory and public-constructor probe behavior. The availability probe is asserted directly, including
its off-Windows path, which CI reaches wherever it runs on Linux and macOS.

The real automation adapter, `VisioAutomation`, is the **one boundary CI cannot reach**: it talks to
Microsoft Visio over late-bound COM on Windows only. Its observable seam contract — render each page at the
requested resolution, return one PNG-or-reason result per page, and release its session — is proved in CI
through the extractor tests above (which drive the stub and assert the extractor consumes the seam's output
correctly). The adapter's internal correctness — read-only open, the single session, the PNG export, and
the deterministic session teardown — is proven by the **release-time self-test cases**, not by CI.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings generated at test time by `TestData/VsdxFixtures.cs`,
  materialized to a per-test `TempScratch` folder; the rendering path is driven by synthetic PNG payloads
  from the stub
- **Mocking**: a stub `IVisioAutomation` stands in for Microsoft Visio on every platform; a recording sink
  captures the pages, gaps, diagnostics, and environment facts
- **Isolation**: each test builds its own drawing and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Com subsystem test run passes when the extractor declares the full capability
superset including rendered-pages at the lower priority, delegates the guaranteed content to the managed
backend, renders every page through the seam at the requested resolution, isolates a failed page into a
counted `pages` gap and a `VISIO0004` diagnostic while continuing, suppresses the delegated backend's
"rendering not provided" fact while emitting the authoritative renderer fact, and probes unavailable
without throwing and without instructing an installation; and when the availability probe reports
unavailable off Windows with a declarative reason. Any overstated capability, silent page loss,
contradictory rendering fact, throwing probe, or install-instructing reason is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioComExtractor`, `VisioComAvailability`, and `VisioAutomation`
unit verification chapters, each naming the requirement it evidences.
