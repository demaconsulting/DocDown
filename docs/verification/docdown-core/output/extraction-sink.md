### ExtractionSink Verification Design

This document describes the unit-level verification strategy for `ExtractionSink`, the concrete write
surface a backend uses. It allocates dense, stable relative paths for images, pages, and parts,
deduplicates images, and records the backend's reports and gaps for later serialization.

#### Verification Approach

`ExtractionSink` is verified in isolation through unit tests in `ExtractionSinkTests.cs` under the
`Output` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ExtractionSink_`.

Nothing is mocked; the sink's value is exactly what it allocates and writes to disk, so tests drive the
**real** sink over a real `ScratchFolder` — its documented path-safety dependency — prepared over a
per-test `TempScratch` folder. The test itself plays the backend's role, calling `AddImageAsync`,
`AddPageAsync`, `AddContentPartAsync`, and the `Report*` methods with the byte streams and hints each
scenario needs, then inspecting the returned relative paths and the files on disk. This is the correct
boundary because the sink is not itself an injected seam — it is the surface a backend writes through —
so exercising the real allocation, deduplication, and recording logic against the real containment gate
is what proves the unit.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder backs the real `ScratchFolder`
- **Inputs**: byte streams, image hints, parts, and reports supplied by the test in the backend's place
- **Mocking**: none; the real sink and scratch folder are exercised
- **Isolation**: each test owns its scratch folder and cleans it on dispose

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExtractionSink` unit test run passes when images receive dense, ordinal-prefixed
paths with an extension matching their bytes; when identical image bytes are written once and repeats
return the existing path; when a page file is named by its document page number and a non-positive page
number is rejected; when content parts receive dense ordinal-kind-slug paths regardless of advisory
ordinals; when images are suppressed and the suppression recorded once when images are disabled; when the
backend's reports are recorded faithfully; when reported gaps receive dense ordered identifiers with a
blank reason substituted; and when each image's recorded transform is the one the extractor reported,
defaulted to `Passthrough` only when it reported none. Any duplicated image, wrong page name, sparse
ordinal, lost report, or image whose recorded provenance differs from the one the extractor claimed is
a failure.

#### Test Scenarios

##### Image paths are dense and ordinal-prefixed

**Tests**: `ExtractionSink_AddImageAsync_ThreeDistinctImages_AllocatesDenseOrdinalPaths`,
`ExtractionSink_AddImageAsync_JpegMediaType_UsesJpgExtension`,
`ExtractionSink_AddImageAsync_AllocatedName_BeginsWithOrdinalPrefix`

Proves distinct images receive dense, ordinal-prefixed paths with an extension derived from the written
bytes, yielding stable links independent of backend naming. Evidence for
`DocDownCore-Output-ExtractionSink-AllocatesImagePaths`.

##### Identical image bytes are deduplicated

**Test**: `ExtractionSink_AddImageAsync_IdenticalBytesTwice_DeduplicatesToOneFile`

Proves identical image bytes are written once and a repeat returns the existing path, keeping output
compact. Evidence for `DocDownCore-Output-ExtractionSink-ImageDeduplication`.

##### Pages are named by document page number

**Test**: `ExtractionSink_AddPageAsync_PageNumber_NamesFileByDocumentPage`

Proves a rendered page file is named by its document page number, making the page correspondence explicit.
Evidence for `DocDownCore-Output-ExtractionSink-AllocatesPagePaths`.

##### Part paths are dense ordinal-kind-slug regardless of advisory ordinals

**Tests**: `ExtractionSink_AddContentPartAsync_TitledPart_AllocatesOrdinalKindSlugPath`,
`ExtractionSink_AddContentPartAsync_UntitledPart_AllocatesOrdinalKindPath`,
`ExtractionSink_AddContentPartAsync_ConflictingAdvisoryOrdinals_AllocatesDenseOrdinals`

Proves content parts receive dense ordinal-kind-slug paths in call order, gap-free even when the backend
supplies conflicting or sparse advisory ordinals. Evidence for
`DocDownCore-Output-ExtractionSink-AllocatesPartPaths`.

##### Disabled images are suppressed and recorded once

**Test**: `ExtractionSink_AddImageAsync_ImagesDisabled_SuppressesAndRecordsOnce`

Proves that when images are disabled, writes are suppressed and the fact is recorded once, keeping output
honest without flooding the diagnostics. Evidence for `DocDownCore-Output-ExtractionSink-ImageSuppression`.

##### Backend reports are recorded

**Test**: `ExtractionSink_Reports_RecordedForLaterSerialization`

Proves document info, diagnostics, environment facts, and found counts are recorded faithfully for the
summary, manifest, and ledger to consume later. Evidence for
`DocDownCore-Output-ExtractionSink-RecordsReports`.

##### Gaps receive dense identifiers and a substituted reason

**Tests**: `ExtractionSink_ReportGap_MultipleGaps_AssignsDenseIdentifiersOverwritingCallerIds`,
`ExtractionSink_ReportGap_BlankReason_SubstitutesReasonAndRecordsDiagnostic`

Proves reported gaps receive dense, ordered identifiers overwriting caller-supplied ids, and a blank
reason is replaced with a substitute and a diagnostic, so every gap has a stable identity and a meaningful
explanation. Evidence for `DocDownCore-Output-ExtractionSink-AllocatesGapIdentifiers`.

##### Image provenance is recorded from the extractor's report, in both directions

**Tests**: `ExtractionSink_AddImageAsync_NoTransformHint_RecordsPassthrough`,
`ExtractionSink_AddImageAsync_DecodedToPngHint_RecordsDecodedToPng`,
`ExtractionSink_AddImageAsync_DuplicateBytes_KeepsFirstRecordedTransform`

Proves both directions of the provenance claim, which is why there are two positive scenarios rather than
one: a hint carrying no transform is recorded as `Passthrough`, and a hint claiming a decode-and-re-encode
is recorded as `DecodedToPng` and not silently flattened to the default. A test of the default alone would
be satisfied by a sink that hard-codes one value, so it would prove nothing about honesty. The third test
fixes deduplication precedence as defined behavior: a byte-identical repeat keeps the first record's
transform, because identical bytes cannot honestly carry two provenances. Evidence for
`DocDownCore-Output-ExtractionSink-ImageTransformRecorded`.

##### An image description is recorded from the hint, or reported absent

**Test**: `ManifestWriter_Write_ImageWithDescription_SerializesDescriptionAndSource`

Exercises the sink through the manifest: an image hint carrying a description and its source is recorded on
the sink and reaches the manifest verbatim, while a hint with none leaves both absent. The sink takes the
description from the extractor's report rather than guessing, so an unknown description is reported as
absent and never invented. Evidence for `DocDownCore-Output-ExtractionSink-ImageDescriptionRecorded`.

##### Invalid counts are rejected

**Tests**: `ExtractionSink_AddPageAsync_ZeroPageNumber_ThrowsArgumentOutOfRange`,
`ExtractionSink_ReportFound_NegativeCount_ThrowsArgumentOutOfRange`

Proves a zero page number and a negative found count are rejected as out-of-range, catching invalid
backend input at the surface. These are defensive tests with no linked requirement.
