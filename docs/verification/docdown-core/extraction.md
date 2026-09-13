## Extraction Subsystem Verification Design

This document describes the verification strategy for the Extraction subsystem, which registers
backends, probes their availability, selects one deterministically for the detected format, and
orchestrates the extraction pipeline end to end.

### Verification Approach

Extraction is verified through subsystem integration tests that exercise its four units —
`DocDownBuilder`, `ExtractorRegistry`, `ExtractorSelector`, and `DocDownEngine` — collaborating as they
would in a real host. Tests reside in `ExtractionTests.cs` under the `Extraction` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `Extraction_`.

Mocking occurs only at the backend boundary, which is the subsystem's one injected dependency. Tests
register `StubExtractor` instances from `DemaConsulting.DocDown.TestSupport`, configured for the scenario under test —
available, unavailable with a reason, a throwing probe, a fully capable text backend, or a deliberately
failing one. Everything inside the subsystem is real: the builder materializes real registrations, the
registry probes real stub availability and caches it, the selector runs its real ranking function, and
the engine drives the real Output subsystem, writing to a real `TempScratch` folder. This is the
correct boundary because the backend is exactly the third-party seam a host supplies, while the
registration, selection, and orchestration logic is the subsystem's own responsibility. Isolated unit
behavior of each class is covered in the four unit chapters under this subsystem.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances configured per scenario; no real extractor required
- **Filesystem**: a per-test `TempScratch` folder receives any layout the orchestration writes
- **Determinism**: a fixed `ExtractionOptions.TimestampUtc` where output is compared
- **Isolation**: each test builds its own engine and scratch folder; no shared state

### Acceptance Criteria

Per IEC 62304 §5.5.2, an Extraction subsystem test run passes when registration exposes exactly the
backends configured, in order; when availability probing skips and explains unusable backends, including
a probe that throws; when selection is a deterministic function of format, options, and candidates and
produces a complete trace; when a backend fault or an absent backend becomes a coded structured failure
rather than an exception; and when the self-validation seam surfaces Core and backend cases. Any thrown
exception that should have been contained, non-deterministic selection, or missing trace entry is a
failure.

### Test Scenarios

#### Registered backends appear in the engine

**Test**: `Extraction_ExplicitRegistration_RegisteredBackends_AppearInEngine`

Proves the subsystem exposes exactly the backends the host registered, in registration order, giving
the host deterministic control over what runs. Evidence for
`DocDownCore-Extraction-ExplicitRegistration`.

#### A throwing availability probe is treated as unavailable

**Test**: `Extraction_AvailabilityProbing_ThrowingProbe_TreatedAsUnavailable`

Proves a backend whose availability probe itself throws is contained and treated as unavailable rather
than aborting the run. Evidence for `DocDownCore-Extraction-AvailabilityProbing`.

#### Selection is deterministic for equal inputs

**Test**: `Extraction_DeterministicSelection_EqualInputs_ProduceEqualSelection`

Proves equal inputs always select the same backend, independent of registration order, which
reproducible extraction depends on. Evidence for `DocDownCore-Extraction-DeterministicSelection`.

#### The selection trace covers every candidate

**Test**: `Extraction_SelectionExplained_MultipleCandidates_TraceCoversEveryCandidate`

Proves the subsystem produces a verdict for every candidate backend, making an unexpected choice
self-diagnosable. Evidence for `DocDownCore-Extraction-SelectionExplained`.

#### A partial satisfier is selected when no full satisfier exists

**Test**: `Extraction_CapabilityNegotiation_RenderRequestedButUnavailable_SelectsPartialSatisfier`

Proves the subsystem negotiates capabilities and selects the best partial satisfier when a requested
capability is unavailable, so an extraction still proceeds with the richest achievable result. Evidence
for `DocDownCore-Extraction-CapabilityNegotiation`.

#### The pipeline runs end to end for a text document

**Test**: `Extraction_Orchestration_TextDocument_RunsPipelineEndToEnd`

Proves the subsystem runs detection, selection, extraction, and output as one sequence, each stage
receiving the previous stage's result. Evidence for `DocDownCore-Extraction-Orchestration`.

#### No available backend yields a coded failure

**Test**: `Extraction_StructuredFailure_NoAvailableBackend_ReturnsFailureWithCode`

Proves that when no backend can run, the subsystem returns a coded structured failure instead of
throwing, so automation can branch on the cause. Evidence for
`DocDownCore-Extraction-StructuredFailure`.

#### Builder mutation after build does not affect the engine

**Test**: `Extraction_OptionIsolation_BuilderMutatedAfterBuild_DoesNotAffectEngine`

Proves an engine's configuration is snapshotted at build time, so later builder mutation cannot change
how a built engine behaves. Evidence for `DocDownCore-Extraction-OptionIsolation`.

#### A backend fault becomes a structured failure

**Test**: `Extraction_ExtractorIsolation_BackendThrows_BecomesStructuredFailure`

Proves an exception thrown by a backend is contained and converted into a coded, reportable failure,
protecting the host process and the output contract. Evidence for
`DocDownCore-Extraction-ExtractorIsolation`.

#### The self-validation seam exposes Core and backend cases

**Test**: `Extraction_SelfValidationSeam_Engine_ExposesCoreAndBackendCases`

Proves the subsystem surfaces Core's own self-test cases together with each backend's cases through one
seam, so a host can validate the contract where it runs. Evidence for
`DocDownCore-Extraction-SelfValidationSeam`.
