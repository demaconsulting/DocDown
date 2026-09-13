## Extraction Subsystem Verification Design

This document describes the verification strategy for the Extraction subsystem.

### Verification Approach

Extraction is verified through subsystem tests in `ExtractionTests.cs`. The tests use
`StubExtractor` instances to control backend behavior while the builder, registry, selector, engine,
and Output subsystem run for real. That boundary keeps the third-party extractor seam under test while
proving the subsystem's own registration, selection, and orchestration behavior directly.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances configured per scenario
- **Filesystem**: a per-test `TempScratch` folder for end-to-end orchestration scenarios

### Acceptance Criteria

Per IEC 62304 §5.5.2, Extraction passes when registration is explicit, availability probing contains
failing probes, selection is deterministic, page-render providers are preferred only when pages were
requested, unreadable conditions return `ExtractionFailure` data instead of leaking exceptions,
backend faults are contained, and the self-test seam exposes Core and backend cases together.

### Test Scenarios

#### Registered backends appear in the engine

**Test**: `Extraction_ExplicitRegistration_RegisteredBackends_AppearInEngine`

Proves only explicitly registered extractors participate.

#### A throwing probe is treated as unavailable

**Test**: `Extraction_AvailabilityProbing_ThrowingProbe_TreatedAsUnavailable`

Proves extractor availability probing is contained.

#### Equal inputs select the same backend

**Test**: `Extraction_DeterministicSelection_EqualInputs_ProduceEqualSelection`

Proves selection is deterministic for equal inputs.

#### Requested rendered pages prefer a renderer

**Test**: `Extraction_RenderPreference_RenderRequested_PrefersRenderer`

Proves the render preference outranks ordinary priority only when page rendering was requested.

#### The pipeline runs end to end

**Test**: `Extraction_Orchestration_TextDocument_RunsPipelineEndToEnd`

Proves Detection, Selection, backend execution, and Output serialization run as one sequence.

#### No available backend returns an unreadable failure

**Test**: `Extraction_StructuredFailure_NoAvailableBackend_ReturnsUnreadableFailure`

Proves unavailable extraction returns data rather than throwing.

#### Builder mutation after build does not affect the engine

**Test**: `Extraction_OptionIsolation_BuilderMutatedAfterBuild_DoesNotAffectEngine`

Proves default options are snapshotted into the built engine.

#### A backend fault becomes an unreadable result

**Test**: `Extraction_ExtractorIsolation_BackendThrows_BecomesUnreadableFailure`

Proves extractor exceptions are contained by the engine.

#### The self-test seam exposes Core and backend cases together

**Test**: `Extraction_SelfValidationSeam_Engine_ExposesCoreAndBackendCases`

Proves the engine returns both built-in and backend-contributed self-tests through one interface.
