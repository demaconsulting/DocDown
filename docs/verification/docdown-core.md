# System Verification Design

This document describes the system-level verification strategy for DocDown.Core: how the library's
requirements are proven by tests, how those tests are split so that a cross-platform product can still
carry honest platform evidence, and why the degraded extraction path — not the fully successful one —
is the path the ordinary test suite exercises on every push.

## Verification Approach

DocDown.Core is verified at three levels that mirror its software structure. System tests drive the
whole pipeline end to end through the public `DocDownEngine`; subsystem tests exercise each
architectural boundary (Detection, Extraction, Output) through its own public surface; unit tests
isolate a single class and its documented dependencies. This document covers the system level; the
subsystem and unit levels are covered by the corresponding chapters under *Detection Subsystem
Verification Design*, *Extraction Subsystem Verification Design*, and *Output Subsystem Verification
Design*.

System tests reside in `DocDownCoreTests.cs` within the `DemaConsulting.DocDown.Core.Tests` project.
They are driven through configurable stub backends supplied by the shared `DemaConsulting.DocDown.TestSupport`
project, because Core ships no extractor of its own — the one format Core can exercise end to end on
its own is plain text, and a stub extractor supplies the text backend the pipeline runs.

Requirements-to-test coverage is tracked by the ReqStream trace matrix generated during the build,
not restated in this document. The scenarios below name the test that evidences each requirement so a
reviewer can read the intent, but the authoritative mapping — and its platform source filters — lives
in the requirement YAML under `docs/reqstream/`.

### The two invariants under test

Every scenario in this document ultimately proves one of two invariants stated in the *Design*
document, so they are restated here as the yardstick against which a passing test is judged:

- **Layout invariance.** Every extraction, whatever its outcome, produces the same artifact layout:
  `summary.txt` and `manifest.json` always exist, and the five-slot completeness ledger is always
  resolved. This holds for a clean success, a degraded run, and an outright failure.
- **Honesty.** Whatever was not extracted is stated explicitly with a reason. An absence is never
  merely implied by a missing file. Every reported gap and ledger entry matches the artifacts
  actually on disk.

### Class A and Class B tests

Cross-platform tests split into two disjoint classes, and the split is deliberate because it governs
what a passing test is allowed to assert.

> **Cross-platform tests cannot assert byte-identical or content-identical output. Determinism is
> scoped to an environment, not across environments.**

**Class A — contract/invariant tests. Run everywhere. No source filter.** These assert only what is
universally true regardless of operating system, runtime, or installed applications, and they are the
majority of the suite. They make up the whole of `DocDownCoreTests.cs` and every subsystem and unit
test. What each Class A system test asserts:

| Test | Asserts |
| --- | --- |
| `DocDownCore_Extract_TextDocument_ProducesContractLayout` | full layout, complete, zero gaps for a clean success |
| `DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem` | the honesty test — the verifier finds zero violations |
| `DocDownCore_Extract_NoBackendForFormat_FailsAndStillWritesLayout` | summary and manifest still written |
| `DocDownCore_ExtractAsync_DocxWithoutWordBackend_FailsHonestlyNamingThePackage` | the remedy names the package |
| `DocDownCore_ExtractAsync_DocWithoutWordBackend_FailsNoExtractorNamingLegacyFormat` | fails DD0402 not DD0401 |
| `DocDownCore_Extract_Failure_StillWritesSummaryAndManifest` | no hole in the layout where failures live |
| `DocDownCore_Extract_UnexplainedAbsence_CoreSynthesizesGap` | a silent backend still yields an explained manifest |
| `DocDownCore_Extract_Manifest_RoundTrips` | the manifest reloads without loss |
| `DocDownCore_Extract_Summary_NamesSelectedBackend` | the summary names the backend the manifest records |
| `DocDownCore_Extract_RepeatedRun_IsByteIdentical` | determinism, scoped to one environment at a fixed timestamp |

