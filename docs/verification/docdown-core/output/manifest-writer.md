### ManifestWriter Verification Design

This document describes the unit-level verification strategy for `ManifestWriter`, which produces the
machine-readable `manifest.json`, reconciles the completeness ledger against disk, synthesizes gaps for
unexplained absences, and serializes deterministically.

#### Verification Approach

`ManifestWriter` is verified in isolation through unit tests in `ManifestWriterTests.cs` under the
`Output` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ManifestWriter_`.

Nothing is mocked. The reconciliation step compares the ledger against the actual bytes on disk, so tests
drive a **real** `ExtractionSink` over a real `ScratchFolder` prepared on a per-test `TempScratch` folder,
let it write genuine content, then run the writer's `Reconcile` and `WriteAsync` and parse the produced
`manifest.json` back. A real folder is essential precisely because the load-bearing behavior — reconciling
each ledger slot against the filesystem and synthesizing a gap for any unexplained absence — cannot be
evidenced without real files to reconcile against. The JSON is parsed and, for determinism, compared byte
for byte, so the source-generated serializer is exercised as it runs in production.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder backs the real `ScratchFolder` and `ExtractionSink`
- **Mocking**: none; the real sink writes real content that the writer reconciles
- **Isolation**: each test owns its scratch folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `ManifestWriter` unit test run passes when the manifest declares its schema
version; when it records the producing tool and selected backend; when the completeness ledger is
reconciled against disk and every ledger slot is present; when an unexplained absence is given a
synthesized gap with a diagnostic that then appears in the manifest with its reason; when the complete
flag equals whether any gaps remain; when the same content serializes to byte-identical, BOM-free
JSON; and when **every** member of the `ImageTransform` vocabulary projects to a distinct, non-empty
camelCase manifest string. Any missing slot, unexplained absence, wrong complete flag,
non-deterministic byte, or transform member that fails to project is a failure.

#### Test Scenarios

##### The schema version is declared

**Test**: `ManifestWriter_WriteAsync_AnyRun_DeclaresSchemaVersionOnePointZero`

Proves the manifest declares its schema version, letting tooling validate compatibility before acting on
the contents. Evidence for `DocDownCore-Output-ManifestWriter-SchemaVersion`.

##### The tool and backend are recorded

**Test**: `ManifestWriter_WriteAsync_SuccessfulRun_RecordsToolAndBackend`

Proves the manifest records the producing tool and selected backend, supporting programmatic attribution.
Evidence for `DocDownCore-Output-ManifestWriter-MachineTwin`.

##### The completeness ledger is reconciled and complete

**Tests**: `ManifestWriter_Reconcile_TextWritten_MarksLedgerSlotsAndContentPresent`,
`ManifestWriter_WriteAsync_LedgerBlock_ContainsEverySlot`

Proves the ledger is reconciled against disk — marking content present when it was written — and that the
serialized ledger block contains every slot. Evidence for
`DocDownCore-Output-ManifestWriter-CompletenessLedger`.

##### An unexplained absence is synthesized into a gap

**Tests**: `ManifestWriter_Reconcile_UnexplainedAbsence_SynthesizesGapWithDiagnostic`,
`ManifestWriter_WriteAsync_SynthesizedGap_AppearsInManifestWithReason`

Proves a partial or absent artifact with no explanation is given a synthesized gap and a diagnostic, which
then appears in the manifest with its reason, enforcing that every absence is accounted for. Evidence for
`DocDownCore-Output-ManifestWriter-GapSynthesis`.

##### The complete flag reflects the gaps

**Test**: `ManifestWriter_WriteAsync_CompleteFlag_EqualsWhetherGapsAreEmpty`

Proves the complete flag is derived strictly from whether any gaps remain, giving consumers one honest
completeness signal. Evidence for `DocDownCore-Output-ManifestWriter-CompleteFlag`.

##### JSON serialization is deterministic

**Test**: `ManifestWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalNoBomJson`

Proves the same content serializes to byte-identical JSON without a byte-order mark, so consumers detect
real changes rather than encoding noise. Evidence for `DocDownCore-Output-ManifestWriter-DeterministicJson`.

##### Every image transform projects to a distinct camelCase string

**Tests**: `ManifestWriter_Write_DecodedToPngImage_SerializesCamelCaseTransform`,
`ManifestWriter_TransformString_EveryImageTransformValue_Projects`

Proves the symmetry between what the manifest documents and what the library can produce, from both
sides. The first test shows `decodedToPng` — a value the schema documented long before any code path
could emit it — now reaches `manifest.json` end to end. The second is the standing gate: it enumerates
every declared `ImageTransform` member, drives each through the real writer, and asserts each yields a
non-empty, distinct, camelCase string. "Producible implies documented" is already structural, since the
enumeration is closed and the compiler forbids any other value; this scenario closes the converse, so a
future member added without a projection fails here rather than at a consumer's manifest. Evidence for
`DocDownCore-Output-ManifestWriter-ImageTransformProjected`.

##### A null sink is rejected

**Test**: `ManifestWriter_Reconcile_NullSink_ThrowsArgumentNullException`

Proves reconciliation rejects a null sink at entry with the documented exception. This is a defensive test
with no linked requirement.

##### An image description and its provenance are serialized honestly

**Tests**: `ManifestWriter_Write_ImageWithDescription_SerializesDescriptionAndSource`,
`ManifestWriter_Write_ImageWithoutDescription_SerializesNullDescription`

Proves both directions of the image-description record. The first shows an authored description and its
`descriptionSource` provenance reaching `manifest.json` verbatim, so a reader can tell an authored
description apart from a contextual hint. The second shows that an image given no description serializes
an explicit `null` for both fields rather than a fabricated one — the manifest states the absence rather
than inventing a description. Evidence for `DocDownCore-Output-ManifestWriter-ImageDescriptionSerialized`.
