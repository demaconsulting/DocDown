### ScratchFolder

![Output Structure](OutputView.svg)

#### Purpose

`ScratchFolder` owns the absolute output directory for one extraction and is the single gate through
which every Core-side path is allocated, validated, and contained. It is a **security control, not a
convenience wrapper**: its single responsibility is to make it impossible for untrusted document
content to escape, collide with a reserved device name, or exceed the platform's path length.

#### Data Model

`ScratchFolder` is a `sealed class`, immutable after `Prepare` returns.

- **`_absolutePath`** (`string`) — The absolute, normalized path of the prepared folder, exposed as
  `AbsolutePath`.
- **`MaxComponentLength`** (`const int`, 40) — The maximum length of a single slugged path component.
- **`MaxTotalPathLength`** (`const int`, 240) — The maximum length of any allocated absolute path;
  leaves headroom under the classic Windows `MAX_PATH` of 260.
- **`ReservedNames`** (`static HashSet<string>`) — The case-insensitive reserved device-name stems.

The paired `ScratchFolderException` is a `sealed class : Exception` carrying a machine-readable
`Reason` (for example `pathTooLong`, `reservedDeviceName`, `invalidPathComponent`).

#### Key Methods

- **`static ScratchFolder Prepare(string requestedPath, ScratchFolderMode mode)`** — resolves,
  contains, and creates the folder on disk per the mode (below). Performs filesystem I/O.
- **`string Combine(string relativePath)`** — the allocation gate. Rejects a backslash, splits on `/`,
  validates each component, applies `SafePathCombine` containment, then bounds the total length. Returns
  the contained absolute path.
- **`string EnsureSubfolder(string relativeFolder)`** — validates through `Combine`, then creates the
  directory.
