### DocDownEngine Verification Design

This document describes the unit-level verification strategy for `DocDownEngine`.

#### Verification Approach

`DocDownEngine` is verified in `DocDownEngineTests.cs`. `StubExtractor` controls backend behavior,
while Detection and Output run for real against a `TempScratch` folder. That makes the engine's own
responsibilities observable: option snapshotting, scratch-folder handling, selection, note emission,
failure containment, and result assembly.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances scripted per scenario
- **Filesystem**: a per-test `TempScratch` folder receives any real output

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `DocDownEngine` passes when it runs the documented pipeline order, never gives a
backend the scratch path, clones options before use, returns only `Produced` or `Unreadable`, records
render-request notes correctly, contains backend faults, reports registered candidates, exposes the
two Core self-test cases, propagates cancellation, and rejects null or empty required arguments.

#### Test Scenarios

##### A non-paginated render request succeeds silently

**Test**: `DocDownEngine_ExtractAsync_NonPaginatedFormat_RenderRequest_SucceedsSilently`

##### A paginated render request with no renderer records a note

**Test**: `DocDownEngine_ExtractAsync_PaginatedFormat_RenderRequestUnavailable_RecordsNote`

##### A refused scratch folder returns failure without writing artifacts

**Test**: `DocDownEngine_ExtractAsync_ScratchRefused_ReturnsFailureWithoutWritingLayout`

##### Unknown input does not invoke the backend

**Test**: `DocDownEngine_ExtractAsync_UnknownFormat_DoesNotInvokeBackend`

##### Caller options are cloned before reaching the backend

**Test**: `DocDownEngine_ExtractAsync_CallerOptions_AreClonedBeforeReachingBackend`

##### The backend receives sink context without the scratch path

**Test**: `DocDownEngine_ExtractAsync_Backend_ReceivesSinkContextWithoutScratchPath`

##### A backend fault returns an unreadable layout

**Test**: `DocDownEngine_ExtractAsync_BackendThrows_ReturnsUnreadableLayout`

##### Registered candidates are reported with availability and render support

**Test**: `DocDownEngine_GetBackends_MixedBackends_ReportsAvailabilityAndRenderedPageSupport`

##### The current Core self-test cases are exposed

**Tests**: `DocDownEngine_GetSelfTestCases_NoBackends_IncludesTwoCoreCases`,
`DocDownEngine_GetSelfTestCases_UnavailableBackend_WrapsCasesAsSkipped`

##### Cancellation propagates

**Test**: `DocDownEngine_ExtractAsync_CanceledToken_PropagatesOperationCanceled`

##### Null and empty required arguments are rejected

**Tests**: `DocDownEngine_ExtractAsync_NullSource_ThrowsArgumentNullException`,
`DocDownEngine_ExtractAsync_EmptyScratchFolder_ThrowsArgumentException`,
`DocDownEngine_ExtractAsync_EmptyDocumentPath_ThrowsArgumentException`

##### A produced run may contain both images and pages

**Test**: `DocDownEngine_ExtractAsync_ImagesAndPages_ProducesBothArtifacts`
