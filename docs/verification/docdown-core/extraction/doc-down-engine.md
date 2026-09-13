### DocDownEngine Verification Design

This document describes the unit-level verification strategy for `DocDownEngine`, the public facade that
orchestrates one extraction end to end: it validates arguments, snapshots options, prepares output, runs
the selected backend in isolation, and reports backend status and self-test cases.

#### Verification Approach

`DocDownEngine` is verified in isolation through unit tests in `DocDownEngineTests.cs` under the
`Extraction` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`DocDownEngine_`.

The backend is the injected seam and is stubbed; the rest of the pipeline is real. Tests drive the engine
through `StubExtractor` backends configured per scenario — available, unavailable, silent, or deliberately
failing — while the Detection and Output subsystems the engine orchestrates run for real, writing a
genuine layout into a per-test `TempScratch` folder. One scenario substitutes a recording context so it
can prove the backend is handed a sink and context that carry **no** scratch path, evidencing the
invariant that an extractor never sees a filesystem path. Stubbing only the backend is the correct
boundary because the extractor is the third-party seam, while argument validation, option snapshotting,
fault isolation, and failure-with-layout are the engine's own responsibilities. A fixed
`ExtractionOptions.TimestampUtc` keeps any written output deterministic.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances configured per scenario; a recording context for the no-path proof
- **Filesystem**: a per-test `TempScratch` folder receives any layout the engine writes
- **Determinism**: a fixed `ExtractionOptions.TimestampUtc`
- **Mocking**: the backend seam is stubbed; Detection and Output run for real
- **Isolation**: each test builds its own engine and scratch folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `DocDownEngine` unit test run passes when stages run in a fixed order and a
backend is never invoked for unknown input; when the backend receives a sink context with no scratch
path; when options are cloned on entry so later mutation cannot affect the run; when a backend fault
becomes an `ExtractorFailed` failure; when a refused scratch folder returns a structured failure before
any layout is written; when a post-preparation failure still writes a verifiable layout; when backend
status and self-test cases are reported, with an unavailable backend's cases wrapped as skipped; when
cancellation propagates; and when null or empty arguments are rejected. Any leaked exception, written
scratch path, or unverifiable failure layout is a failure.

#### Test Scenarios

##### Stages run in order and skip the backend for unknown input

**Tests**: `DocDownEngine_ExtractAsync_UnknownFormat_DoesNotInvokeBackend`,
`DocDownEngine_ExtractAsync_Backend_ReceivesSinkContextWithoutScratchPath`

Proves the backend is never invoked when detection fails and, when it is invoked, receives a sink
context carrying no scratch path. The second claim is asserted, not merely stated: reflection walks
every public member of `IExtractionContext` and of the concrete context type and asserts that none
carries `Path`, `Folder`, `Directory`, or `Scratch` in its name, then reads every public string
member and asserts none holds the scratch path under some other name. A member added later — however
innocently named — fails this test rather than silently leaking the path. Evidence for
`DocDownCore-Extraction-DocDownEngine-PipelineOrder`.

##### A page-render request degrades a paginated format but stays silent for a non-paginated one

**Tests**: `DocDownEngine_ExtractAsync_NonPaginatedFormat_RenderRequest_SucceedsSilently`,
`DocDownBuilder_ConfigureDefaults_RenderPages_ProducesPageGapByDefault`

Proves page rendering is treated as a request: when the selected backend cannot render a **paginated**
format, the run degrades with a counted `pages` gap; when the format is **non-paginated**, the request
is honored with silence — an informational `DD0303` diagnostic records that it applied to nothing, no
gap is emitted, and the run does not degrade, because an absence no environment could ever fill is not a
shortfall. Evidence for `DocDownCore-Extraction-DocDownEngine-RenderRequestApplicability`.

##### Options are cloned before reaching the backend

**Test**: `DocDownEngine_ExtractAsync_CallerOptions_AreClonedBeforeReachingBackend`

Proves the caller's options are cloned on entry so a later mutation cannot corrupt the extraction the
backend sees. Evidence for `DocDownCore-Extraction-DocDownEngine-OptionsSnapshot`.

##### A backend fault becomes an ExtractorFailed failure

**Test**: `DocDownEngine_ExtractAsync_BackendThrows_ReturnsExtractorFailedFailure`

Proves an exception thrown by a backend is caught and converted into a coded `ExtractorFailed` failure.
Evidence for `DocDownCore-Extraction-DocDownEngine-ExtractorIsolation`.

##### A refused scratch folder returns a structured failure

**Test**: `DocDownEngine_ExtractAsync_ScratchRefused_ReturnsFailureWithoutWritingLayout`

Proves a refused scratch folder returns a structured `ScratchFolderRefused` failure before any layout
is written. The test asserts the **whole** layout is absent — `summary.txt`, `manifest.json`,
`content.md`, and the `images/`, `pages/`, and `parts/` folders — and further asserts the folder
still contains exactly the caller's pre-existing file with its original contents, and nothing else.
Asserting only that `summary.txt` is absent would let a regression that wrote the manifest into a
refused folder pass. Evidence for `DocDownCore-Extraction-DocDownEngine-FailureReturned`.

##### A post-preparation failure still writes a verifiable layout

**Test**: `DocDownEngine_ExtractAsync_BackendThrows_StillWritesVerifiableLayout`

Proves that when extraction fails after the folder is prepared, the full verifiable layout is still
written, so every outcome leaves auditable evidence. Evidence for
`DocDownCore-Extraction-DocDownEngine-FailureArtifacts`.

##### Backend status is reported

**Test**: `DocDownEngine_GetBackendStatus_MixedBackends_ReportsAvailabilityAndCapabilities`

Proves the engine reports each backend's availability and capabilities, supporting environment diagnosis.
Evidence for `DocDownCore-Extraction-DocDownEngine-BackendStatus`.

##### Self-test cases are exposed and skipped when unavailable

**Tests**: `DocDownEngine_GetSelfTestCases_NoBackends_IncludesThreeCoreCases`,
`DocDownEngine_GetSelfTestCases_UnavailableBackend_WrapsCasesAsSkipped`

Proves the engine exposes Core's own self-test cases and wraps an unavailable backend's cases as skipped
rather than failing. Evidence for `DocDownCore-Extraction-DocDownEngine-SelfTestCases`.

##### Cancellation propagates

**Test**: `DocDownEngine_ExtractAsync_CanceledToken_PropagatesOperationCanceled`

Proves a canceled token propagates an operation-canceled outcome, letting a host stop work promptly.
Evidence for `DocDownCore-Extraction-DocDownEngine-Cancellation`.

##### Null and empty arguments are rejected

**Tests**: `DocDownEngine_ExtractAsync_NullSource_ThrowsArgumentNullException`,
`DocDownEngine_ExtractAsync_EmptyScratchFolder_ThrowsArgumentException`,
`DocDownEngine_ExtractAsync_EmptyDocumentPath_ThrowsArgumentException`

Proves a null source and an empty scratch folder or document path are rejected up front with the
documented exceptions. Evidence for `DocDownCore-Extraction-DocDownEngine-RejectNullArguments`.
