### ManifestWriter

![Output Structure](OutputView.svg)

#### Purpose

`ManifestWriter` serializes `manifest.json`, the machine-readable twin of `summary.txt`, and runs the
**completeness-ledger reconciliation** immediately before serialization. Its single responsibility is
to produce the trim/AOT-safe, deterministic JSON manifest and to guarantee — as it builds it — that no
partial or absent artifact goes unexplained.

#### Data Model

`ManifestWriter` is a `static class` with no state. It maps the in-memory extraction report and the
sink's recorded artifacts into the `ExtractionManifest` DTO graph, then serializes that graph. The DTO
graph uses **only** `string`, numeric, `bool`, nullable, and `IReadOnlyList<>` members: every enum is
mapped to its camelCase string by the writer, so no `JsonStringEnumConverter` is needed and the output
stays trim- and AOT-safe.

#### On-disk format

`manifest.json` is serialized through a **source-generated** `JsonSerializerContext`
(`DocDownJsonContext`) with `WriteIndented = true` and
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, written with `\n` line endings and no BOM. The
shape carries:

- `"schemaVersion": "1.0"`, and a `tool` block naming DocDown and the producing package.
- `scratchFolder`, `extractedAtUtc`, `status`, and the load-bearing boolean `complete`.
- `source` (path, file name, size, SHA-256, format, media type, detection basis).
- `extractor` (id, display name, package, capabilities, priority, fidelity) and `selection` (mode,
  required and satisfied capabilities, the candidate trace).
- `environment` (operating system, architecture, runtime, identifier, facts) and `document` metadata.
- `artifacts` — the completeness ledger, one entry per core artifact with its status and counts.
- `images`, `pages`, and **`parts`** (added alongside images and pages), each listing the produced
  resources; images record a `references` count for the SHA-256 deduplication, and a `description` with
  its `descriptionSource` provenance when the document offered text about the image (both `null`
  otherwise).
- `gaps`, `diagnostics`, `requestedOptions`, and `failure` (null on success).

The manifest deliberately does **not** include a `derivedFrom` field; the convert-to-PDF delegation
seam that would have populated it was retracted. Enum-valued fields are emitted as camelCase strings
(for example `"detectionBasis": "contentSignature"`, `"status": "degraded"`) by the writer's mapping
helpers, keeping the DTO graph converter-free.

#### The ledger reconciliation

Before serializing, `ManifestWriter` enforces the load-bearing invariant: **every `ArtifactEntry`
whose `Status` is `Partial` or `Absent` must be explained by at least one `ExtractionGap` whose
`Target` matches.** An unexplained absence gets a synthesized gap with reason *"the extractor did not
report why this artifact is absent"* and diagnostic `DD0701`. `complete` (and
`ExtractionResult.IsComplete`) is exactly `Gaps.Count == 0`. This is why a silent hole is structurally
impossible: the reconciliation runs unconditionally, so the worst case is an admitted, coded gap.

#### Key Methods

- **`static ValueTask WriteAsync(...)`** — reconciles the ledger, builds the `ExtractionManifest` via
  `BuildManifest`, serializes it through `DocDownJsonContext`, and writes it through the scratch-folder
  gate.

Private helpers map each enum to its camelCase string (`OutcomeString`, `BasisString`, `ModeString`,
`CandidateOutcomeString`, `StatusString`, `GapKindString`, `GapScopeString`, `SeverityString`,
`FailureKindString`, `ImageOutputString`, `TransformString`, `SplitString`, `ScratchModeString`) and
format the timestamp and page range with `InvariantCulture`. Each projection is `switch`-based and
throws `ArgumentOutOfRangeException` on a value it has not been taught to serialize, so a vocabulary
member added without a projection fails at the first attempt to write it rather than reaching a
consumer's manifest as an unrecognized or missing value. `TransformString` projects `ImageTransform`
(`Passthrough`→`passthrough`, `DecodedToPng`→`decodedToPng`) for each recorded image.

#### Error Handling

The writer performs no I/O beyond the single manifest write, which goes through
`ScratchFolder.WriteTextAsync` (deterministic `\n`/no-BOM encoding; a path fault surfaces as
`ScratchFolderException`). It raises no exceptions of its own for content conditions — an unexplained
absence is repaired by synthesis, not by throwing. Serialization is total over the DTO graph because
the graph is converter-free.

#### Dependencies

- **ScratchFolder** — the text write gate. See *ScratchFolder Design*.
- **ExtractionManifest**, **DocDownJsonContext**, **ArtifactLedger**, **ArtifactEntry**,
  **ExtractionGap**, **ExtractionDiagnostic** (supporting types; see *Output Subsystem Design*).
- `System.Text.Json` (in-box) — no runtime NuGet dependencies.

#### Callers

`DocDownEngine` calls `ManifestWriter` in the serialization step, after `ContentWriter` and before
`SummaryWriter`, so the reconciled ledger and any synthesized gaps are reflected in both twins. See
*DocDownEngine Design*.
