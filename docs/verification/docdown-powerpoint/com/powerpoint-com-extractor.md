### PowerPointComExtractor Verification Design

This document describes the unit-level verification strategy for `PowerPointComExtractor`, the
rendering backend.

### Verification Approach

`PowerPointComExtractor` is verified through unit tests in `Com/PowerPointComExtractorTests.cs` in
`DemaConsulting.DocDown.PowerPoint.Tests`, exercising the whole extraction path through an injected
stub `IPowerPointAutomation` with no Microsoft Office present. The stub lets CI prove the delegation
to the managed backend, the rendering of every slide, the per-slide fault isolation, the
render-resolution pass-through, the rendering-fact reconciliation, and the probe behavior —
everything the backend does apart from talking to PowerPoint. The real adapter it constructs by
default is proved separately by release-time self-tests.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: PresentationML decks generated at test time by `TestData/PptxFixtures.cs`, materialized
  to a per-test `TempScratch` folder; synthetic PNG payloads from `TestData/StubPowerPointAutomation.cs`
- **Mocking**: a stub `IPowerPointAutomation` stands in for Microsoft PowerPoint on every platform; a
  recording sink captures pages, notes, and environment facts
- **Isolation**: each test builds its own deck and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointComExtractor` unit test run passes when the extractor exposes the
expected backend identity, supported format, lower priority, and page-rendering applicability; the
backend delegates the guaranteed content to the managed backend and writes the speaker notes; renders
every slide through the seam at the caller's requested resolution; records a one-sentence note naming
a slide that could not be rendered while continuing; suppresses the delegated backend's "rendering not
provided" fact while emitting the authoritative renderer fact; and probes unavailable without throwing
and without instructing an installation. Any silent slide loss, contradictory fact, or throwing probe
is a failure.

### Test Scenarios

#### The selection surface matches the supported contract

**Test**: `PowerPointComExtractor_Descriptor_MatchesContract`

Proves the identifier, supported format, priority-zero tie-break, and page-rendering applicability.
Evidence for `DocDownPowerPoint-Com-PowerPointComExtractor-DescribesSelectionSurface`.

#### The managed content is delegated

**Test**: `PowerPointComExtractor_Extract_ViaStub_WritesNotesAndRendersEverySlide`

Proves the backend writes the guaranteed content — including the speaker notes no render can supply —
by delegating to the managed backend, and adds one rendered page per slide, disposing the session.
Evidence for `DocDownPowerPoint-Com-PowerPointComExtractor-DelegatesManagedContent`.

#### The composed rendering facts are reconciled

**Test**: `PowerPointComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact`

Proves the COM run suppresses the delegated managed backend's "rendering not provided" fact and emits
the authoritative `pages.renderer : available` fact, so the composed report is internally consistent.
Evidence for `DocDownPowerPoint-Com-PowerPointComExtractor-ReconcilesRenderingFacts`.

#### Every slide renders at the requested resolution

**Test**: `PowerPointComExtractor_Extract_PassesRenderDpiToAutomation`

Proves the caller's render resolution is passed through to the automation seam, and every slide is
rendered and added as a page. Evidence for
`DocDownPowerPoint-Com-PowerPointComExtractor-RendersEverySlide`.

#### A failed slide becomes a plain note

**Test**: `PowerPointComExtractor_Extract_SlideRenderFails_ReportsNote`

Proves a slide that fails to render is recorded as a one-sentence note while the remaining slides
still render. Evidence for
`DocDownPowerPoint-Com-PowerPointComExtractor-ReportsSlideRenderFailureNote`.

#### The probe is honest and instructs no installation

**Tests**: `PowerPointComExtractor_NullFactory_ProbesUnavailable`,
`PowerPointComExtractor_PublicCtor_ProbeDoesNotThrowAndNeverInstructsInstallation`

Prove that a null adapter factory makes the backend probe unavailable on every platform without
throwing, that the public constructor probes without throwing, and that no reason instructs an
installation. Evidence for `DocDownPowerPoint-Com-PowerPointComExtractor-ProbesHonestly`.
