## Output Subsystem Verification Design

This document describes the verification strategy for the Output subsystem.

### Verification Approach

Output is verified through subsystem tests in `OutputTests.cs`. The tests drive the real
`ScratchFolder`, `ExtractionSink`, `ContentWriter`, `MetadataWriter`, `ManifestWriter`, and
`SummaryWriter` over a real `TempScratch` folder. No mocking is used because the subsystem's value is
what it actually writes to disk.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder receives the real output
- **Inputs**: the test itself acts as the backend by writing through `IExtractionSink`

### Acceptance Criteria

Per IEC 62304 §5.5.2, Output passes when it prepares the scratch folder and writes the root
artifacts, emits the reduced schema `manifest.json`, writes `content.md` when text exists,
deduplicates identical images, names rendered pages by source page number, carries notes into both
summary and manifest, contains hostile path input, produces deterministic bytes at a fixed timestamp,
and records image provenance per image.

### Test Scenarios

#### A successful write creates the root artifacts

**Test**: `Output_ArtifactLayout_SuccessfulWrite_CreatesRootArtifacts`

#### The summary contains the mandatory sections

**Test**: `Output_SummaryContent_Written_ContainsMandatorySections`

#### The manifest parses as schema version 2.0

**Test**: `Output_ManifestContent_Written_ParsesWithSchemaVersionTwoPointZero`

#### Written text produces content.md

**Test**: `Output_ContentDocument_TextWritten_ProducesContentMd`

#### Duplicate image bytes are stored once

**Test**: `Output_ImageResources_DuplicateBytes_AreDeduplicated`

#### Rendered pages are named by page number

**Test**: `Output_PageResources_PageWritten_IsNamedByPageNumber`

#### Notes appear in both summary and manifest

**Test**: `Output_Notes_Reported_AppearInSummaryAndManifest`

#### A malicious image name stays contained

**Test**: `Output_PathSafety_MaliciousImageName_IsContained`

#### Fixed-timestamp runs are byte-identical

**Test**: `Output_Determinism_FixedTimestamp_ProducesByteIdenticalOutput`

#### Image provenance is recorded per image

**Test**: `Output_ImageProvenance_MixedTransforms_ManifestRecordsEachHonestly`

#### Image referrers and the template flag are recorded per image

**Test**: `Output_ImageReferrers_PagesAndTemplate_ManifestRecordsAliasAndFlag`

Proves the pipeline writes every referring page into `sourcePages`, aliases its first entry as
`sourcePage`, and marks a template-only image with `referencedByTemplate`.
