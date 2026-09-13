### ManifestWriter Verification Design

This document describes the unit-level verification strategy for `ManifestWriter`.

#### Verification Approach

`ManifestWriter` is verified in `ManifestWriterTests.cs` by driving a real `ExtractionSink` over a
real `ScratchFolder`, then parsing the written `manifest.json` back. The writer's behavior is the
exact schema it emits, so the real file is the verification target.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Filesystem**: a real per-test `TempScratch` folder and `ScratchFolder`
- **Mocking**: N/A - the real writer and real sink are exercised

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `ManifestWriter` passes when it declares schema version 2.0, records the tool,
selected backend, scratch path, and status, emits notes in order, records the reduced requested
options and reduced extractor shape, omits the removed top-level keys, writes deterministic no-BOM
JSON, projects image transforms to the manifest vocabulary, and records per-image referrers,
template flags, and descriptions.

#### Test Scenarios

##### The schema version is declared as 2.0

**Test**: `ManifestWriter_WriteAsync_AnyRun_DeclaresSchemaVersionTwoPointZero`

##### A produced run records tool, backend, scratch folder, and status

**Test**: `ManifestWriter_WriteAsync_ProducedRun_RecordsToolBackendScratchAndStatus`

##### An unreadable run records failure and unreadable status

**Test**: `ManifestWriter_WriteAsync_UnreadableRun_RecordsFailureAndUnreadableStatus`

##### Notes are serialized in emission order

**Test**: `ManifestWriter_WriteAsync_NotesReported_SerializesMessagesInOrder`

##### Requested options use the reduced field set

**Test**: `ManifestWriter_WriteAsync_RequestedOptions_ContainsReducedFieldSet`

##### Removed top-level keys stay absent

**Test**: `ManifestWriter_WriteAsync_ProducedRun_OmitsRemovedTopLevelKeys`

##### The selected extractor uses the reduced shape

**Test**: `ManifestWriter_WriteAsync_SelectedExtractor_SerializesReducedExtractorShape`

##### Serialization is deterministic and writes no BOM

**Test**: `ManifestWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalNoBomJson`

##### Image transforms are projected to camelCase strings

**Test**: `ManifestWriter_WriteAsync_DecodedToPngImage_SerializesCamelCaseTransform`

##### Image metadata carries referrers, template flags, and descriptions

**Test**: `ManifestWriter_WriteAsync_ImageMetadata_SerializesReferrersTemplateAndDescription`
