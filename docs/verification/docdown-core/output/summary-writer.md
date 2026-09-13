### SummaryWriter Verification Design

This document describes the unit-level verification strategy for `SummaryWriter`, which produces the
human-readable `summary.txt`: every mandatory section, the absolute scratch path, the selected backend,
the environment, enumerated gaps, and a verbatim failure explanation, deterministically.

#### Verification Approach

`SummaryWriter` is verified in isolation through unit tests in `SummaryWriterTests.cs` and golden-file
tests in `SummaryWriterGoldenTests.cs`, both under the `Output` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `SummaryWriter_`.

Nothing is mocked. The writer serializes an extraction's recorded result and sink state to disk, so tests
assemble the real result and environment values each scenario needs, prepare a real `ScratchFolder` over a
per-test `TempScratch` folder, invoke the writer, and read the produced `summary.txt` back. The writer's
value is the exact text it emits — including fixed line endings for reproducibility — so exercising the
real serialization against a real file is the only way to evidence the mandatory sections, the verbatim
failure text, and byte-identical output; a mock file system would defeat the determinism assertion.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder receives the produced `summary.txt`
- **Inputs**: result, environment facts, gaps, and failure values assembled per scenario
- **Mocking**: none; the real writer produces real text
- **Isolation**: each test owns its scratch folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `SummaryWriter` unit test run passes when every mandatory section is present, with
an explicit note when a section has nothing to report; when the header states the absolute scratch path;
when a successful run names the selected backend; when the runtime and recorded environment facts are
reported and grouped under their contributing component; when every gap, including a Core-synthesized
one, is enumerated; when a failed run reproduces the failure explanation verbatim; when the same content
produces byte-identical summary text; and when the rendered output for each representative scenario
matches its committed golden file (after normalizing only the machine-specific scratch-folder and
source-document lines). Any missing section, paraphrased failure, non-deterministic byte, unattributed
environment fact, or golden mismatch is a failure.

#### Test Scenarios

##### Every mandatory section is present

**Test**: `SummaryWriter_WriteAsync_CleanRun_ContainsEveryMandatorySection`

Proves the summary always carries every mandatory section, stating explicitly when one has nothing to
report, so an omission is never mistaken for a missing feature. Evidence for
`DocDownCore-Output-SummaryWriter-MandatorySections`.

##### The header states the absolute scratch path

**Test**: `SummaryWriter_WriteAsync_HeaderBlock_ContainsAbsoluteScratchPath`

Proves the header states the absolute scratch-folder path, removing ambiguity about where the artifacts
are. Evidence for `DocDownCore-Output-SummaryWriter-AbsoluteScratchPath`.

##### A successful run names the backend

**Test**: `SummaryWriter_WriteAsync_SuccessfulRun_NamesSelectedBackend`

Proves a successful run names the backend that produced it, supporting reproduction and correlation.
Evidence for `DocDownCore-Output-SummaryWriter-BackendNamed`.

##### The environment is reported

**Test**: `SummaryWriter_WriteAsync_WithEnvironmentFacts_ReportsRuntimeAndFacts`

Proves the summary reports the runtime and recorded environment facts, letting an auditor reproduce the
conditions and explaining environment-dependent gaps. Evidence for
`DocDownCore-Output-SummaryWriter-EnvironmentReported`.

##### Synthesized gaps are enumerated

**Test**: `SummaryWriter_WriteAsync_UnexplainedAbsence_EnumeratesSynthesizedGap`

Proves every gap, including a gap Core synthesized for an unexplained absence, is enumerated, so no
absence is hidden from the operator. Evidence for `DocDownCore-Output-SummaryWriter-GapsEnumerated`.

##### A failure is reproduced verbatim

**Test**: `SummaryWriter_WriteAsync_FailedRun_ReproducesFailureExplanationVerbatim`

Proves a failed run reproduces the failure explanation verbatim, preserving the diagnostic detail and
remedy. Evidence for `DocDownCore-Output-SummaryWriter-FailureExplained`.

##### Summary text is deterministic

**Test**: `SummaryWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalSummary`

Proves the same content produces byte-identical summary text with fixed line endings, so consumers can
diff runs cleanly. Evidence for `DocDownCore-Output-SummaryWriter-DeterministicText`.

##### Environment facts are attributed to their contributing component

**Test**: `SummaryWriter_Golden_RenderedPagesNoImages_MatchesCommittedGolden`

Proves the Environment block groups contributed facts under the component that reported them (here
`DocDown.Pdf` and `DocDown.Pdf.Rendering`), so a component's honest statement — such as a capability it
does not offer — does not read as a whole-run failure. Evidence for
`DocDownCore-Output-SummaryWriter-EnvironmentAttributed`.

##### Rendered output matches committed golden files

**Tests**:

- `SummaryWriter_Golden_RenderedPagesNoImages_MatchesCommittedGolden`
- `SummaryWriter_Golden_EmbeddedImages_MatchesCommittedGolden`
- `SummaryWriter_Golden_DegradedWithGap_MatchesCommittedGolden`
- `SummaryWriter_Golden_RenderingNotRegisteredDd0301_MatchesCommittedGolden`

These golden-file tests, in `SummaryWriterGoldenTests.cs`, render real `summary.txt` output for four
representative reconciled ledgers and byte-compare it against committed, human-readable golden files
under `test/DemaConsulting.DocDown.Core.Tests/golden/`. They exist to close a review-process gap:
earlier review rounds inspected code, requirements, design, verification docs and tests but never the
generated artifact a reader consumes, so a Layout/Completeness vocabulary collision and an
unattributed-environment-fact defect survived. Committing the golden files makes the rendered output
itself a reviewable artifact.

**Scope and honesty.** Each scenario constructs its ledger by driving the real `ExtractionSink`,
reconciling with `ManifestWriter`, and rendering with `SummaryWriter`, under a fixed
`TimestampUtc` and a fixed `ExtractionEnvironment`. The golden files therefore evidence **the summary
renderer's output for a given reconciled ledger** — they do **not** prove that a real end-to-end PDF
extraction, or real page rendering, produces that ledger. End-to-end behavior (including the native
rendering stack) remains the responsibility of the platform-conditional `DocDown.Pdf` and
`DocDown.Pdf.Rendering` tests. Because the golden files are Core-level and deterministic, they run on
every platform with no native dependency and no skip.

**Determinism and normalization.** The timestamp, the environment strings and the relative paths are
fixed by construction, and `SummaryWriter` already emits `\n` endings, no BOM and invariant-culture
formatting. The only machine-specific lines are the absolute `Scratch folder` path and the temporary
`Source document` path; both are replaced with a fixed placeholder token before comparison, and every
other line is byte-compared against the golden.

Evidence for `DocDownCore-Output-SummaryWriter-GoldenOutput` (and, for the first test, also
`DocDownCore-Output-SummaryWriter-EnvironmentAttributed`).
