### ExtractorRegistry Verification Design

This document describes the unit-level verification strategy for `ExtractorRegistry`, the immutable
snapshot of registered backends that exposes their descriptors, resolves them by identifier, and caches
availability probe results.

#### Verification Approach

`ExtractorRegistry` is verified in isolation through unit tests in `ExtractorRegistryTests.cs` under the
`Extraction` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ExtractorRegistry_`.

The registered backends are stubbed and everything else is real. Tests construct the real registry over
`StubExtractor` instances configured for the scenario — available, unavailable with a reason, or a
throwing probe — and the probe-caching scenario uses a stub that records how many times its availability
probe is called, so the "probes once" and "re-probes after refresh" behaviors are observable. Stubbing
the backend is correct because availability probing is exactly the third-party behavior the registry must
contain; the caching, ordering, diagnostic-recording, and lookup logic under test is the registry's own.
No filesystem or network access is involved.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances, including a probe-counting stub for the caching scenarios
- **Mocking**: the backend availability seam is stubbed; the registry is real
- **Isolation**: each test builds its own registry; no shared state or filesystem access

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExtractorRegistry` unit test run passes when descriptors are exposed in
registration order; when a backend resolves by id and an unknown or empty id is rejected with the
documented exception; when availability is probed once and cached, and re-probed after an explicit
refresh; when a probe that throws or a backend that reports itself unavailable is contained as an
unavailable candidate with a recorded diagnostic; and when exactly one candidate is yielded per backend
regardless of availability. Any repeated probe, lost diagnostic, or missing candidate is a failure.

#### Test Scenarios

##### Descriptors are exposed in registration order

**Test**: `ExtractorRegistry_Descriptors_TwoExtractors_ExposedInRegistrationOrder`

Proves the registry exposes a descriptor per backend in the order they were registered, giving selection
and reporting a stable view. Evidence for `DocDownCore-Extraction-ExtractorRegistry-DescriptorExposure`.

##### Backends resolve by identifier

**Tests**: `ExtractorRegistry_Resolve_KnownId_ReturnsRegisteredInstance`,
`ExtractorRegistry_Resolve_UnknownId_ThrowsKeyNotFoundException`,
`ExtractorRegistry_Resolve_EmptyId_ThrowsArgumentException`,
`ExtractorRegistry_Resolve_NullId_ThrowsArgumentNullException`

Proves a known id resolves to its registered instance while an unknown, empty, or null id fails
clearly. Null and empty are asserted as separate scenarios because they take different branches of
the guard and raise different exception types; covering only the empty branch would leave a
regression in the null branch undetected. Evidence for
`DocDownCore-Extraction-ExtractorRegistry-LookupById`.

##### Availability is probed once and cached

**Test**: `ExtractorRegistry_GetCandidates_RepeatedCalls_ProbesOnce`

Proves each backend's availability is probed once and the result cached, so repeated enumeration does not
re-disturb the environment. Evidence for `DocDownCore-Extraction-ExtractorRegistry-ProbeCaching`.

##### A throwing or unavailable probe is contained with a diagnostic

**Tests**: `ExtractorRegistry_GetCandidates_ThrowingProbe_ContainedAsUnavailableWithDiagnostic`,
`ExtractorRegistry_GetCandidates_UnavailableBackend_RecordsInfoDiagnostic`

Proves a probe that throws, and a backend that reports itself unavailable, are both contained as
unavailable candidates with a recorded diagnostic. Evidence for
`DocDownCore-Extraction-ExtractorRegistry-ProbeFailureContainment`.

##### Availability is re-probed after a refresh

**Test**: `ExtractorRegistry_RefreshAvailability_AfterCaching_ReprobesOnNextEnumeration`

Proves an explicit refresh causes the next enumeration to re-probe, so a newly installed component is
picked up without restarting. Evidence for `DocDownCore-Extraction-ExtractorRegistry-RefreshAvailability`.

##### One candidate is yielded per backend

**Test**: `ExtractorRegistry_GetCandidates_MixedAvailability_YieldsOneCandidatePerExtractor`

Proves exactly one candidate is enumerated per backend regardless of availability, so the selection trace
can explain every backend including the unavailable ones. Evidence for
`DocDownCore-Extraction-ExtractorRegistry-CandidateEnumeration`.

##### Duplicate identifiers are rejected at construction

**Test**: `ExtractorRegistry_Construct_DuplicateIds_ThrowsArgumentException`

Proves constructing a registry over two backends with the same identifier is rejected, keeping selection
and override unambiguous. This is a defensive test with no linked requirement.
