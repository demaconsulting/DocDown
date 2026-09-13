### ContractVerifier Verification Design

This document describes the unit-level verification strategy for `ContractVerifier`, which reconciles a
finished scratch folder against its own `manifest.json` and reports any divergence, making the honesty
contract machine-checkable.

#### Verification Approach

`ContractVerifier` is verified in isolation through unit tests in `ContractVerifierTests.cs` under the
`Output` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ContractVerifier_`.

Verification proves the verifier detects dishonesty in **both directions**: that every file the manifest
lists actually exists on disk, and that every file on disk is listed in the manifest. Nothing is mocked;
the verifier's documented dependencies — the real writer chain, `ExtractionManifest`,
`System.Text.Json`, and `System.Security.Cryptography` — are all used for real. Each test first builds a
genuine, honest extraction folder through the real writers into a per-test `TempScratch` folder, then
**deliberately corrupts it once per violation code** — deleting a listed image (missing artifact),
adding an unlisted file (the reverse direction), overwriting image bytes (hash mismatch), editing the
ledger (ledger mismatch), leaving a partial artifact unexplained (unexplained absence), renaming the
backend in the summary (backend mismatch), blanking a gap reason, flipping the complete flag, corrupting
the schema version, and removing a mandatory field. Building a real folder rather than a mock is
essential, because the verifier's whole purpose is to compare a real manifest against real bytes; a mock
manifest would not evidence that reconciliation.

A defining property is that `Verify` **reports rather than throws** for every corruption, including an
absent folder and an unparsable manifest: it returns the complete list of violations so a caller always
receives a full verdict instead of stopping at the first problem. That inverted exception model is
asserted directly.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds the honest folder that is then corrupted
- **Mocking**: none for the reconciliation scenarios; the real writer chain builds the folder and the
  real verifier reads it
- **Injected boundary**: the unreadable-folder scenarios supply an `IDirectoryEnumerator` that fails
  for exactly one path and delegates every other read to the real file system. This is a deliberate
  deviation from the "no mocking" approach used everywhere else in this unit, and it is recorded
  because an unreadable directory **cannot be simulated portably** across net8.0, net9.0 and net10.0
  on both Windows and Linux: `chmod 000` has no effect when the test process runs as root, which is
  common in CI containers, and the Windows equivalent requires access-control edits that are not
  available in every runner. Injecting the boundary is the only way to exercise the failure path
  deterministically on every supported target. The folder under test is still a genuine extraction
  folder built by the real writers, and only the directory read is substituted
- **Isolation**: each test builds and corrupts its own folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `ContractVerifier` unit test run passes when an honest, complete extraction
reports zero violations; when each deliberately introduced corruption is detected with its expected code
— a listed-but-absent artifact, an unlisted file, a hash mismatch, a ledger contradiction, an
unexplained absence, and a backend-name mismatch; when a folder that cannot be read is reported as an
incomplete verification naming the folder and the reason, and never as a clean pass or as a phantom
zero count; and when verification reports rather than throws for
every corruption, including an absent folder and an unparsable manifest. Any missed corruption, wrong
code, false positive on an honest folder, or thrown exception is a failure.

#### Test Scenarios

##### An honest extraction reports no violations

**Test**: `ContractVerifier_Verify_HonestExtraction_ReportsNoViolations`

Proves a correct, internally consistent extraction passes cleanly, establishing the baseline against
which every detected violation is meaningful. Evidence for
`DocDownCore-Output-ContractVerifier-HonestExtractionPasses`.

##### A listed artifact missing from disk is detected

**Tests**: `ContractVerifier_Verify_ListedImageDeleted_DetectsMissingArtifact`,
`ContractVerifier_Verify_SummaryDeleted_DetectsMissingSummary`,
`ContractVerifier_Verify_ManifestClaimsSummaryPresentButAbsent_ReportsListedResourceMissing`

Proves the forward direction of the reconciliation: a manifest-listed image that has been deleted, a
missing summary, and a completeness ledger that still claims `summary.txt` is present after the file
was removed are all reported rather than trusted. The last of these builds a real honest folder,
asserts it verifies clean, then injects exactly one divergence, so the assertion is attributable to
that divergence alone. Evidence for `DocDownCore-Output-ContractVerifier-DetectsMissingArtifact`.

##### A file on disk not accounted for in the manifest is detected

**Tests**: `ContractVerifier_Verify_UnlistedFileAdded_DetectsUnlistedFile`,
`ContractVerifier_Verify_RootLevelUnlistedFile_ReportsUnlistedFile`,
`ContractVerifier_Verify_NestedUnlistedFileUnderImages_ReportsUnlistedFile`

Proves the reverse direction, and proves it is exhaustive. Three divergences are injected
independently — an extra file inside `images/`, a stray file at the scratch root, and a file hidden
one level below `images/` in a `thumbs/` subfolder. The last two are exactly what a walk limited to
the direct children of the resource folders could not see. Each test first asserts the untouched
folder verifies clean, then injects one file, and asserts both the `DD0717` code and that the
reported detail names the file by its forward-slash relative path. Evidence for
`DocDownCore-Output-ContractVerifier-DetectsUnlistedFile`.

##### A subtree that cannot be read is reported, not assumed empty

**Tests**: `ContractVerifier_Verify_UnreadableSubtree_ReportsVerificationIncomplete`,
`ContractVerifier_Verify_UnreadableSubtree_IsNotACleanPass`,
`ContractVerifier_Verify_UnreadableResourceFolder_ReportsIncompleteWithoutFalseLedgerMismatch`

Proves the verifier never converts "I could not check" into "I checked and it was fine". Each test
starts from a genuine honest folder and blocks exactly one directory read.

The first asserts the failure is reported as `DD0723` and that the detail names both the folder and
the reason, so an operator can act on it. The second is the executable statement of the product's
central principle: the identical folder verifies clean when it can be read, and must **not** return
an empty list when the walk is blocked — a blocked read that looked like an empty directory would
silently suppress every `DD0717` finding in that subtree. The third blocks only the `images/` read of
a folder holding one listed image, and asserts both that `DD0723` is reported **and** that no
`DD0718` is raised: substituting zero for an unknown count would manufacture a ledger contradiction
against an honest manifest, which is the same dishonesty in the opposite direction.

Because `Verify` already signals findings by returning a non-empty list, an incomplete verification
is distinguishable from a clean pass to every existing caller, including
`ContractAssert.NoViolations`, without any change to the public contract. Evidence for
`DocDownCore-Output-ContractVerifier-ReportsIncompleteVerification`.

##### Tampered bytes are detected via a hash mismatch

**Test**: `ContractVerifier_Verify_TamperedImageBytes_DetectsHashMismatch`

Proves overwriting a listed artifact's bytes is caught by the recorded SHA-256, giving consumers integrity
assurance. Evidence for `DocDownCore-Output-ContractVerifier-DetectsHashMismatch`.

##### A ledger contradiction is detected

**Test**: `ContractVerifier_Verify_LedgerCountContradictsDisk_DetectsLedgerMismatch`

Proves a ledger status or count that disagrees with the filesystem is detected, so a completeness claim
the artifacts do not support is not trusted. Evidence for
`DocDownCore-Output-ContractVerifier-DetectsLedgerMismatch`.

##### An unexplained absence is detected

**Test**: `ContractVerifier_Verify_PartialArtifactWithoutGap_DetectsUnexplainedAbsence`

Proves a partial or absent artifact with no matching gap is detected, enforcing the honesty guarantee
independently of the writer that should have upheld it. Evidence for
`DocDownCore-Output-ContractVerifier-DetectsUnexplainedAbsence`.

##### A backend-name mismatch is detected

**Tests**: `ContractVerifier_Verify_SummaryOmitsBackend_DetectsBackendMismatch`,
`ContractVerifier_Verify_SummaryMentionsBackendOutsideTheBackendBlock_ReportsBackendMismatch`

Proves a summary that omits the manifest's recorded backend is detected, and — the more interesting
case — that a summary whose authoritative `Selected  :` line names a *different* backend is still
detected even though the real backend identifier appears elsewhere in the same file. That second
scenario is the executable statement that the check is anchored on the backend declaration rather
than searching the whole document for an incidental substring. Evidence for
`DocDownCore-Output-ContractVerifier-DetectsBackendMismatch`.

##### Part bodies are outside the integrity scope

No test asserts that a modified part *body* is detected, because the verifier does not and cannot
detect it: the manifest schema records no hash for a part, so parts are existence-checked only. This
is recorded here as a deliberate, documented limit rather than left as an unstated gap in coverage —
stating it is itself the honesty rule the verifier exists to enforce.

##### Verification reports rather than throws

**Test**: `ContractVerifier_Verify_AbsentAndUnparsable_ReportsRatherThanThrows`

Proves verification never crashes on damaged output: an absent folder and an unparsable manifest are
reported as violations, so a caller always obtains a complete verdict. Evidence for
`DocDownCore-Output-ContractVerifier-ReportsRatherThanThrows`.

##### Further corruptions are detected

**Tests**: `ContractVerifier_Verify_GapReasonBlanked_DetectsEmptyReason`,
`ContractVerifier_Verify_CompleteFlagFlipped_DetectsCompleteMismatch`,
`ContractVerifier_Verify_UnsupportedSchemaVersion_DetectsSchemaViolation`,
`ContractVerifier_Verify_MandatoryFieldRemoved_DetectsMissingField`

Proves the verifier also detects a blanked gap reason, a flipped complete flag, an unsupported schema
version, and a removed mandatory field, each with its own code. These are defensive tests with no linked
requirement.