- **`static string SafePathCombine(string basePath, string relativePath)`** — containment only: resolves
  both sides with `Path.GetFullPath`, uses `Path.GetRelativePath`, and rejects `..`, `../`, `..\`, and
  rooted results — throwing `ScratchFolderException`, not `ArgumentException`.
- **`static bool IsReservedDeviceName(string fileName)`** — checks the raw component, the component with
  its extension stripped, and the component with trailing dots and spaces stripped.
- **`static string Slugify(string? value, int maxLength = 40)`** — NFKD-normalizes, drops non-spacing
  marks, lowercases with `InvariantCulture`, maps every non-`[a-z0-9]` character to `-`, collapses
  hyphen runs, trims, truncates to `maxLength`, and trims a trailing hyphen again.
- **`static void ValidateTotalPathLength(string absolutePath)`** — refuses any path longer than
  `MaxTotalPathLength` with `ScratchFolderException`.
- **`internal ValueTask WriteTextAsync(...)`** — the shared text-write helper used by the writers;
  normalizes to `\n`, encodes UTF-8 without a BOM. Internal because only the Output writers use it.

`Prepare` semantics per mode: `RequireEmpty` refuses a non-empty existing folder;
`CleanIfDocDownFolder` deletes only the specific files a manifest written for that folder accounts
for, and refuses whenever that cannot be proved; `Overwrite` deletes contents unconditionally;
`CreateUnique` appends `-2`, `-3`, … until an unused name is found.

#### Reuse deletes only what it can prove it wrote

`CleanIfDocDownFolder` is the **default** mode, so it stands between a mistyped `--scratch` path and
a user's files. It does not classify a folder and then empty it. It deletes **exactly the artifacts a
manifest written for this folder accounts for, and nothing else** — and refuses the whole operation
the moment anything is unproved. No code path in this mode empties a directory.

Four steps must all succeed, in order, before any deletion happens, and a fifth guards each deletion
as it happens.

**1. The folder must hold a DocDown manifest.** `LoadDocDownManifest` reads `manifest.json` once and
returns the parsed object only when all six structural conditions hold:

1. `manifest.json` exists and is no larger than `MaxManifestProbeBytes` (8 MiB), bounding the read.
2. It deserializes successfully through the source-generated `DocDownJsonContext`. A truncated,
   partially written, or non-JSON file throws `JsonException` and is refused.
3. `schemaVersion` equals the pinned `SupportedManifestSchema` (`"1.0"`), compared with an ordinal comparison. A
   folder written by a future, unrecognized schema is not provably ours to delete.
4. `tool.name` equals `"DocDown"` **exactly** — never as a substring.
5. `tool.package` starts with `"DemaConsulting.DocDown"`, so any DocDown package's output qualifies.
6. `artifacts` is non-null, which every real DocDown run writes.

An alien-but-valid JSON document — another product's `manifest.json`, say — deserializes with null
members and fails at (3) or (4). Any exception, any doubt, yields `null`, and the refusal is
`scratchFolderNotDocDown`. Returning the parsed manifest rather than a boolean is deliberate: the
later steps then reason about the very object these conditions validated, from one bounded read and
one parse, so the structural check and the binding check cannot disagree about the file's content.

**These six conditions establish only that the file is a DocDown manifest — not that the folder is
DocDown output.** That distinction is the whole point of the next step.

**2. The manifest must be bound to this folder.** `PathBinds` compares the manifest's recorded
`scratchFolder` against the folder being prepared; a mismatch refuses with
`scratchFolderPathMismatch`. Without this, a genuine `manifest.json` copied out of a real run into a
folder of the caller's own documents satisfies all six structural conditions, and a hand-authored one
can too.

Both sides are reduced to the same canonical form — `Path.GetFullPath` to resolve `.`, `..` and
separator style, then `Path.TrimEndingDirectorySeparator` so a trailing separator cannot cause a
false refusal — and compared with `OrdinalIgnoreCase` on Windows and macOS, whose default file
systems are case-insensitive, and `Ordinal` elsewhere; weakening the Linux comparison would accept a
folder the library never wrote. Symbolic links, junctions and short (8.3) names are **deliberately
not resolved**: `File.ResolveLinkTarget` resolves only the final component, so it cannot produce a
true canonical path and would lend false confidence, and resolving would make a destructive
comparison *more* permissive, which is the wrong direction. A manifest is written with exactly the
`Path.GetFullPath` form a re-run of the same requested path reproduces, so a plain normalized
comparison is precise for legitimate reuse; an alias-induced mismatch produces a refusal, which is
the fail-safe direction.

**3. Every file present must be accounted for.** `ArtifactInventory` computes the manifest's
accounted set — its image, page and part paths plus `summary.txt`, `manifest.json` and `content.md` —
and any file beneath the folder that is not in that set refuses with
`scratchFolderUnaccountedContent`. A folder that is partly the caller's is therefore never partly
deleted. This is the **same rule, in the same code**, that `ContractVerifier` applies as `DD0717`, so
the two cannot drift apart. The predicate is file-only, exactly as `DD0717` is.

**4. Every inventoried path must resolve inside the folder.** A manifest can be hand-authored, so
each accounted path is resolved through `SafePathCombine` **before any deletion occurs**; an entry
such as `"../../evil.png"` refuses the whole operation with `scratchFolderManifestPathEscapes`
instead of reaching the file.

**5. Each target must still be the file that was inventoried.** Steps 1–4 all reason about the
folder as it was when step 3 listed it, and the deletions happen afterwards. A file created or
modified at an inventoried path in between would therefore be deleted on the strength of a check
that never saw it — and while image and page paths are content-derived slugs nobody can predict,
`summary.txt`, `manifest.json` and `content.md` are inventoried under fixed names in every run and
are knowable without inspecting anything. So step 3's listing is also used to record, for each
accounted path, whether a file was present and, if so, its length and last-write time; an accounted
path the listing did not contain is recorded as absent. Immediately before each individual deletion
that recording is compared against a fresh reading of the same file, and any difference — including
absent-then-present — refuses with `scratchFolderChangedDuringPreparation`. The recording is built
from the listing step 3 already holds rather than from a second walk, because a later walk would
simply reopen the interval it exists to shorten; and each target is re-read immediately before its
own deletion rather than all targets up front, for the same reason.

##### What step 5 does not do

The interval between checking a file and deleting it is **narrowed, not eliminated**, and this
document states that plainly because claiming more safety than is delivered is the failure mode this
whole guard exists to avoid. Three limits remain:

1. **The gap between the reading and the deletion.** A file can still be replaced between the final
   reading of its state and the delete operation itself. Closing that gap requires
   operating-system-level locking — taking an exclusive handle on every target, or moving the folder
   to a private location before scanning it — which this library deliberately does not do. What step
   5 achieves is reducing the exposed interval from "the whole scan, containment resolution and
   every preceding deletion" to "one file-system operation".
2. **Timestamp granularity.** A modification that changes neither the byte length nor the recorded
   last-write time is not detected. Comparing existence, length and time together reduces this
   considerably — an equal-length rewrite normally advances the timestamp — but where a file
   system's timestamp resolution is coarse, an in-place, same-length overwrite within the resolution
   window can go unnoticed.
3. **The refusal is not atomic.** A mismatch on the n-th target aborts after targets 1..n-1 have
   already been deleted, and nothing is rolled back. This is accepted rather than hidden: every file
   deleted before the abort is an inventoried DocDown artifact, so the blast radius stays bounded by
   the inventory — the same invariant steps 3 and 4 rest on — and **no file of the caller's can be
   lost this way**. The refusal message states it, so an operator reading the message is not left to
   discover it.

Only after all five steps hold for a given file is that file deleted, individually.

Empty directories — typically `images/` or `pages/` left from a previous run — are **kept**. Removing
them would mean deleting something no manifest accounts for, which is the exact behavior this guard
exists to eliminate; an empty directory holds no data; and the next run re-creates the layout
idempotently.

Every refusal is a `ScratchFolderException`, which the engine surfaces as `DD0501` /
`ExtractionFailureKind.ScratchFolderRefused`, and **every refusal message names
`ScratchFolderMode.Overwrite`** — a refusal a caller cannot act on is a dead end, and `Overwrite` is
the deliberate, explicit opt-in to unconditional clearing. `DeleteContents`, which does empty a
folder, is reachable only from `PrepareOverwrite`.

Legitimate reuse is unaffected: every DocDown run, **including a failed one**, writes the full layout
with a `manifest.json` recording the folder it wrote to. A run that crashed before the manifest was
written is refused, which is the safe direction to err in.

Deliberately rejected alternative: a dedicated marker file. It would become a root-level artifact the
contract verifier's exhaustive walk must whitelist, would need a ledger entry, and would perturb the
golden-file layout — three new coupling points for no additional safety, since the manifest already
carries both the identity proof and the folder binding.

Two further alternatives for step 5 were considered and rejected. **Taking an exclusive lock on each
target** would close the remaining gap, but it makes preparation fail against any process holding a
handle — including virus scanners and sync clients on the caller's own output folder — and turns a
bounded, explainable refusal into an unpredictable one. **Moving the folder to a private staging
location before scanning it** would make the scan and the deletion operate on contents nothing else
can reach, but relocating a folder the library has not yet proved is its own is a destructive act
performed *before* the proof, which inverts the order this entire guard depends on. **Comparing each
file against the size the manifest recorded** was also rejected: `ManifestPart` and the three root
artifacts carry no size, and schema 1.0 records no last-write time anywhere, so that comparison
could only ever cover images and pages — the half of the inventory whose paths are content-derived
and unguessable — while leaving the three fixed, predictable root artifacts entirely unprotected.

#### Three distinct controls (Correction C3)

Path safety is three separate controls, and they are **distinct** — no one of them subsumes another:

1. **Containment** (`SafePathCombine`) — resolves both paths and rejects any result that escapes the
   scratch folder (`..`, `../`, `..\`, rooted paths).
2. **Reserved Windows device names** — `CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, and `LPT0`–`LPT9`,
   compared case-insensitively against the component with its extension stripped and with trailing dots
   and spaces stripped. This check runs on **all** platforms, including Linux, so output written on
   Linux stays portable to Windows.
