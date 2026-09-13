### ScratchFolder Verification Design

This document describes the unit-level verification strategy for `ScratchFolder`, the library's
path-safety security control. It owns the absolute output directory for one extraction and is the single
gate through which every Core-side path is allocated, validated, and contained.

#### Verification Approach

`ScratchFolder` is verified in isolation through **adversarial** unit tests in `ScratchFolderTests.cs`
under the `Output` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ScratchFolder_`.

Path safety is a security control, not a convenience wrapper, so its verification is written as security
testing: the tests deliberately supply hostile inputs — the kind that originate in untrusted document
content such as image names, part titles, and sheet names — and assert the control refuses or
neutralizes each. Nothing is mocked; the unit's only dependencies are `System.IO`,
`System.Globalization`, and `System.Text`. Filesystem-touching scenarios use a real `TempScratch`
temporary folder so the four preparation modes exercise real directory creation, cleaning, and
refusal; the pure static helpers (`IsReservedDeviceName`, `Slugify`, `ValidateTotalPathLength`) are
tested directly with adversarial strings. A real folder is essential because the catastrophic-data-loss
guard can only be evidenced by proving that unrelated files on disk survive a refusal.

The design records that path safety is **three distinct controls** — containment, reserved-device-name
rejection, and total-path-length bounding — and that no one of them subsumes another; the scenarios are
organized to exercise each control independently so a gap in one cannot be masked by another.

The adversarial scenario classes and the real tests that cover them:

| Adversarial class | Real test |
| --- | --- |
| Traversal sequences (`../`, `..\`, `....//`, nested) | `ScratchFolder_Combine_TraversalOrRootedPath_IsRefused` |
| Absolute and rooted paths (`/etc/passwd`, `\foo`) | `ScratchFolder_Combine_TraversalOrRootedPath_IsRefused` |
| UNC paths (`\\server\share\x`) | `ScratchFolder_Combine_TraversalOrRootedPath_IsRefused` |
| Separator, colon, and NUL injection | `ScratchFolder_Combine_InjectionCharacterInComponent_IsRefused` |
| Reserved device names, bare and disguised | `ScratchFolder_IsReservedDeviceName_ReservedComponent_ReturnsTrue` |
| Ordinary look-alike names not falsely rejected | `ScratchFolder_IsReservedDeviceName_OrdinaryComponent_ReturnsFalse` |
| `{ordinal:D4}-` prefix invariant | `ScratchFolder_IsReservedDeviceName_OrdinalPrefixedName_IsNotReserved` |
| Reserved device name in an allocation | `ScratchFolder_Combine_ReservedDeviceNameComponent_IsRefused` |
| Over-length total path | `ScratchFolder_Combine_OverLongTotalPath_ThrowsScratchFolderNotPathTooLong` |
| Over-length component (slug truncation) | `ScratchFolder_Slugify_OverLongTitle_TruncatesToComponentLimit` |
| Trailing dots and spaces | `ScratchFolder_Slugify_TrailingDotsAndSpaces_AreRemoved` |
| Unicode normalization collisions | `ScratchFolder_Slugify_UnicodeNormalizationForms_ProduceIdenticalSlug` |
| Catastrophic-data-loss guard | `ScratchFolder_Prepare_CleanIfDocDownOnUserFolder_RefusesAndPreservesContents` |
| Copied genuine manifest | `ScratchFolder_Prepare_CopiedGenuineManifestAmongUserFiles_RefusesAndPreservesContents` |
| Hand-authored manifest naming another folder | `ScratchFolder_Prepare_ManifestNamingADifferentFolder_IsRefused` |
| One unaccounted file | `ScratchFolder_Prepare_DocDownFolderWithOneUnlistedFile_RefusesAndPreservesContents` |
| Manifest listing a path outside the folder | `ScratchFolder_Prepare_ManifestListingAnEscapingPath_IsRefused` |
| Changed after scan | `ScratchFolder_Prepare_InventoriedFileChangedBetweenScanAndDelete_RefusesAndPreservesTheChange` |
| Created after scan | `ScratchFolder_Prepare_InventoriedPathCreatedAfterTheScan_RefusesAndPreservesTheNewFile` |
| Rewritten after scan | `ScratchFolder_Prepare_InventoriedFileTouchedBetweenScanAndDelete_RefusesOnTimestampAlone` |

