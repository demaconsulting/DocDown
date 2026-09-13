## VisioComExtractor Verification Design

This document describes the unit-level verification strategy for `VisioComExtractor`, the full-superset
rendering backend.

### Verification Approach

`VisioComExtractor` is verified through unit tests in `Com/VisioComExtractorTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, exercising the **whole extraction path through an injected stub**
`IVisioAutomation` with no Microsoft Office present. The stub lets CI prove the delegation to the managed
backend, the rendering of every page, the per-page fault isolation, the render-resolution pass-through, the
rendering-fact reconciliation, and the probe behavior — everything the backend does apart from talking to
Visio. The real adapter it constructs by default is proved separately by release-time self-tests.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings generated at test time by `TestData/VsdxFixtures.cs`,
  materialized to a per-test `TempScratch` folder; synthetic PNG payloads from
  `TestData/StubVisioAutomation.cs`
- **Mocking**: a stub `IVisioAutomation` stands in for Microsoft Visio on every platform; a recording sink
  captures pages, gaps, diagnostics, and environment facts
- **Isolation**: each test builds its own drawing and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioComExtractor` unit test run passes when the descriptor declares the full
superset including rendered-pages at priority zero; the backend delegates the guaranteed content — the
topology — to the managed backend; renders every page through the seam at the caller's resolution; isolates
a failed page into a counted `pages` gap and a `VISIO0004` diagnostic while continuing; suppresses the
delegated backend's "rendering not provided" fact while emitting the authoritative renderer fact; and
probes unavailable without throwing and without instructing an installation. Any overstated capability,
silent page loss, contradictory fact, or throwing probe is a failure.

### Test Scenarios

#### The descriptor declares the full superset

**Test**: `VisioComExtractor_Descriptor_MatchesContract`

Proves the identifier, formats, priority-zero tie-break, and the full capability set including
rendered-pages. Evidence for `DocDownVisio-Com-VisioComExtractor-DeclaresCapabilities`.

#### The managed content is delegated and every page renders

**Test**: `VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage`

Proves the backend writes the guaranteed content — the directed topology — by delegating to the managed
backend, and adds one rendered page per page, disposing the session. Evidence for
`DocDownVisio-Com-VisioComExtractor-DelegatesManagedContent`.

#### The composed rendering facts are reconciled

**Test**: `VisioComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact`

Proves the COM run suppresses the delegated managed backend's "rendering not provided" fact and emits the
authoritative `pages.renderer : available` fact, so the composed report is internally consistent. Evidence
for `DocDownVisio-Com-VisioComExtractor-ReconcilesRenderingFacts`.

#### Every page renders at the requested resolution

**Test**: `VisioComExtractor_Extract_PassesRenderDpiToAutomation`

Proves the caller's render resolution is passed through to the automation seam, and every page is rendered
and added. Evidence for `DocDownVisio-Com-VisioComExtractor-RendersEveryPage`.

#### A failed page is a counted gap

**Test**: `VisioComExtractor_Extract_PageRenderFails_ReportsCountedGap`

Proves a page that fails to render becomes a counted `pages` gap and a `VISIO0004` diagnostic while the
remaining pages still render. Evidence for `DocDownVisio-Com-VisioComExtractor-IsolatesPageRenderFailures`.

#### The probe is honest and instructs no installation

**Tests**: `VisioComExtractor_NullFactory_ProbesUnavailable`,
`VisioComExtractor_PublicCtor_ProbeDoesNotThrowAndNeverInstructsInstallation`

Prove that a null adapter factory makes the backend probe unavailable on every platform without throwing,
that the public constructor probes without throwing, and that no reason instructs an installation. Evidence
for `DocDownVisio-Com-VisioComExtractor-ProbesHonestly`.
