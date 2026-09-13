## Output Subsystem Verification Design

This document describes the verification strategy for the Output subsystem, the sole write path for an
extraction: it prepares the scratch folder, contains every path, writes the content, images, pages and
parts, serializes `summary.txt`, `manifest.json`, and `metadata.json`, and verifies a finished folder
against its own manifest.

### Verification Approach

Output is verified through subsystem integration tests that exercise its six units — `ScratchFolder`,
`ExtractionSink`, `ContentWriter`, `SummaryWriter`, `ManifestWriter`, and `ContractVerifier` — as one
write path over a real temporary folder. Tests reside in `OutputTests.cs` under the `Output` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `Output_`.

There is no mocking of the write path itself; that is deliberate, because the subsystem's whole value is
what it actually writes to disk. Tests prepare a real `ScratchFolder` over a `TempScratch` temporary
folder, drive the real `ExtractionSink` and writers to produce a genuine layout, and then reconcile the
result against the filesystem with the real `ContractVerifier`. The backend that would normally feed the
sink is simulated only in the sense that the test itself plays that role — writing content, images, and
parts directly through the sink — because the subsystem boundary is the sink surface, not the extractor.
Path safety, the adversarial input classes, and the honesty reconciliation are exercised in depth at the
unit level in *ScratchFolder Verification Design* and *ContractVerifier Verification Design*.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds the real layout the writers produce
- **Determinism**: a fixed `ExtractionOptions.TimestampUtc`, so byte-identity comparisons are stable
  within an environment
- **Mocking**: none of the write path; the test drives the real sink in the backend's place
- **Isolation**: each test owns its scratch folder and cleans it on dispose

### Acceptance Criteria

Per IEC 62304 §5.5.2, an Output subsystem test run passes when the fixed layout with `summary.txt` and
`manifest.json` is established for a run; when the summary carries every mandatory section and the
manifest declares its schema version; when content, deduplicated images, and page-numbered page files
are written to their stable relative paths; when the completeness ledger is reconciled against disk and
every unexplained absence is given a synthesized gap; when a hostile artifact name is contained; when
identical input at a fixed timestamp yields byte-identical output; and when the verifier reports an
honest folder as clean and a tampered one as violated; and when two images added in one run with
different reported transforms are each recorded with their own provenance rather than one blanket
label. Any layout hole, unexplained absence, escaped path, non-deterministic byte, or flattened
provenance claim is a failure.

### Test Scenarios

#### A successful write creates the summary and manifest

**Test**: `Output_ArtifactLayout_SuccessfulWrite_CreatesSummaryAndManifest`

Proves the subsystem prepares the scratch folder and establishes the fixed layout, with `summary.txt`
and `manifest.json` present. Evidence for `DocDownCore-Output-ArtifactLayout`.

#### The summary contains every mandatory section

**Test**: `Output_SummaryContent_Written_ContainsMandatorySections`

Proves the human-readable summary always carries the same mandatory sections, so an operator finds the
same information in the same place. Evidence for `DocDownCore-Output-SummaryContent`.

#### The manifest parses with a schema version

**Test**: `Output_ManifestContent_Written_ParsesWithSchemaVersion`

Proves the machine-readable manifest declares its schema version, letting tooling validate
compatibility before acting on it. Evidence for `DocDownCore-Output-ManifestContent`.

#### The document's self-reported metadata is written to metadata.json

**Tests**: `MetadataWriter_WriteAsync_ReportedMetadata_WritesFile`,
`MetadataWriter_WriteAsync_NoReportedMetadata_WritesSparseFile`,
`MetadataWriter_Render_PopulatedFields_EmitsValueAndSourceObjects`,
`MetadataWriter_Render_AbsentInteresting_EmitsAbsentArrayAndNote`,
`OpcMetadataMapper_From_PopulatedProperties_MapsFieldsWithProvenanceAndIsoDates`,
`OpcMetadataMapper_From_BlankValues_OmittedAsFieldsAndRecordedAbsent`

Proves `metadata.json` records what the document asserts about itself with per-field provenance,
omits blank values while naming the interesting fields the document left blank, and is always written
— rendering a sparse form with an explaining note rather than an empty object when the backend read
nothing. The shared OPC mapper applies omit-empty, the interesting-absence list, provenance, and
ISO-8601 date normalization once for every OPC backend. Evidence for
`DocDownCore-Output-DocumentMetadata`.