Reserved device names are asserted on **every** platform, including Linux, because output written on
Linux must stay portable to Windows; the tests do not skip the reserved-name checks off Windows. The
over-length total-path scenarios explicitly assert the refusal is a `ScratchFolderException`
(`ScratchFolderRefused`), never an opaque `PathTooLongException`, so the failure is classifiable rather
than surfacing deep in the filesystem. The `{ordinal:D4}-` prefix invariant is asserted **separately**
from the reserved-name check so that a future change to the naming scheme that dropped the prefix would
surface as a failing test rather than silently removing the incidental protection.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder for the preparation-mode and containment scenarios
- **Inputs**: adversarial strings supplied directly to the static helpers
- **Mocking**: none for the path-safety and mode scenarios; no injected dependencies
- **Declared deviation**: the three revalidation scenarios drive an injected `IFileStateReader`
  rather than a second thread. The behavior under test is what happens when a folder changes between
  the inventory scan and the deletion, and no test can reliably win that race — a test that failed to
  win it would report success while proving nothing. The injected reader is therefore used as the
  timing boundary only: it decorates the real file system, every value it returns is the file
  system's own, and the change it makes at that boundary is a genuine write to a genuine file on
  disk. A reader that fabricated a differing value would prove only that the comparison compares
  values, which is not the property at issue. This mirrors the `IDirectoryEnumerator` deviation
  already declared for `ContractVerifier`, and the production path always uses
  `FileSystemFileStateReader`.
- **Platforms**: reserved-name assertions run on Windows, Linux, and macOS alike

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `ScratchFolder` unit test run passes when every traversal, absolute, rooted, UNC,
injection, and reserved-device-name allocation is refused with a `ScratchFolderException`; when a
legitimate relative allocation stays contained; when an over-length total path is refused as a
scratch-folder refusal rather than a `PathTooLongException`; when slugging is deterministic across
Unicode forms and strips unsafe trailing characters and over-length input; when each of the four
preparation modes behaves as specified, including the guard that preserves unrelated user data; when
destructive reuse is refused unless the folder's own manifest is bound to that folder and accounts
for every file present, with each inventoried path resolved through the containment check before any
deletion, each target re-read immediately before its own deletion and refused on any divergence from
the state recorded during the inventory scan, and each refusal naming `ScratchFolderMode.Overwrite`;
and when
every refusal reports a stable, machine-readable reason. Any escaped path, false rejection of a safe
name, un-neutralized hostile input, or destroyed user data is a failure.

#### Test Scenarios

##### RequireEmpty creates an absent folder and refuses a populated one

**Tests**: `ScratchFolder_Prepare_RequireEmptyOnAbsentFolder_CreatesFolder`,
`ScratchFolder_Prepare_RequireEmptyOnPopulatedFolder_RefusesAndPreservesContents`

Proves the strict require-empty policy creates a fresh folder but refuses a non-empty one without deleting
its contents. Evidence for `DocDownCore-Output-ScratchFolder-ModeRequireEmpty`.

##### CleanIfDocDownFolder deletes only what a manifest for that folder accounts for

**Tests**: `ScratchFolder_Prepare_CleanIfDocDownOnUserFolder_RefusesAndPreservesContents`,
`ScratchFolder_Prepare_CleanIfDocDownOnDocDownFolder_CleansContents`,
`ScratchFolder_Prepare_FolderMentioningDocDownInAPlainFile_IsRefused`,
`ScratchFolder_Prepare_UnrelatedFileNamedManifestJson_IsRefused`,
`ScratchFolder_Prepare_TruncatedManifest_IsRefused`,
`ScratchFolder_Prepare_ManifestFromADifferentTool_IsRefused`

Proves the default mode's catastrophic-data-loss guard refuses every folder whose `manifest.json`
does not structurally prove it was written by DocDown. The four adversarial scenarios attack the
guard from each direction a weak fingerprint would fail:

| Attack | Fixture | Expected |
| --- | --- | --- |
| Incidental mention | a user `notes.txt` plus a hand-written `manifest.json` note index | refuse |
| Same name, different content | a `manifest.json` reading `{"tool":"AcmeExporter","docs":"DocDown-like"}` | refuse |
| Corrupt or partial | the first 70 bytes of a genuine DocDown manifest, cut part-way through the tool block | refuse |
| Right shape, wrong identity | a schema-correct manifest whose `tool.name` is `OtherTool` | refuse |

All four fixtures are deliberately chosen so that a substring fingerprint searching for `"tool"` and
`DocDown` would have *accepted* every one of them — the truncated manifest is cut after the tool
block a crashed run would have written, and the note index names another tool while mentioning
DocDown — which is what makes these tests regression evidence for the structural proof rather than a
restatement of the guard.

Every one of the four asserts **both** that the refusal is a `ScratchFolderException` carrying the
`scratchFolderNotDocDown` reason **and** that the planted user file still exists afterwards with its
original contents byte-for-byte. Asserting only the exception would not prove the data survived,
which is the property that actually matters.