**Class B — backend/environment-specific content tests. Source-filtered.** These use the ReqStream
source-filter prefixes established in `docs/reqstream/docdown-core/platform-requirements.yaml`:
`windows@`, `ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, and `net10.0@`. A prefix restricts which
test-run results count as evidence for the requirement, so a result recorded on Windows cannot stand
in for the Linux requirement.

> Removing a source filter invalidates the platform evidence: an unfiltered link would let a pass on
> any one operating system or runtime satisfy a requirement that specifically demands another. The
> filters are what make the six platform requirements provable rather than merely asserted.

In Phase 1 the six platform requirements
(`DocDownCore-Platform-Windows`, `-Linux`, `-MacOS`, `-Net8`, `-Net9`, `-Net10`) each link to the
single system test `DocDownCore_Extract_TextDocument_ProducesContractLayout` under their respective
source filter. The CI matrix runs that test on Windows, Linux, and macOS across net8.0, net9.0, and
net10.0, and each run contributes evidence only to the platform whose filter it matches. As
format-specific backends arrive in later phases, Class B grows to include backend-specific content
tests (for example page rendering that runs only where a renderer is registered); the class structure
is defined now so those tests attach to the right requirements without restructuring.

### Degraded extraction is the default path under test

> **Degraded extraction is the common CI path, not an edge case.** Continuous integration has no
> Microsoft Office and, in Phase 1, no registered page renderer, so the fallback, capability-narrowing
> and gap-reporting paths are the ones the ordinary test suite exercises on every push. The Core test
> architecture leans into this: the default test fixture is a capability-limited or unavailable stub,
> and the fully-successful extraction is the special case.

The system-test suite is therefore built around a degraded-by-default matrix. Most rows produce an
incomplete or failed run; the fully successful row is the exception. Every row ends by confirming the
contract verifier finds no violations, so gap accuracy is asserted precisely on the degraded rows,
which is where it matters. Each row names the real system test:

| Scenario | System test |
| --- | --- |
| No backend for format | `DocDownCore_Extract_NoBackendForFormat_FailsAndStillWritesLayout` |
| Backend package not installed | `DocDownCore_ExtractAsync_DocxWithoutWordBackend_FailsHonestlyNamingThePackage` |
| Legacy binary format detected | `DocDownCore_ExtractAsync_DocWithoutWordBackend_FailsNoExtractorNamingLegacyFormat` |
| Backend registered but unavailable | `DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary` |
| Higher-fidelity backend unavailable | `DocDownCore_Extract_HigherFidelityUnavailable_FallsBackAndRecordsExclusion` |
| Available but missing a capability | `DocDownCore_Extract_PagesRequestedButUnsupported_DegradesWithGap` |
| Required capability unsatisfiable | `DocDownCore_Extract_RequiredCapabilitiesUnavailable_FailsWithoutDegrading` |
| Override names an unavailable backend | `DocDownCore_Extract_OverrideUnavailable_FailsAndNeverFallsBack` |
| Extractor silent about an absence | `DocDownCore_Extract_UnexplainedAbsence_CoreSynthesizesGap` |
| Fully successful | `DocDownCore_Extract_TextDocument_ProducesContractLayout` |

### The reconciliation test is the highest-value test in the repository

The reconciliation test — `DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`, backed by
`ContractVerifier` — is the highest-value test in the repository, because it makes *honesty itself*
verifiable rather than aspirational. Every extraction test in every test project ends with
`ContractAssert.NoViolations(scratchFolder)`, which reconciles the manifest against the bytes on disk
in both directions: every file the manifest lists exists, every file on disk is listed, every hash
matches, and every partial or absent artifact is explained by a gap. A backend that under-reports a
gap therefore fails its own test suite, because the reconciliation appended to that very test detects
the divergence.

### Shared test-support project

The shared `DemaConsulting.DocDown.TestSupport` project provides four pieces of infrastructure to Core, to the
Pdf test project, and to every future test project — Tool and the Phase 2 Office packages:

- `StubExtractor` — a configurable backend with selectable identity, formats, declared and effective
  capabilities, priority, and availability (available, unavailable-with-reason, or throwing probe),
  plus a scripted extraction body that can write, report, stay deliberately silent, or throw.
- `RecordingSink` — an `IExtractionSink` that records every call in order without touching the
  filesystem, for asserting exactly what a backend emitted.
- `ContractAssert` — `ContractAssert.NoViolations(scratchFolder)` and
  `ContractAssert.LayoutPresent(scratchFolder)`, wrapping the verifier and the layout check.
- `TempScratch` — a disposable, uniquely named temporary folder so tests never share a fixed scratch
  location and run safely in parallel.

Per the design-documentation standard, test projects and test infrastructure are excluded from
*design* documentation scope, so `DemaConsulting.DocDown.TestSupport` carries no requirements, design, or
verification companion artifacts. It **is** covered by ReviewMark and has its own repo-level
review-set, so the shared infrastructure is still formally reviewed.

### Golden-file determinism

Determinism is proven by two complementary Class A tests, both scoped to a single environment:

- `DocDownCore_Extract_RepeatedRun_IsByteIdentical` runs the extraction twice into the **same** scratch
  folder — so the absolute scratch path is identical between runs — with a fixed
  `ExtractionOptions.TimestampUtc` and the clean-if-DocDown scratch mode, then asserts the `summary.txt`
  and `manifest.json` byte arrays are equal. A fixed timestamp is what makes literal byte-identity
  achievable.
- `DocDownCore_Extract_RepeatedRunWithSystemClock_DiffersOnlyInTimestamp` runs twice with the system
  clock instead of a fixed timestamp and asserts the two runs differ **only** in the timestamp line,
  proving nothing else in the output depends on wall-clock time or run order.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline across the Windows, Linux,
  and macOS runners
- **Backends**: no real extractor is required; `StubExtractor` from `DemaConsulting.DocDown.TestSupport` supplies the
  text backend the pipeline runs, and the absence of Office and of a page renderer on CI is the normal,
  intended condition
- **Filesystem**: each test owns a `TempScratch` folder under the system temp path, created and deleted
  per test, so there is no shared state and tests run in parallel
- **Dependencies**: no external services, databases, or network access; Core has no runtime NuGet
  dependencies beyond the .NET base class library
- **Determinism controls**: a fixed `ExtractionOptions.TimestampUtc` and `CultureInfo.InvariantCulture`
  formatting, so byte-identity assertions are stable within an environment

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations, so the recorded
  result and the artifacts on disk tell the same story.
- Layout invariance holds for every outcome: `summary.txt` and `manifest.json` are present and the
  completeness ledger is resolved even for failed runs.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.
- The two determinism tests hold within each environment: byte-identical output at a fixed timestamp,
  and timestamp-only variation under the system clock.

## Test Scenarios

Each scenario below corresponds to one system requirement and names the real test method that
evidences it. Platform requirements are covered by the source-filtered runs of the layout scenario, as
described under *Class A and Class B tests*.

### Output contract is self-describing and verifiable

**Tests**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`,
`DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

Proves a downstream consumer can validate an extraction without the original document: a clean run
produces the full contract layout, and the verifier reconciles the manifest against disk with zero
violations. Evidence for `DocDownCore-OutputContract`.

### Layout is written for every outcome including failure

**Tests**: `DocDownCore_Extract_NoBackendForFormat_FailsAndStillWritesLayout`,
`DocDownCore_Extract_Failure_StillWritesSummaryAndManifest`

Proves the summary and manifest exist even when no backend fits the format and when extraction fails
after the folder is prepared, so an operator can always distinguish a recorded failure from a missing
input. Evidence for `DocDownCore-LayoutInvariance`.

### Human-readable summary describes what was and was not extracted

**Tests**: `DocDownCore_Extract_Summary_ContainsAbsoluteScratchPath`,
`DocDownCore_Extract_Summary_NamesSelectedBackend`

Proves the summary states the absolute scratch-folder path and names the selected backend, giving a
human the first plain-language account of the run. Evidence for `DocDownCore-SummaryFile`.

### Machine-readable manifest round-trips the result

**Test**: `DocDownCore_Extract_Manifest_RoundTrips`

Proves the manifest can be reloaded and acted on without loss, which reproducible automated processing
depends on. Evidence for `DocDownCore-ManifestFile`.

### Extracted content is written as Markdown

**Test**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`

