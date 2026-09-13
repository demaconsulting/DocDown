### ContractVerifier

![Output Structure](OutputView.svg)

#### Purpose

`ContractVerifier` reconciles a finished scratch folder against its own `manifest.json` and reports any
divergence. Its single responsibility is to make the honesty contract **machine-checkable**: it proves
that what the manifest claims matches what is actually on disk, and that every claimed absence is
explained. It is shipped as **public** API so Core's own tests, third-party backend authors, and a
future `docdown --verify` all share one implementation.

#### Data Model

`ContractVerifier` is a `static class` with no state. `Verify` parses the manifest into the
`ExtractionManifest` DTO graph and accumulates findings into a `List<ContractViolation>` (each a `Code`
plus a `Detail`), returning them as an `IReadOnlyList<ContractViolation>` — empty for an honest
extraction.

#### Key Methods

- **`static IReadOnlyList<ContractViolation> Verify(string scratchFolder)`** — the reconciliation.
  - *Precondition*: `scratchFolder` is a readable folder path.
  - *Postcondition*: returns every violation found, or an empty list when the extraction told the
    truth; it **never throws for a contract violation** — it reports it.

The checks, and the codes they emit, are:

| Code | Check |
| ------ | ------- |
| `DD0710` | `manifest.json` is missing. |
| `DD0711` | `manifest.json` is unparsable. |
| `DD0712` | unsupported `schemaVersion`. |
| `DD0713` | a mandatory field is missing. |
| `DD0714` | `summary.txt` is missing. |
| `DD0715` | a listed resource is missing on disk. |
| `DD0716` | a listed resource's size or SHA-256 does not match the bytes on disk. |
| `DD0717` | a file on disk is not listed in the manifest. |
| `DD0718` | the ledger status or count contradicts the filesystem. |
| `DD0719` | a `Partial`/`Absent` artifact has no matching gap. |
| `DD0720` | a gap has an empty reason. |
| `DD0721` | `summary.txt` does not name the backend the manifest says ran. |
| `DD0722` | `complete` does not equal `gaps.length == 0`. |
| `DD0723` | part of the folder could not be read, so that part of the verification did not happen. |

Private helpers implement each check in isolation: `LoadSummary`, `VerifySchemaVersion`,
`VerifyMandatoryFields`, `VerifyListedResources` (with per-resource `VerifyHashedResource` and
`VerifyClaimedRootArtifact`), `VerifyNoUnlistedFiles`, `VerifyLedger` (with `VerifyFolderLedger`),
`VerifyGapCoverage`, `VerifyGapReasons`, `VerifyBackendNamed`, and `VerifyCompleteFlag`. `ToDiskPath`
maps a manifest relative path to an on-disk path; `ArtifactInventory.ToRelativePath` is its inverse.

#### Two-way reconciliation

The verifier reconciles the scratch folder against the manifest in **both** directions. Either half
alone is worthless: the forward half cannot see content that was added, and the reverse half cannot
see content that was promised and never written.

**Forward — everything the manifest claims must exist on disk.** `VerifyListedResources` checks each
listed image and page for existence, exact recorded size, and recomputed SHA-256 (`DD0715` /
`DD0716`), and each listed part for existence (`DD0715`). It then checks the four fixed root
artifacts through `VerifyClaimedRootArtifact`: when the completeness ledger claims `summary.txt`,
`manifest.json`, `metadata.json`, or `content.md` is `present` or `partial` and the file is absent,
that is `DD0715`
in the same direction as a missing image. These four cannot appear in the manifest's resource
arrays — the manifest cannot list itself by hash — so the ledger is where their claim lives.

**Reverse — every file on disk must be accounted for by the manifest.** `VerifyNoUnlistedFiles`
walks **every file beneath the scratch root at any depth**, converts each to a forward-slash relative
path so the comparison is platform-neutral, and reports `DD0717` for anything not in the accounted
set. That set — the four fixed root artifacts plus the manifest's image, page, and part paths — is
defined once, in `ArtifactInventory`, and is the same set `ScratchFolder` uses to decide what it may
delete on a destructive reuse; holding it in one place is what stops the two controls from drifting
into different ideas of "accounted for". The walk is deliberately exhaustive
rather than limited to the direct children of `images/`, `pages/`, and `parts/`: a shallow walk could
not see a stray file at the scratch root or one hidden inside `images/thumbs/`, which is precisely
where undocumented content would hide.

#### Inability to verify is not verification