`ScratchFolder_Prepare_CleanIfDocDownOnDocDownFolder_CleansContents` is the **happy-path guard, not a
discriminating test**: it passes both before and after the inventory-scoped reuse change, and passes
equally with and without the pre-deletion revalidation, so it is not evidence for either. It is
claimed only as evidence that tightening the guard did not break legitimate reuse. It additionally
asserts that an empty `images/` folder left by a previous run survives, recording the deliberate
decision that empty directories are never deleted because nothing accounts for them. Evidence for
`DocDownCore-Output-ScratchFolder-ModeCleanIfDocDownFolder` and
`DocDownCore-Output-ScratchFolder-InventoryScopedDeletion`.

##### Reuse is refused unless the manifest is bound to this folder

**Tests**: `ScratchFolder_Prepare_CopiedGenuineManifestAmongUserFiles_RefusesAndPreservesContents`,
`ScratchFolder_Prepare_ManifestNamingADifferentFolder_IsRefused`,
`ScratchFolder_Prepare_ManifestPathDifferingOnlyCosmetically_StillCleans`,
`ScratchFolder_Prepare_RefusedReuse_NamesTheOverwriteEscapeHatch`

Proves that satisfying the six structural conditions is not sufficient to authorize deletion. The
first scenario is the defect itself: a genuine `manifest.json` copied out of a real run and dropped
into a folder holding the caller's `thesis.md`. Every structural condition holds, so the folder would
previously have been classified as prior output and emptied; the test asserts the refusal carries
`scratchFolderPathMismatch` and that both the caller's file and the copied manifest are byte-for-byte
unchanged. The second proves the same refusal applies even when the manifest is the only file
present, so nothing is deleted on the way to the refusal.

The third bounds the **false-refusal** surface rather than the false-acceptance surface: a manifest
recording the same folder written with a redundant `.` segment and a trailing separator must still be
accepted, because a guard that refused legitimate re-runs over cosmetic path spelling would be
replaced by `Overwrite` in practice and would protect nobody. It is not a discriminating test — it
passes under the previous behavior too — and is claimed only as a false-alarm bound. The fourth
proves a refusal names `ScratchFolderMode.Overwrite`, so the caller is told how to proceed
deliberately instead of being left at a dead end. Evidence for
`DocDownCore-Output-ScratchFolder-ReuseBoundToFolder`.

##### Deletion is scoped to the inventory and routed through containment

**Tests**: `ScratchFolder_Prepare_DocDownFolderWithOneUnlistedFile_RefusesAndPreservesContents`,
`ScratchFolder_Prepare_ManifestListingAnEscapingPath_IsRefused`

Proves the operation deletes only what the manifest accounts for. The first plants one file of the
caller's own in an otherwise genuine, correctly bound prior run; the guard refuses with
`scratchFolderUnaccountedContent` and the test asserts the refusal is **total** — the caller's file
and all three of the run's own artifacts are still present — so a folder that is partly the caller's
is never partly deleted.

The second hand-authors a correctly bound manifest listing `../../evil.png` as an image and plants
that file two levels above the scratch folder. Because every accounted path is resolved through the
containment check before any deletion happens, the whole operation refuses with
`scratchFolderManifestPathEscapes` and the outside file is byte-for-byte untouched. This is the
evidence that a hand-authored manifest cannot be used to reach a file outside the scratch folder.
Evidence for `DocDownCore-Output-ScratchFolder-InventoryScopedDeletion`.

##### Deletion is revalidated immediately before it happens

**Tests**: `ScratchFolder_Prepare_InventoriedFileChangedBetweenScanAndDelete_RefusesAndPreservesTheChange`,
`ScratchFolder_Prepare_InventoriedPathCreatedAfterTheScan_RefusesAndPreservesTheNewFile`,
`ScratchFolder_Prepare_InventoriedFileTouchedBetweenScanAndDelete_RefusesOnTimestampAlone`

Proves the guard does not act on a stale reading of the folder. Every earlier check reasons about
the folder as it was when it was listed; these three prove that a file which changed after that
listing is refused rather than deleted, with the refusal carrying
`scratchFolderChangedDuringPreparation`.

| Change made between the scan and the deletion | Fixture | Expected |
| --- | --- | --- |
| Content appended, so length and time differ | a real append to `content.md` | refuse, changed bytes kept |
| A file appears where the path held none | a real file created at `images/0001-late.png` | refuse, new file kept |
| Identical bytes rewritten, so only the time differs | a real rewrite with the time advanced | refuse, file kept |

The second is the attack itself, reproduced exactly, and it is also what pins absence-to-presence as
a divergence — a comparison of length and last-write time alone would not see it. The third isolates
the timestamp half of the comparison: with the bytes and the length unchanged, nothing but the time
can reveal the rewrite, so removing that comparison would leave this scenario silently deleted while
the other two still refused. Each test asserts both the refusal reason and that the contended file is
still on disk with the bytes the change wrote, because asserting only the exception would not prove
the concurrent write survived, which is the property that matters.

