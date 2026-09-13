## VisioComExtractor Verification Design

This document describes the unit-level verification strategy for `VisioComExtractor`, the composing
rendering backend.

### Verification Approach

`VisioComExtractor` is verified through unit tests in `Com/VisioComExtractorTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, exercising the whole extraction path through an injected stub
`IVisioAutomation` with no Microsoft Office present. The stub lets CI prove the delegation to the
managed backend, rendering of every page, per-page note reporting, render-resolution pass-through,
rendering-fact reconciliation, and probe behavior. The real adapter it constructs by default is
proved separately by release-time self-tests.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings generated at test time by `TestData/VsdxFixtures.cs`,
  materialized to a per-test `TempScratch` folder; synthetic PNG payloads from
  `TestData/StubVisioAutomation.cs`
- **Mocking**: a stub `IVisioAutomation` stands in for Microsoft Visio on every platform; a
  recording sink captures pages, notes, and environment facts
- **Isolation**: each test builds its own drawing and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioComExtractor` unit test run passes when the extractor identifies the
modern Visio drawing formats, participates in selection below the managed backend, delegates the
managed content path, renders every page through the seam at the caller's requested resolution,
records a plain note for any page it could not render while continuing, suppresses the delegated
backend's `visio.pageRendering` fact while emitting the authoritative renderer fact, and probes
unavailable without throwing or instructing an installation.

### Test Scenarios

#### The extractor participates in selection as the rendering backend

**Test**: `VisioComExtractor_Descriptor_MatchesContract`

Proves the identifier, supported formats, lower priority, and the fact that page rendering remains a
meaningful request for Visio drawings. Evidence for
`DocDownVisio-Com-VisioComExtractor-ParticipatesInSelection`.

#### The managed content is delegated and every page renders

**Test**: `VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage`

Proves the backend writes the guaranteed content by delegating to the managed backend and adds one
rendered page per page, disposing the session. Evidence for
`DocDownVisio-Com-VisioComExtractor-DelegatesManagedContent`.

#### The composed rendering facts are reconciled

**Test**: `VisioComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact`

Proves the COM run suppresses the delegated managed backend's `visio.pageRendering` fact and emits
the authoritative `pages.renderer` fact. Evidence for
`DocDownVisio-Com-VisioComExtractor-ReconcilesRenderingFacts`.

#### Every page renders at the requested resolution

**Test**: `VisioComExtractor_Extract_PassesRenderDpiToAutomation`

Proves the caller's render resolution is passed through to the automation seam. Evidence for
`DocDownVisio-Com-VisioComExtractor-RendersEveryPage`.

#### A failed page records a plain note

**Test**: `VisioComExtractor_Extract_PageRenderFails_ReportsNote`

Proves a page that fails to render is recorded as a plain note while the remaining pages still
render. Evidence for `DocDownVisio-Com-VisioComExtractor-ReportsPageRenderFailureNotes`.

#### The probe is honest and instructs no installation

**Tests**: `VisioComExtractor_NullFactory_ProbesUnavailable`,
`VisioComExtractor_PublicCtor_ProbeDoesNotThrowAndNeverInstructsInstallation`

Prove that a null adapter factory makes the backend probe unavailable on every platform without
throwing, that the public constructor probes without throwing, and that no reason instructs an
installation. Evidence for `DocDownVisio-Com-VisioComExtractor-ProbesHonestly`.
