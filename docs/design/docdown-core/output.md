## Output Subsystem

![Output Structure](OutputView.svg)

The Output subsystem is the sole write path for an extraction. It prepares the scratch folder,
allocates every output path, writes content and resources, and serializes the human- and
machine-readable reports. It does not judge document acceptability: it records only what was written
and which attempted extraction steps could not be completed.

### Overview

Output turns the extractor's emitted content into a predictable on-disk layout while preserving two
properties:

- **Path safety** — every path derived from document content stays contained, rejects reserved device
  names, and respects the total-path-length bound.
- **Honest reporting** — content features describe what the extracted content contains, including a
  deliberate zero for a looked-for feature; notes describe only attempted extraction steps that could
  not be completed.

The subsystem contains six documented units:

- **ScratchFolder** — owns the output directory and enforces path safety. See _ScratchFolder Design_.
- **ExtractionSink** — concrete `IExtractionSink`; allocates paths, writes images and pages, buffers
  content and parts, and records notes and content features. See _ExtractionSink Design_.
- **ContentWriter** — finalizes `content.md` and any `parts/` files. See _ContentWriter Design_.
- **SummaryWriter** — serializes the human-readable `summary.txt`. See _SummaryWriter Design_.
- **ManifestWriter** — serializes `manifest.json`. See _ManifestWriter Design_.
- **ImageTextSelector** — shared image-text ranking policy used by backends when they derive image
  labels. See _ImageTextSelector Design_.

`MetadataWriter` and `ArtifactInventory` are helper types documented inline here. `MetadataWriter`
always writes `metadata.json`; `ArtifactInventory` defines which files a prior manifest accounts for
so `ScratchFolder` can clean only proven DocDown output.

### Interfaces

The subsystem exposes:

- **`IExtractionSink`** — the write-only surface handed to a backend: `AddImageAsync`,
  `AddPageAsync`, `WriteContentAsync`, `AddContentPartAsync`, `ReportDocumentInfo`,
  `ReportDocumentMetadata`, `ReportContentFeature`, `ReportNote`, and `ReportEnvironmentFact`.
- **`ScratchFolder`** — `Prepare`, `Combine`, `EnsureSubfolder`, `SafePathCombine`, `Slugify`,
  `IsReservedDeviceName`, and `ValidateTotalPathLength`.

The engine drives `ContentWriter`, `MetadataWriter`, `ManifestWriter`, and `SummaryWriter`; extractors
touch only `IExtractionSink`.

### Design

`ScratchFolder` resolves the requested output directory and enforces the path-safety policy for every
later allocation. `CleanIfDocDownFolder` is the default reuse mode: it deletes only the files a
manifest written for that same folder accounts for, using `ArtifactInventory`, and refuses whenever
anything about that proof fails. `Overwrite` is the explicit mode for unconditional replacement.

`ExtractionSink` routes all binary writes through `ScratchFolder`, deduplicates identical images by
SHA-256, allocates page and part paths in Core, and records the extractor's document info,
self-reported metadata, content features, notes, and environment facts.

`ContentWriter` decides the shape of `content.md`: single flow, concatenated parts, or an index over
part files. `MetadataWriter` always writes `metadata.json`, omitting blank metadata fields while
preserving authored values and their provenance. `ManifestWriter` emits schema version `2.0`, which
records the tool, scratch folder, source, selected extractor, environment, document metadata,
content features, image/page/part inventories, notes, requested options, and any unreadable failure.
`SummaryWriter` emits the fixed plain-text sections: title and gist, header, optional failure,
backend, environment, document metadata, layout, what was extracted, and the `Could not read` notes
block.

#### Supporting types

The subsystem defines the following supporting types inline because they do not need separate unit
chapters:

- **`ExtractionNote`** — one plain-language fact about an attempted extraction step that could not be
  completed.
- **`ContentFeature`** — counted structural feature of the extracted content, including the
  `LookedFor` zero-count case.
- **`ImageHint`**, **`ImageTransform`**, **`ImageTextCandidate`**, **`ImageTextConfidence`**,
  **`ImageTextSource`**, and **`SelectedImageText`** — the image naming and provenance model.
- **`ContentPart`** and **`ContentPartKind`** — buffered content part identity.
- **`ExtractionEnvironment`** and **`EnvironmentFact`** — runtime context for the run.
- **`MetadataWriter`** — static helper that writes `metadata.json` with per-field provenance.
- **`ArtifactInventory`** — static helper used by `ScratchFolder` to decide which prior files may be
  deleted safely.
- **`ExtractionManifest`** and **`DocDownJsonContext`** — the manifest DTO graph and source-generated
  JSON context.
- **`ScratchFolderException`** — typed refusal returned when a folder or path is unsafe.