Proves a successful extraction writes `content.md`, giving every downstream renderer and indexer one
predictable representation. Evidence for `DocDownCore-ContentFile`.

### Images are deduplicated and their presence or absence reflected on disk

**Tests**: `DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`,
`DocDownCore_Extract_TextDocument_ProducesContractLayout`

Proves the images folder on disk matches what the manifest claims, so a consumer never silently loses
a picture. Evidence for `DocDownCore-ImageFolder`.

### Rendered pages are written when supported and otherwise explained

**Tests**: `DocDownCore_Extract_PagesRequestedButUnsupported_DegradesWithGap`,
`DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

Proves that when the environment cannot render pages, their absence is reported with an explaining gap
rather than left to guesswork — the ordinary CI condition. Evidence for `DocDownCore-PageFolder`.

### Format is determined before a backend is selected

**Tests**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`,
`DocDownCore_Extract_UnknownFormat_FailsWithDiagnostic`

Proves the pipeline detects the true format from content and file name, routing a recognized document
to a capable backend and failing an unrecognized one cleanly with a diagnostic. Evidence for
`DocDownCore-FormatDetection`.

### Only explicitly registered backends are used

**Test**: `DocDownCore_GetBackendStatus_ReportsAvailabilityAndReason`

Proves the engine exposes exactly the backends the host registered, giving the host deterministic
control over which software runs against its documents. Evidence for `DocDownCore-ExplicitRegistration`.