#### The content outline names the structure of content.md

**Tests**: `ExtractionSink_ReportContentFeature_RepeatedLabels_AccumulateAndDropZeroCounts`,
`ExtractionSink_ReportContentFeature_LookedForZero_IsReported`,
`SummaryWriter_WriteAsync_WithContentFeatures_OpensWithGistAndOutlinesContent`,
`SummaryWriter_WriteAsync_LookedForZeroFeature_OutlinesTheZero`

Proves the subsystem accumulates the counted structural features a backend reports — folding repeated
labels together, dropping a zero count for a feature the backend did not declare it looked for, and
keeping the zero for one it did — and renders the surviving features as the summary's one-line content
outline (the machine-readable twin is the manifest's `contentFeatures` array), so a reader learns that
(for example) author-attributed comments are present without parsing the markdown, and can tell a
looked-for feature the document does not carry from one that was never counted. Evidence for
`DocDownCore-Output-ContentOutline`.

#### Text content produces a Markdown document

**Test**: `Output_ContentDocument_TextWritten_ProducesContentMd`

Proves extracted text is written as `content.md`, giving consumers one predictable representation.
Evidence for `DocDownCore-Output-ContentDocument`.

#### Duplicate image bytes are deduplicated

**Test**: `Output_ImageResources_DuplicateBytes_AreDeduplicated`

Proves identical image bytes are written once and shared, keeping output compact while every reference
still resolves. Evidence for `DocDownCore-Output-ImageResources`.

#### A rendered page is named by its page number

**Test**: `Output_PageResources_PageWritten_IsNamedByPageNumber`

Proves each rendered page file is named by its document page number, making the page correspondence
unambiguous. Evidence for `DocDownCore-Output-PageResources`.

#### The completeness ledger marks content present

**Test**: `Output_CompletenessLedger_TextWritten_MarksContentPresent`

Proves the completeness ledger is reconciled against disk and marks content present when it was written.
Evidence for `DocDownCore-Output-CompletenessLedger`.

#### A missing artifact is explained by a synthesized gap

**Test**: `Output_GapExplanation_MissingContent_SynthesizesExplainedGap`

Proves the subsystem records an explaining gap for a requested output it could not deliver, so no
absence is silent. Evidence for `DocDownCore-Output-GapExplanation`.

#### A malicious image name is contained

**Test**: `Output_PathSafety_MaliciousImageName_IsContained`

Proves a hostile artifact name is contained within the scratch folder rather than escaping it. The full
adversarial catalogue is proven in *ScratchFolder Verification Design*. Evidence for
`DocDownCore-Output-PathSafety`.

#### A fixed timestamp produces byte-identical output

**Test**: `Output_Determinism_FixedTimestamp_ProducesByteIdenticalOutput`

Proves that naming, summary text, and manifest JSON depend only on the input and the fixed timestamp, so
two runs are byte-identical within an environment. Evidence for `DocDownCore-Output-Determinism`.

#### The verifier passes an honest folder and detects an unlisted file

**Tests**: `Output_ContractVerification_HonestExtraction_ReportsNoViolations`,
`Output_ContractVerification_UnlistedFile_IsDetected`

Proves the subsystem verifies a finished extraction against its contract, reporting no violations for an
honest folder and flagging a file the manifest does not account for. The reconciliation runs in both
directions and its filesystem walk is exhaustive over the whole scratch tree, so a stray file at the
root or nested below a resource folder is caught as readily as one placed directly in `images/`; the
divergence-by-divergence proof of that lives in *ContractVerifier Verification Design*. Evidence for
`DocDownCore-Output-ContractVerification`.

#### Mixed image provenance is recorded per image

**Test**: `Output_ImageProvenance_MixedTransforms_ManifestRecordsEachHonestly`

Proves the subsystem records a provenance claim for each image rather than one claim for the run: a
passthrough image and a decoded-and-re-encoded image added in the same extraction appear in the manifest
with their own distinct transforms, in allocation order, and the folder still verifies clean. Evidence for
`DocDownCore-Output-ImageProvenance`.
