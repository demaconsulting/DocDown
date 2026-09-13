### ScratchFolder

![Output Structure](OutputView.svg)

#### Purpose

`ScratchFolder` owns the absolute output directory for one extraction and is the single gate through
which every Core-side path is allocated, validated, and contained. It is a security control, not a
convenience wrapper.

#### Data Model

`ScratchFolder` is a `sealed class`, immutable after `Prepare` returns.

- **`_absolutePath`** (`string`) — the normalized absolute folder path.
- **`MaxComponentLength`** (`40`) — maximum length of one slugged component.
- **`MaxTotalPathLength`** (`240`) — maximum length of any allocated absolute path.
- **`ReservedNames`** — case-insensitive reserved device-name stems.
- **`SupportedManifestSchema`** (`"2.0"`) — manifest schema version accepted by the reuse guard.

`ScratchFolderException` carries the machine-readable refusal reason.

#### Key Methods

- **`Prepare(string requestedPath, ScratchFolderMode mode)`** — prepares the folder under either
  `CleanIfDocDownFolder` or `Overwrite`.
- **`Combine(string relativePath)`** — validates each relative component, applies containment, then
  enforces the total-path-length bound.
- **`EnsureSubfolder(string relativeFolder)`** — contained directory creation.
- **`SafePathCombine`** — containment check only.
- **`IsReservedDeviceName`** — reserved-name detection applied on every platform.
- **`Slugify`** — deterministic ASCII-safe slug generation.
- **`ValidateTotalPathLength`** — explicit total-path-length refusal.
- **`WriteTextAsync`** — deterministic `
`, no-BOM text writer used by Output writers.

#### CleanIfDocDownFolder deletes only what it can prove

`CleanIfDocDownFolder` is the default mode, so it refuses whenever proof is incomplete. Before any
file is deleted it requires all of the following:

1. `manifest.json` exists, is reasonably small, deserializes through `DocDownJsonContext`, and names
   DocDown with schema version `2.0`.
2. The manifest's recorded `scratchFolder` names this same folder.
3. Every file currently present is accounted for by `ArtifactInventory` from that manifest.
4. Every accounted path resolves inside the folder through `SafePathCombine`.
5. Each target still matches the state recorded during the inventory scan when it is about to be
   deleted.

Only then is each inventoried file deleted individually. `Overwrite` is the explicit escape hatch for
unconditional replacement.

#### Error Handling

Every refusal is surfaced as `ScratchFolderException`, including traversal attempts,
reserved-device-name components, over-length paths, failed destructive-reuse proof, and low-level I/O
faults while preparing the folder.

#### Dependencies

- **`ScratchFolderMode`** — preparation policy from the Extraction subsystem.
- **`ArtifactInventory`** — helper defining which prior files a manifest accounts for.
- **`ExtractionManifest`** and **`DocDownJsonContext`** — manifest probe and structural proof.
- The .NET Base Class Library (`System.IO`, `System.Text`, `System.Globalization`).

#### Callers

`DocDownEngine` calls `Prepare`. `ExtractionSink` and the writers call `Combine`,
`EnsureSubfolder`, and `WriteTextAsync`.