3. **Total path length** — an explicit bound (`MaxTotalPathLength`, 240), refused as
   `ScratchFolderRefused` rather than surfacing an opaque `PathTooLongException` later.

**Containment alone catches neither the reserved-name nor the path-length hazard.** `SafePathCombine`
is purely a containment check; a contained path can still be a reserved device name and can still
exceed the platform length limit. Promoting `SafePathCombine` into Core is therefore *necessary but not
sufficient* — this document states so explicitly to prevent false confidence that "we contain paths, so
we are safe."

Note further that image names, part names, sheet names, slide titles, and (in a later phase) embedded
attachment file names all originate in **untrusted document content**. The reserved-name protection was
previously only an *incidental consequence* of the `{ordinal:D4}-` file-name prefix (a name beginning
with four digits can never equal a reserved stem). It is now an **explicit, tested invariant**: the
reserved-name check runs unconditionally on every allocated component, and a separate test asserts the
prefix invariant, so a future change to the naming scheme cannot silently remove the control.

#### Error Handling

Every refusal is surfaced as `ScratchFolderException` with a machine `Reason` — a backslash or empty
component, a colon/backslash/NUL, a reserved device name, an over-length path, a non-empty folder under
a strict mode, an unproved reuse (`scratchFolderNotDocDown`, `scratchFolderPathMismatch`,
`scratchFolderUnaccountedContent`, `scratchFolderManifestPathEscapes`,
`scratchFolderChangedDuringPreparation`), or an underlying I/O fault
while creating or cleaning. The engine converts this into a
structured `ScratchFolderRefused` failure (`DD0501`) rather than leaking an opaque
`PathTooLongException` or `IOException`. `Combine` and `EnsureSubfolder` also throw `ArgumentException`
for a null or empty relative path. The static string helpers are pure and perform no I/O.

#### Dependencies

- **ScratchFolderException** (supporting type) — the structured refusal.
- **ScratchFolderMode** (supporting type; see *Extraction Subsystem Design*) — the preparation policy.
- **ArtifactInventory** (supporting type) — the shared definition of what a manifest accounts for,
  used by the reuse guard and by `ContractVerifier`'s `DD0717` check.
- **FileState**, **IFileStateReader**, **FileSystemFileStateReader** (supporting types, defined at
  the bottom of `ScratchFolder.cs`) — the recorded state of one inventoried file and the boundary it
  is read through. The boundary exists so the changed-during-preparation refusal can be tested
  deterministically instead of by winning a real race; it has exactly one production implementation
  and nothing outside that file depends on it.
- **ExtractionManifest** (supporting type) — the manifest the reuse guard parses and binds against.
- The .NET Base Class Library (`System.IO`, `System.Globalization`, `System.Text`).

#### Callers

`DocDownEngine` calls `Prepare` at the start of the pipeline. `ExtractionSink` and the writers call
`Combine`, `EnsureSubfolder`, and `WriteTextAsync` for every allocation and write. `ContractVerifier`
and the naming logic use `Slugify` and `IsReservedDeviceName`. See *DocDownEngine Design* and
*ExtractionSink Design*.
