### ExtractorRegistry Verification Design

This document describes the unit-level verification strategy for `ExtractorRegistry`.

#### Verification Approach

`ExtractorRegistry` is verified in `ExtractorRegistryTests.cs` using `StubExtractor` instances. The
stubs control probe behavior while the registry itself remains real, which makes ordering, caching,
identifier resolution, and probe containment directly observable without filesystem access.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances, including throwing and unavailable probes
- **Filesystem**: N/A - registry tests are fully in memory

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `ExtractorRegistry` passes when descriptors remain in registration order,
identifiers resolve as documented, availability is probed once and cached until refreshed, throwing
or null-like probe behavior is contained as unavailability, and exactly one candidate is returned per
registered extractor.

#### Test Scenarios

##### Descriptors are exposed in registration order

**Test**: `ExtractorRegistry_Descriptors_TwoExtractors_AppearInRegistrationOrder`

##### Backends resolve by identifier

**Tests**: `ExtractorRegistry_Resolve_KnownId_ReturnsRegisteredInstance`,
`ExtractorRegistry_Resolve_UnknownId_ThrowsKeyNotFoundException`,
`ExtractorRegistry_Resolve_EmptyId_ThrowsArgumentException`,
`ExtractorRegistry_Resolve_NullId_ThrowsArgumentNullException`

##### Availability is probed once and cached

**Test**: `ExtractorRegistry_GetCandidates_RepeatedCalls_ProbesOnce`

##### Probe failures are contained as unavailability

**Tests**: `ExtractorRegistry_GetCandidates_ThrowingProbe_ContainedAsUnavailable`,
`ExtractorRegistry_GetCandidates_UnavailableBackend_PreservesReason`

##### Availability is re-probed after refresh

**Test**: `ExtractorRegistry_RefreshAvailability_AfterCaching_ReprobesOnNextEnumeration`

##### One candidate is yielded per extractor

**Test**: `ExtractorRegistry_GetCandidates_MixedAvailability_YieldsOneCandidatePerExtractor`