### Highest-fidelity available backend is selected

**Test**: `DocDownCore_Extract_HigherFidelityUnavailable_FallsBackAndRecordsExclusion`

Proves that when a higher-fidelity backend is unavailable, selection falls back to the best available
one and records the exclusion and its reason in the trace. Evidence for `DocDownCore-BackendSelection`.

### A failing availability probe marks a backend unavailable with a reason

**Tests**: `DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary`,
`DocDownCore_GetBackendStatus_ReportsAvailabilityAndReason`

Proves an unavailable backend is skipped and its reason surfaces verbatim in the summary and status,
so operators can fix the environment instead of receiving an opaque crash. Evidence for
`DocDownCore-BackendAvailability`.

### A caller-named backend is honored and never silently substituted

**Test**: `DocDownCore_Extract_OverrideUnavailable_FailsAndNeverFallsBack`

Proves that when a caller names a backend that cannot run, the extraction fails rather than substituting
another, preserving the caller's deliberate policy. Evidence for `DocDownCore-BackendOverride`.

### Every candidate's selection verdict is reported

**Tests**: `DocDownCore_Extract_HigherFidelityUnavailable_FallsBackAndRecordsExclusion`,
`DocDownCore_Extract_Summary_NamesSelectedBackend`

Proves the selection records why each candidate was or was not chosen and names the winner, turning an
opaque decision into an auditable one. Evidence for `DocDownCore-SelectionReported`.

### Failures are structured and coded, not thrown

**Tests**: `DocDownCore_Extract_NoBackendForFormat_FailsAndStillWritesLayout`,
`DocDownCore_Extract_UnknownFormat_FailsWithDiagnostic`,
`DocDownCore_Extract_Failure_StillWritesSummaryAndManifest`

Proves each failure path returns a coded, structured outcome and still writes a verifiable layout, so
batch automation can branch on the cause without handling exceptions. Evidence for
`DocDownCore-StructuredFailure`.

### Caller options are isolated from later mutation

**Test**: `DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult`

Proves mutating the options object after the call cannot affect the in-flight or completed extraction,
which is essential for safe concurrent use. Evidence for `DocDownCore-Options`.

### Missing optional capabilities degrade with an explained gap

