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

#### Core properties map to document metadata

**Tests**: `OpcMetadataMapper_From_PopulatedProperties_MapsFieldsWithProvenanceAndIsoDates`,
`OpcMetadataMapper_From_BlankValues_OmittedAsFieldsAndRecordedAbsent`,
`OpcMetadataMapper_From_PopulatedInterestingField_NotRecordedAbsent`,
`OpcMetadataMapper_From_UnspecifiedKindDate_TreatedAsUtc`,
`OpcMetadataMapper_From_EmptyProperties_ProducesNoFields`,
`OpcMetadataMapper_From_AllProperties_MapsEachInCanonicalOrder`,
`OpcMetadataMapper_From_LocalKindDate_ConvertedToUtc`,
`OpcMetadataMapper_From_NullProperties_ThrowsArgumentNullException`

Proves the shared mapping from OPC core properties to `DocumentMetadata` records populated fields with
provenance and ISO-8601 dates in canonical order, omits blank values while recording the interesting
ones as absent, treats an unspecified-kind date as UTC, converts a local-kind date to UTC, yields no
fields for an empty property set, and rejects a null property set. These tests are in
`OpcMetadataMapperTests.cs`.

#### Self-test resources load or fail loudly

**Tests**: `Load_PresentResource_ReturnsResourceBytes`, `Load_MissingResource_ThrowsNamingTheResource`,
`Load_MalformedArguments_Throw`

Proves `SelfTestProbe.Load` returns the bytes of a present embedded resource, names a missing resource
in its exception, and rejects malformed arguments instead of returning an empty probe. These tests are
in `SelfTestProbeTests.cs`.

#### Document sources describe and open their input

**Tests**: `Extraction_DocumentSource_FileSource_RecordsIdentityAndOpensFreshStreams`,
`Extraction_DocumentSource_MissingFile_NullSizeAndOpenReadThrows`,
`Extraction_DocumentSource_StreamSource_RewindsAndLeavesCallerStreamOpen`,
`Extraction_DocumentSource_NullOrEmptyArguments_Throw`

Proves a file source records its name, path, and size and opens an independent stream per read, a missing
file has an unknown size and fails only when opened, a stream source is rewound on each read and is never
disposed on the caller