A verifier that cannot read a directory knows nothing about that directory. Reporting that as "no
unlisted files found" would be the exact dishonesty this component exists to detect — an unreadable
subtree would suppress `DD0717` entirely, and a blocked read would be indistinguishable from an empty
folder.

Every directory read therefore returns a `FileListing`, which carries either the files or the path
and the reason the read failed. On failure:

- `VerifyNoUnlistedFiles` reports `DD0723` naming the folder and the reason, and **withholds** the
  unlisted-file conclusion entirely rather than drawing it from a listing it never obtained.
- `VerifyFolderLedger` reports `DD0723` and **skips** the count comparison. Substituting zero for an
  unknown count would invent a `DD0718` contradiction the evidence does not support — a false
  accusation against an honest manifest — and would equally conceal a real one.

The result stays distinguishable from a clean pass by the ordinary means: `Verify` returns a
non-empty list, so every existing caller, including `ContractAssert.NoViolations`, fails on an
incomplete verification without any change. `DD0723` is a distinct code from `DD0717` because the two
are different propositions: "a file exists that should not" is an affirmative finding, while "I could
not look" is the absence of a finding.

**The enumeration boundary is injected.** `Verify(string)` delegates to an internal
`Verify(string, IDirectoryEnumerator)` whose default implementation,
`FileSystemDirectoryEnumerator`, reads the real file system; both types are internal to
`ContractVerifier.cs`. The seam exists for one reason: an unreadable directory cannot be simulated
portably across `net8.0`/`net9.0`/`net10.0` on both Windows and Linux — `chmod 000` has no effect
when the test process runs as root, which is common in CI containers, and the Windows equivalent
requires access-control edits unavailable in some runners. Injecting the boundary is the only way to
exercise the failure path deterministically. The production path always uses the real file system.

**Explicit integrity scope.** Images and pages carry a recorded size and SHA-256, so their *bodies*
are verified. Parts do not: the manifest schema records no hash for a part, so a part is
**existence-checked only** and a modified part body will pass. This limit is stated rather than
implied, because a verifier that quietly suggested more coverage than it has would violate the very
honesty rule it exists to enforce. Recording part hashes would be a manifest schema change and is out
of scope here.

#### The backend check is anchored, not searched

`VerifyBackendNamed` parses `summary.txt` line by line for the one line beginning
` Selected  : `, splits the remainder on the first ` - `, and requires the segment before it to
equal `extractor.Id` with an ordinal comparison and the segment after it to begin with `extractor.DisplayName`. A
whole-file substring search would be open to spoofing: the backend identifier legitimately appears in the
candidate trace and in narrative text, so a summary whose authoritative `Selected` line named a
*different* backend could still have passed. The check now means what it claims. It remains skipped
when no extractor was selected or no summary is available.

#### Error Handling

`ContractVerifier` inverts the usual exception model: a missing or unparsable manifest, a missing
summary, a hash mismatch, an unlisted file, a ledger contradiction, an unexplained absence, an empty
gap reason, a backend-name mismatch, and a `complete`-flag inconsistency are all **reported as
violations**, not thrown. This is deliberate — a verifier that threw on the first problem could not
enumerate the rest, and could not be used as a conformance check that returns a full report. It reports
rather than throws so a caller always receives the complete list.

Tolerating an I/O error is **not** the same as ignoring it. An unreadable listed resource is reported
as `DD0715`, and a directory that cannot be read is reported as `DD0723`; in neither case is the
failure converted into evidence of correctness. No check in this component draws a conclusion from
data it failed to obtain.

#### Dependencies

- **ExtractionManifest**, **DocDownJsonContext**, **ContractViolation**, **ArtifactInventory**
  (supporting types; see *Output Subsystem Design*). `ArtifactInventory` holds the shared definition
  of what a manifest accounts for, so the `DD0717` check and `ScratchFolder`'s reuse guard apply one
  rule.
- `System.Security.Cryptography` (SHA-256), `System.Text.Json` (in-box) — no runtime NuGet
  dependencies, preserving Core's zero-dependency posture.

#### Callers

`DocDownEngine.GetSelfTestCases` uses `Verify` in its `core.gap-accuracy` self-test. Core's own test
suite calls it after every extraction test (via a `ContractAssert.NoViolations` helper) so that the
honesty contract is asserted on every case, and third-party consumers may call it directly. It is not
required by the extraction pipeline itself — it is an independent, after-the-fact check.