**Tests**: `DocDownCore_Extract_PagesRequestedButUnsupported_DegradesWithGap`,
`DocDownCore_Extract_RequiredCapabilitiesUnavailable_FailsWithoutDegrading`,
`DocDownCore_Extract_HigherFidelityUnavailable_FallsBackAndRecordsExclusion`

Proves a missing optional capability degrades the run with an explaining gap while a caller-required
capability that cannot be met fails hard, preserving the usable result without hiding the loss.
Evidence for `DocDownCore-Degradation`.

### Coded diagnostics explain notable conditions

**Tests**: `DocDownCore_Extract_UnknownFormat_FailsWithDiagnostic`,
`DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary`

Proves notable conditions carry machine-stable codes so tooling can recognize recurring situations
without parsing free-form English. Evidence for `DocDownCore-Diagnostics`.

### All artifacts are confined to the scratch folder

**Test**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`

Proves an end-to-end extraction writes only inside the caller-provided scratch folder; the adversarial
containment proof for hostile names lives at the unit level in *ScratchFolder Verification Design*.
Evidence for `DocDownCore-ScratchFolderSafety`.

### Output is byte-identical for identical input at a fixed timestamp

**Tests**: `DocDownCore_Extract_RepeatedRun_IsByteIdentical`,
`DocDownCore_Extract_RepeatedRunWithSystemClock_DiffersOnlyInTimestamp`

Proves two runs of the same input into the same scratch folder produce byte-identical output at a fixed
timestamp, and differ only in the timestamp line under the system clock. Evidence for
`DocDownCore-Determinism`.

### A numbered gap is reported for every undelivered output

**Tests**: `DocDownCore_Extract_PagesRequestedButUnsupported_DegradesWithGap`,
`DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

Proves every part of a requested extraction that could not be delivered is recorded as a numbered gap
a consumer can enumerate. Evidence for `DocDownCore-GapReporting`.

### Reported gaps and ledger entries match the filesystem

**Test**: `DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

Proves the completeness ledger and every gap are reconciled against the artifacts actually on disk
before reporting, so the record and the filesystem agree. Evidence for `DocDownCore-GapAccuracy`.

### No absence is left unexplained

**Tests**: `DocDownCore_Extract_UnexplainedAbsence_CoreSynthesizesGap`,
`DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem`

Proves that when a backend leaves an absence silent, Core synthesizes an explaining gap, so the
worst-case output is an admission of ignorance rather than a false impression of completeness. Evidence
for `DocDownCore-NoSilentAbsence`.

### Producing tool and backend are recorded

**Tests**: `DocDownCore_Extract_Summary_NamesSelectedBackend`,
`DocDownCore_Extract_Manifest_RoundTrips`

Proves both the summary and the manifest attribute the extraction to the tool and backend that produced
it, supporting reproducibility and selective distrust. Evidence for `DocDownCore-ProvenanceReported`.

### Runtime environment is recorded

**Test**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`

Proves the output records the operating system and runtime the extraction ran under, letting an auditor
reproduce the conditions and explaining environment-dependent gaps. Evidence for
`DocDownCore-EnvironmentReported`.

### Self-test cases are exposed for on-demand validation

**Test**: `DocDownCore_GetSelfTestCases_IncludesCoreCases`

Proves the engine exposes Core's own self-test cases, letting a host confirm the contract in its
deployed environment rather than trusting the build machine. Evidence for
`DocDownCore-SelfValidationSeam`.

### Image provenance is stated per extracted image

**Test**: `DocDownCore_Extract_ImageProvenance_ManifestRecordsTransformPerImage`

Proves the library states, for every image it extracts, how that image was produced. A backend that hands
Core one image in the document's own encoding and one it decoded and re-encoded yields a manifest whose two
image entries carry distinct, truthful transforms and matching media types. This is the honesty invariant
applied to a field that is present rather than absent: a false provenance claim would be as damaging as a
silent omission, because it would be believed. Evidence for `DocDownCore-ImageProvenanceReported`.
