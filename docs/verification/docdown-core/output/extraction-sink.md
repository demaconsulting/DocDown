### ExtractionSink Verification Design

This document describes the unit-level verification strategy for `ExtractionSink`.

#### Verification Approach

`ExtractionSink` is verified in `ExtractionSinkTests.cs` over a real `ScratchFolder` and a real
`TempScratch` folder. The tests themselves play the backend role, which makes allocation,
deduplication, suppression, and reporting behavior observable without mocking the sink.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Filesystem**: a real per-test `TempScratch` folder
- **Inputs**: byte streams, parts, notes, metadata, environment facts, and content features

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `ExtractionSink` passes when it allocates dense relative paths for images and
parts, deduplicates identical image bytes, merges duplicate image referrers, names rendered page
files by source page number, suppresses disabled images without writing them, records notes,
metadata, environment facts, and looked-for zero-count features, and keeps hostile preferred names
contained within the scratch folder.

#### Test Scenarios

##### Image paths are dense and use the correct extension

**Tests**: `ExtractionSink_AddImageAsync_ThreeDistinctImages_AllocatesDenseOrdinalPaths`,
`ExtractionSink_AddImageAsync_JpegMediaType_UsesJpgExtension`,
`ExtractionSink_AddImageAsync_VectorMediaType_UsesTrueExtension`

##### Identical image bytes are deduplicated and merge referrers

**Tests**: `ExtractionSink_AddImageAsync_IdenticalBytesTwice_DeduplicatesToOneFile`,
`ExtractionSink_AddImageAsync_DuplicateBytes_MergesReferrerSets`

##### Pages are named by source page number

**Test**: `ExtractionSink_AddPageAsync_PageNumber_NamesFileByDocumentPage`

##### Part paths ignore advisory ordinals and stay dense

**Test**: `ExtractionSink_AddContentPartAsync_ConflictingAdvisoryOrdinals_AllocatesDenseOrdinals`

##### Disabled images are suppressed and return no link

**Test**: `ExtractionSink_AddImageAsync_ImagesDisabled_SuppressesWritesAndReturnsEmptyPath`

##### Reports are recorded for later serialization

**Tests**: `ExtractionSink_Reports_RecordedForLaterSerialization`,
`ExtractionSink_ReportContentFeature_LookedForZero_IsReported`

##### Hostile preferred names stay contained

**Test**: `ExtractionSink_AddImageAsync_HostilePreferredName_StaysContained`
