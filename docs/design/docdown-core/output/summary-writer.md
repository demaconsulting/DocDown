### SummaryWriter

![Output Structure](OutputView.svg)

#### Purpose

`SummaryWriter` serializes `summary.txt`, the human- and LLM-readable account of what was extracted,
where it was written, and what was not extracted and why. Its single responsibility is to render the
in-memory extraction report as the fixed-shape plain-text document that is the human twin of
`manifest.json`.

#### Data Model

`SummaryWriter` is a `static class` with no state. It reads an `ExtractionReport` (outcome, source,
detection, selection, environment, options, timestamp, and any failure), the `ArtifactLedger`, the
recorded images, gaps, and diagnostics, and composes them into a `StringBuilder` written through the
scratch-folder gate.

#### On-disk format

`summary.txt` is plain UTF-8 with **no BOM** and `\n` line endings **always**, so it is byte-identical
across platforms for the same content. Lines are kept to at most 120 columns; wrapped values are
indented under their label. All formatting uses `CultureInfo.InvariantCulture`. The sections appear
**always, in this fixed order** (a section with nothing to report says so explicitly rather than being
omitted):

1. `DocDown Extraction Summary` and the `==========================` underline, then a blank line.
2. **Header block** — `Scratch folder` (the absolute path), `Source document`, `Detected format`,
   `Extracted (UTC)`, `Status`.
3. The literal line `All paths below are relative to the scratch folder shown above.`
4. **Failure** — present **only** when `Outcome == Failed`; it contains `Failure.Explanation`
   **verbatim**.
5. **Backend** — the selected backend, its fidelity, and why it was chosen.
6. **Environment** — operating system, runtime, and the environment facts. Contributed facts are
   **grouped under the component that reported them** (for example `DocDown.Pdf` and
   `DocDown.Pdf.Rendering`), preserving first-seen source order and per-source emission order, each
   with an availability suffix. Grouping keeps a component's honest statement — such as a capability
   it does not offer — from reading as a whole-run failure, and scales to the multi-backend case.
7. **Layout** — one line per core artifact and its on-disk **presence** (`PRESENT` / `not present` /
   `ABSENT`).
8. **What WAS extracted** — the content, images, and pages actually produced.
9. **What was NOT extracted** — one indented block per gap, numbered `[GAP-n]`, with its reason,
   impact, and remedy.
10. **Completeness** — the per-artifact **completeness** status, ending — when the ledger
    reconciliation succeeded — with the literal line `Every absence above is explained by a numbered
    gap. There are no unexplained gaps.` Completeness uses a vocabulary (`COMPLETE` / `PARTIAL` /
    `MISSING`) deliberately **disjoint** from the Layout section's presence vocabulary, so no single
    word carries two senses. In particular an empty-but-expected folder reads `not present` in Layout
    (on-disk truth: the folder is not created when empty) and `COMPLETE (0 of 0)` in Completeness
    (nothing is missing) without the two lines contradicting each other.
11. **Diagnostics** — the coded diagnostics, with a count of warnings and errors.

#### Key Methods

- **`static ValueTask WriteAsync(...)`** (the public entry point) builds the document by appending each
  section in order and writes it through `ScratchFolder.WriteTextAsync`.

Private appenders implement the sections one-to-one: `AppendTitleAndHeader`, `AppendFailure`,
`AppendBackend`, `AppendEnvironment`, `AppendLayout`, `AppendWhatWasExtracted`,
`AppendWhatWasNotExtracted`, `AppendCompleteness`, and `AppendDiagnostics`. `AppendEnvironment`
delegates contributed facts to `AppendEnvironmentFactGroups`, which buckets facts by their `Source`
component while preserving first-seen source order and per-source emission order. Shared helpers wrap
long values under a label (`AppendWrapped`), and map statuses and scopes to their human phrases —
`FolderLayoutPhrase`/`ContentLayoutPhrase` render the **presence** vocabulary for Layout, and
`FolderCompletenessPhrase`/`ContentCompletenessPhrase` render the disjoint **completeness** vocabulary
for Completeness. The closing completeness line is not decoration: it is the human-readable form of
the ledger reconciliation the manifest writer performs, and it is the property the reconciliation
tests assert.

#### Golden verification

Because the rendered `summary.txt` is the artifact a reader trusts, its exact wording is locked in by
committed, human-readable golden files under `test/DemaConsulting.DocDown.Core.Tests/golden/` (one per
representative scenario: rendered pages with no images, embedded images, a degraded run with a genuine
gap, and page rendering requested but not registered). The golden tests drive the real
sink/reconcile/render chain with a fixed timestamp and a fixed environment, then byte-compare the
output after normalizing only the two machine-specific lines (the absolute scratch-folder path and the
temporary source-document path). They therefore evidence **the renderer's output for a given
reconciled ledger** — not that a real end-to-end PDF or page-rendering extraction produces that ledger,
which remains the responsibility of the `DocDown.Pdf`/`DocDown.Pdf.Rendering` tests.

#### Error Handling

The writer performs no validation of its own beyond what the report already guarantees; it renders
whatever the reconciled report contains. The single write goes through `ScratchFolder.WriteTextAsync`,
so a path fault surfaces as `ScratchFolderException` and the deterministic `\n`/no-BOM encoding is
applied. There is no partial-write recovery — the summary is composed fully in memory and written once.

#### Dependencies

- **ScratchFolder** — the text write gate. See _ScratchFolder Design_.
- **ArtifactLedger**, **ExtractionGap**, **ExtractionDiagnostic**, **ExtractionEnvironment**,
  **DocumentSource**, **ExtractionOutcome** (supporting types).

#### Callers

`DocDownEngine` calls `SummaryWriter` last in the serialization step, after `ContentWriter` and
`ManifestWriter`, so the summary reflects the reconciled ledger and gaps. See _DocDownEngine Design_.
