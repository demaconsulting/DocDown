# System Verification Design

This document describes the system-level verification strategy for DocDown.Core.

## Verification Approach

DocDown.Core is verified through end-to-end tests in `DocDownCoreTests.cs`, driven through the public
`DocDownEngine` with stub extractors from `DemaConsulting.DocDown.TestSupport`. Core ships no real
format-specific backend of its own, so the stubs are the correct system-level seam: the engine,
selection logic, and output writers all run for real while the tests control exactly what the backend
reports.

Requirements-to-test coverage is tracked in ReqStream. This document names the system scenarios and
current test methods so reviewers can read the verification intent without re-deriving it from code.

The system-level suite is organized around the current reporting model:

- **Two-state outcomes** — the system either produces output or returns an unreadable failure.
- **Inventory plus notes** — the output records what was extracted through counts and reports only
  attempted-but-incomplete steps through notes.
- **Deterministic structure** — the root artifacts and serializer behavior stay stable within one
  environment.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances scripted per scenario
- **Filesystem**: a per-test `TempScratch` folder receives the real output artifacts
- **Dependencies**: no external services or network access

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system test run passes when:

- a successful extraction writes the expected root artifacts and any produced content resources;
- unreadable outcomes return `ExtractionFailure` data and, except for scratch-folder refusal, write
  the normal root artifacts;
- the selected backend and runtime context appear in the output where specified;
- package-hint and legacy-format failure prose remain honest and specific;
- fixed-timestamp runs are byte-identical within one environment;
- the self-test seam exposes the current Core case names.

## Test Scenarios

### A text extraction produces the standard layout

**Test**: `DocDownCore_Extract_TextDocument_ProducesContractLayout`

Proves a successful extraction returns `Produced`, writes `content.md`, and establishes the standard
root artifacts.

### A missing modern-format backend names its owning package

**Test**: `DocDownCore_ExtractAsync_DocxWithoutWordBackend_FailsHonestlyNamingThePackage`

Proves a detected `.docx` document with no matching backend returns `Unreadable` and names
`DemaConsulting.DocDown.Word` in the failure prose.

### A legacy binary format is stated unsupported

**Test**: `DocDownCore_ExtractAsync_DocWithoutWordBackend_FailsNoExtractorNamingLegacyFormat`

Proves legacy binary Office formats are named honestly and reported as unsupported without inventing
an owning package.

### An unavailable backend's reason reaches the summary

**Test**: `DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary`

Proves unavailable backend prose survives into `summary.txt` verbatim.

### A requested render with no renderer produces output plus a note

**Test**: `DocDownCore_Extract_PagesRequestedButUnsupported_ProducesWithNote`

Proves page rendering remains a request rather than a third outcome state: extraction still succeeds
and records a note stating that pages were requested but not rendered.

### An unrecognized format returns an unreadable result

**Test**: `DocDownCore_Extract_UnknownFormat_ReturnsUnreadableResult`

Proves unknown input yields `Unreadable` and an `Unknown` detected format rather than mis-routing the
document.

### A backend failure still writes the root artifacts

**Test**: `DocDownCore_Extract_Failure_StillWritesSummaryAndManifest`

Proves a backend exception is contained as an unreadable result and still leaves the normal summary and
manifest record in place.

### Fixed-timestamp runs are byte-identical

**Test**: `DocDownCore_Extract_RepeatedRun_IsByteIdentical`

Proves `summary.txt` and `manifest.json` are byte-identical between two runs with the same fixed
TimestampUtc in one environment.

### Option mutation after the call does not affect the completed run

**Test**: `DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult`

Proves the engine snapshots the caller's options before extraction begins.

### The self-test seam exposes the current Core cases

**Test**: `DocDownCore_GetSelfTestCases_IncludesCurrentCoreCases`

Proves the built-in case names are `core.layout-invariance` and `core.manifest-schema`.

### Per-image provenance reaches the manifest

**Test**: `DocDownCore_Extract_ImageProvenance_ManifestRecordsTransformPerImage`

Proves the manifest records each image's transform individually rather than flattening provenance to a
single run-level claim.

### Image referrers, the scalar alias, and the template flag reach the manifest

**Test**: `DocDownCore_Extract_ImageReferrers_ManifestRecordsPagesAliasAndTemplateFlag`

Proves the manifest names every referring page of an image in `sourcePages`, repeats the first of
them in the scalar `sourcePage` alias, and sets `referencedByTemplate` for an image reached only
through a template container rather than giving it a fabricated page. Evidence for
`DocDownCore-ImageReferrersReported`.

### Every consumer entry point ships a runnable example

**Test**: `DocDownCore_ApiExamples_ConsumerEntryPoints_CarryRunnableExamples`

Proves the builder, the extraction entry point, and all six backend registration methods each carry
an example with a C# code block that builds an engine, so the API reference generated into every
package's `api/` folder ships a runnable sample offline. The registration set is asserted by name, so
a newly added format package cannot pass the check by being absent. Evidence for the repository-level
`Quality-ApiExampleCoverage` requirement.