These scenarios evidence a **narrowed** interval, not a closed one, and the verification claim is
deliberately limited to match. They do not evidence that a change made between the final reading of a
file and the deletion itself is caught — nothing short of operating-system-level locking would achieve
that, and this library does not take such locks — nor that a same-length in-place overwrite finer
than the file system's timestamp resolution is caught. They likewise do not claim the refusal is
atomic: targets already deleted when a mismatch is found are not restored, which is acceptable only
because every one of them is an inventoried DocDown artifact. Evidence for
`DocDownCore-Output-ScratchFolder-RevalidatedBeforeDeletion`.

##### Overwrite clears existing contents

**Test**: `ScratchFolder_Prepare_OverwriteOnPopulatedFolder_ClearsContents`

Proves the caller-opted overwrite policy unconditionally clears the folder, guaranteeing no stale
artifacts survive. Evidence for `DocDownCore-Output-ScratchFolder-ModeOverwrite`.

##### CreateUnique allocates a distinct folder

**Test**: `ScratchFolder_Prepare_CreateUniqueOnExistingFolder_AllocatesDistinctFolder`

Proves the create-unique policy leaves an existing folder untouched and allocates a distinct sibling name,
letting many runs coexist. Evidence for `DocDownCore-Output-ScratchFolder-ModeCreateUnique`.

##### Every allocated path is contained

**Tests**: `ScratchFolder_Combine_TraversalOrRootedPath_IsRefused`,
`ScratchFolder_Combine_LegitimateRelativePath_StaysContained`,
`ScratchFolder_Combine_InjectionCharacterInComponent_IsRefused`

Proves traversal, absolute, rooted, and UNC allocations, and colon or NUL injection, are refused while a
legitimate relative path stays rooted inside the scratch folder. Evidence for
`DocDownCore-Output-ScratchFolder-PathContainment`.

##### Reserved device names are rejected on every platform

**Tests**: `ScratchFolder_IsReservedDeviceName_ReservedComponent_ReturnsTrue`,
`ScratchFolder_IsReservedDeviceName_OrdinaryComponent_ReturnsFalse`,
`ScratchFolder_IsReservedDeviceName_OrdinalPrefixedName_IsNotReserved`,
`ScratchFolder_Combine_ReservedDeviceNameComponent_IsRefused`

Proves reserved device names — with and without extensions and with trailing dots and spaces — are
detected everywhere, that ordinary look-alike names are not, that the `{ordinal:D4}-` prefix defuses a
reserved stem (asserted separately to guard the naming scheme), and that a reserved-name allocation is
refused. Evidence for `DocDownCore-Output-ScratchFolder-RejectsReservedNames`.

##### Over-length paths are refused with a stable exception

**Tests**: `ScratchFolder_ValidateTotalPathLength_OverLimit_ThrowsScratchFolderException`,
`ScratchFolder_Combine_OverLongTotalPath_ThrowsScratchFolderNotPathTooLong`

Proves an over-length total path is refused up front with a `ScratchFolderException`, explicitly asserted
not to be a `PathTooLongException`, turning an opaque I/O failure into a classifiable refusal. Evidence
for `DocDownCore-Output-ScratchFolder-RejectsOverlongPaths`.

##### Slugging is deterministic and safe

**Tests**: `ScratchFolder_Slugify_OverLongTitle_TruncatesToComponentLimit`,
`ScratchFolder_Slugify_TrailingDotsAndSpaces_AreRemoved`,
`ScratchFolder_Slugify_AccentedAndPunctuated_ProducesCleanAsciiSlug`,
`ScratchFolder_Slugify_OnlyPunctuation_ReturnsEmpty`,
`ScratchFolder_Slugify_UnicodeNormalizationForms_ProduceIdenticalSlug`

Proves slugging truncates over-length titles, strips trailing dots and spaces, folds accents to ASCII,
returns empty for punctuation-only input, and yields the identical slug for the NFC and NFD spellings of
the same word, so naming is deterministic across platforms and Unicode forms. Evidence for
`DocDownCore-Output-ScratchFolder-DeterministicNaming`.

##### Every refusal reports a stable reason

**Tests**: `ScratchFolder_Prepare_RefusedFolder_ReportsStableReason`,
`ScratchFolder_ValidateTotalPathLength_OverLimit_ReportsPathTooLongReason`

Proves a refusal carries a stable, machine-readable reason — `scratchFolderNotEmpty`, `pathTooLong` — so
both humans and the engine can classify the specific refusal. Evidence for
`DocDownCore-Output-ScratchFolder-RefusalReported`.
