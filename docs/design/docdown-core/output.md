## Output Subsystem

![Output Structure](OutputView.svg)

The Output subsystem is the **sole write path** for an extraction. It prepares the scratch folder,
allocates and contains every path, writes the content document, images, pages and parts, serializes
`summary.txt`, `manifest.json`, and `metadata.json`, and provides a verifier that reconciles a finished
folder against its own manifest. No component outside this subsystem writes to the scratch folder, and
no extractor is ever handed a filesystem path.

### Overview

Output turns the honesty stream an extractor produces — content, images, pages, parts, document info,
self-reported document metadata, content features, diagnostics, gaps, and environment facts — into the
invariant on-disk layout, and guarantees two
properties while doing so: **path safety** (every path derived from untrusted content is contained,
non-reserved, and length-bounded) and **honesty** (every partial or absent artifact is explained by a
matching gap). The subsystem contains seven software units:

- **ScratchFolder** — owns the output directory and is the single path-safety gate. See
  _ScratchFolder Design_.
- **ExtractionSink** — the concrete `IExtractionSink`; allocates paths, writes image and page bytes,
  buffers content and parts, and records the honesty stream. See _ExtractionSink Design_.
- **ContentWriter** — finalizes `content.md` and any `parts/` files. See _ContentWriter Design_.
- **SummaryWriter** — serializes the human-readable `summary.txt`. See _SummaryWriter Design_.
- **ManifestWriter** — serializes `manifest.json` and reconciles the completeness ledger. See
  _ManifestWriter Design_.
- **ContractVerifier** — reconciles a finished scratch folder against its manifest. See
  _ContractVerifier Design_.
- **ImageTextSelector** — the shared, format-agnostic policy that ranks the text candidates a backend
  gathers for an image and reports how confidently the chosen text describes it, so every backend names
  images and writes alt text the same honest way. See _ImageTextSelector Design_.

### Interfaces

The subsystem exposes:

- **`IExtractionSink`** — the write surface handed to a backend: `AddImageAsync`, `AddPageAsync`,
  `WriteContentAsync`, `AddContentPartAsync`, and the `Report*` methods (`ReportDocumentInfo`,
  `ReportDocumentMetadata`, `ReportContentFeature`, `ReportDiagnostic`, `ReportGap`,
  `ReportEnvironmentFact`, `ReportFound`). Every `Add*` method returns
  the relative, forward-slash path the extractor must use in markdown links.
- **`ScratchFolder`** — `Prepare`, `Combine`, `EnsureSubfolder`, and the static helpers
  `SafePathCombine`, `IsReservedDeviceName`, `Slugify`, `ValidateTotalPathLength`.
- **`ContractVerifier.Verify(string scratchFolder)`** — returns the contract violations, or an empty
  list for an honest extraction.

It consumes `System.Text.Json` (in-box) for manifest serialization, `System.Security.Cryptography` for
SHA-256, and the Extraction subsystem's value types. The engine drives the writers; extractors touch
only `IExtractionSink`.

### Design

`ScratchFolder` resolves the requested folder per its mode, then validates every allocated path
through `Combine`, which enforces containment (`SafePathCombine`), a reserved-device-name check, a
total-path-length bound, and separator/colon/NUL rejection. `ExtractionSink` routes all byte writes
through that gate; it deduplicates images by the SHA-256 of their written bytes, allocates ordinals
Core-side (so numbering is dense and independent of extractor hints), and buffers content parts so
`ContentWriter` can decide the final layout. `ContentWriter` chooses among single-flow, concatenated,
and index layouts. `ManifestWriter` builds the machine twin and runs the ledger reconciliation before
serializing; `SummaryWriter` produces the human twin. `ContractVerifier` re-derives the truth from the
filesystem and the manifest and reports any divergence, reconciling in both directions: every
artifact the manifest claims must exist on disk, and every file anywhere beneath the scratch root
must be accounted for in the manifest.

`ScratchFolder`'s default reuse guard deletes only what it can prove this library wrote. Under
`CleanIfDocDownFolder` it never empties a folder. Four conditions must all hold, in order, before
anything is deleted: the folder holds a structurally valid DocDown `manifest.json`; that manifest's
recorded `scratchFolder` names this very folder; every file present is accounted for by that
manifest — the same `ArtifactInventory` rule the contract verifier applies as `DD0717`; and every
inventoried path resolves inside the folder through the containment check. A fifth then guards each
deletion as it happens: the state each inventoried path had when the folder was listed is compared
against a fresh reading taken immediately before that file is deleted, and any difference — a
changed file, or one created at an inventoried path after the listing — refuses with
`scratchFolderChangedDuringPreparation`. That interval is **narrowed, not eliminated**: a gap
remains between the final reading and the deletion itself that only operating-system-level locking
could close, and the refusal is not atomic, though everything deleted before an abort is an
inventoried DocDown artifact, so no caller file can be lost. Anything unproved — an unbound
manifest, one unlisted file, one escaping path, one file that changed underfoot — refuses the whole
operation with `DD0501`, and every refusal names `ScratchFolderMode.Overwrite` as the deliberate
escape hatch, because a false positive would delete the caller's data.

All text output is written through one helper that normalizes line endings to `\n` and encodes UTF-8
**without** a byte-order mark, using `CultureInfo.InvariantCulture` for all formatting — which is what
makes `summary.txt` and `manifest.json` byte-identical across platforms for the same content.

#### The completeness ledger

The **completeness ledger** (`ArtifactLedger`) is the machine-checkable statement of what the
extraction produced. It has one entry per core artifact — `summary`, `manifest`, `metadata`,
`content`, `images`, `pages` — and each entry (`ArtifactEntry`) records a path, an `ArtifactStatus` of
`Present`, `Partial`,
or `Absent`, and optional `Obtained`/`Found` counts (for example "3 of 4 images"). The ledger is
mirrored into `manifest.json`'s `artifacts` object and summarized in `summary.txt`'s `Completeness`
block.

The **load-bearing invariant**, enforced in `ManifestWriter` before serialization, is:

> Every `ArtifactEntry` whose `Status` is `Partial` or `Absent` MUST be explained by at least one
> `ExtractionGap` whose `Target` matches.

This is enforced by Core, not by extractor discipline. If a backend leaves an absence unexplained,
Core **synthesizes** a gap with the reason _"the extractor did not report why this artifact is
absent"_ and records diagnostic `DD0701`. It is therefore structurally impossible to emit a silent
hole: the worst-case output is an admission of ignorance, never a false impression of completeness.
`ExtractionResult.IsComplete` and the manifest's `complete` flag are simply `Gaps.Count == 0`, giving a
consuming agent one boolean to branch on without parsing prose. Core also derives some gaps itself
because it knows things the extractor does not — pages requested when the backend lacks `RenderedPages`
**and the format is paginated** (`DD0301` + `DD0702`), images suppressed by
`IncludeEmbeddedImages = false` (`DD0201`), whole artifacts
absent because extraction failed, no text written (`DD0101`), and zero pages produced when rendering
was possible (`DD0302`). A render request against a **non-paginated** format is the deliberate
exception: page rendering is a request, and when the format has no page grid to render the request is
honored with silence — an informational `DD0303` diagnostic records that it applied to nothing, no gap
is emitted, and the run is **not** degraded, because an absence no environment could ever fill is not a
shortfall. The `RenderedPages` capability is masked out of the selection-fit comparison in that case so
a legitimately inapplicable capability never reads as a missing one.

#### Supporting types

The following types are defined by the Output subsystem and documented here because they have no
dedicated unit design. Most are immutable value types and thread-safe; `MetadataWriter` and
`ArtifactInventory` are stateless static helpers, and `ScratchFolderException` is an exception.

- **`IExtractionSink`** (`interface`) — The write surface a backend uses: `AddImageAsync`,
  `AddPageAsync`, `WriteContentAsync`, `AddContentPartAsync`, `ReportDocumentInfo`,
  `ReportDocumentMetadata`, `ReportContentFeature`, `ReportDiagnostic`, `ReportGap`,
  `ReportEnvironmentFact`, `ReportFound`. `ReportDocumentMetadata` carries the document's own
  self-reported claims to `MetadataWriter` for `metadata.json`; `ReportContentFeature` accumulates the
  counted structural features (headings, tables, comments, sheets, charts, and the like) that become
  the summary's content outline and the manifest's `contentFeatures` twin.
- **`ImageHint`** (`sealed record`) — Extractor-supplied image metadata: a preferred name, media type,
  optional dimensions, source page, source reference, an optional `Transform`, an optional
  `Description` with its `DescriptionSource` provenance, and the image-to-unit association carried by
  `SourcePages` (every 1-based page, slide, or worksheet that references the image, sorted and
  distinct) and `ReferencedByTemplate` (set when a PowerPoint layout or master, or a Visio master,
  references the image rather than a specific page — the flag that tells a template-borne logo apart
  from a true orphan). The scalar `SourcePage` is a convenience alias for the lowest `SourcePages`
  entry. The naming members are advisory — Core
  allocates the real name — but `Transform`, the description, and the page associations are
  authoritative provenance, because
  only the extractor knows how it produced the bytes and where the picture was referenced from.
  `ExtractionSink` records the description and its source verbatim, defaulting both to absent,
  and `ManifestWriter` serializes them, together with the page associations, as the image's per-image
  inventory (`description`/`descriptionSource`, `sourcePage`/`sourcePages`, `referencedByTemplate`).
- **`ImageTransform`** (`enum`) — The closed vocabulary of image provenance claims: `Passthrough`
  (the bytes were written exactly as the source document stored them) and `DecodedToPng` (the source
  samples were decoded and re-encoded as PNG). The set is closed at two members because there are
  exactly two ways bytes reach `images/`, and a member with no way to produce it would itself be the
  documented-but-unreachable defect this enumeration exists to prevent. `ExtractionSink` records the
  hint's value, defaulting to `Passthrough`; `ManifestWriter.TransformString` projects it to the
  manifest's camelCase `transform` field and throws on an unmapped member.
- **`ImageTextSource`** (`enum`) — The origin of a candidate image text, in decreasing directness:
  `Description`, `Title`, `Caption`, `PictureName`, `Heading`, `Uri`. The declaration order is the
  preference order the `ImageTextSelector` unit ranks by.
- **`ImageTextConfidence`** (`enum`) — How far a selected image text may be trusted: `Descriptive`,
  `Contextual`, or `Fallback`; it is what lets a caller use a heading to name a file yet refuse to
  assert it as a description.
- **`ImageTextCandidate`** (`sealed record`) — One `(Text, Source)` candidate a backend offers to the
  selector; empty text is treated as absent.
- **`SelectedImageText`** (`sealed record`) — The selector's result: the trimmed chosen `Text`, its
  `Source`, and its `Confidence`.
- **`ContentPart`** (`sealed record`) — A content part supplied by an extractor: `Kind`, an advisory
  `Ordinal`, and an optional `Title`.
- **`ContentPartKind`** (`enum`) — `Page`, `Sheet`, `Slide`, `Section`, `Attachment`, `Chart`. `Chart`
  names a data object drawn on a page, sheet, or slide whose cached data series an extractor recovered
  (the Excel and PowerPoint backends emit chart parts; the Word backend, which does not read chart
  data, instead reports the charts as a counted gap). It is declared **last** so the numeric value of
  every pre-existing kind is unchanged.
- **`ContentFeature`** (`sealed record`) — One counted structural feature of the extracted content: a
  plural `Label` (for example `comments` or `tables`) and a positive `Count`. `ExtractionSink`
  accumulates these under their label and drops any zero count; they are rendered as the summary's
  one-line content outline and as the manifest's `contentFeatures` array, so both describe the same
  structure from the same source.
- **`ExtractionGap`** (`sealed record`) — An enumerated gap: Core-assigned `Id`, `Kind`, `Target`,
  `Scope`, a **mandatory** `Reason`, optional `Impact`/`Remedy`, and optional affected count/items.
- **`GapKind`** (`enum`) — `Text`, `Images`, `Pages`, `Structure`, `Metadata`, `Parts`.
- **`GapScope`** (`enum`) — Why content is missing: `NotAttempted`, `Unavailable`,
  `PartiallyExtracted`, `Failed`.
- **`ArtifactLedger`** (`sealed record`) — The six-entry completeness ledger (`Summary`, `Manifest`,
  `Metadata`, `Content`, `Images`, `Pages`), one entry per root artifact and resource folder.
- **`ArtifactEntry`** (`sealed record`) — One ledger entry: `Path`, `Status`, optional
  `Obtained`/`Found`.
- **`ArtifactStatus`** (`enum`) — `Present`, `Partial`, `Absent`.
- **`ExtractionEnvironment`** (`sealed record`) — Provenance: operating system, process architecture,
  runtime version, runtime identifier, and an ordered list of `Facts`. Core populates the fixed fields
  from `RuntimeInformation`.
- **`EnvironmentFact`** (`sealed record`) — One environment fact: a required `Source` naming the
  component that reported it (for example `DocDown.Pdf` or `DocDown.Pdf.Rendering`), a `Key`, a
  `Value`, and an optional `Available` flag. `SummaryWriter` groups facts by `Source`, and the
  manifest twin `ManifestEnvironmentFact` carries the same `source` field into `manifest.json`.
- **`ContractViolation`** (`sealed record`) — One contract-verification finding: a `Code` and a
  `Detail`.
- **`MetadataWriter`** (`static class`) — serializes `metadata.json`, the record of what the document
  asserts about itself. It renders the sink's reported `DocumentMetadata` (populated fields with
  per-field provenance, the interesting fields the document left blank, and a note whenever there is
  sparseness to explain) with a hand-rolled `Utf8JsonWriter`, applying omit-empty and the
  never-bare-`{}` rule. The manifest context cannot be reused because it serializes nulls explicitly,
  the opposite of the omit-empty policy. `OpcMetadataMapper` maps the shared OPC core properties (an
  `OpcCoreProperties` snapshot each backend copies from its property bag) to `DocumentMetadata` once
  for Word, Excel, PowerPoint, and Visio; the PDF backend has its own mapper because its property bag
  and `D:` date encoding differ.
- **`ArtifactInventory`** (`internal static class`) — The single shared definition of what a manifest
  accounts for: its image, page, and part paths plus the four fixed root artifacts (`summary.txt`,
  `manifest.json`, `metadata.json`, `content.md`), and the
  file-only selection of anything on disk that falls outside that set. `ContractVerifier` uses it for
  `DD0717`; `ScratchFolder` uses it to decide what a destructive reuse may delete. A leaf type
  depending on neither consumer, so the two share one rule and cannot drift apart.
- **`ExtractionManifest`** (`sealed record`, a DTO graph) — The `manifest.json` serialization model
  and its nested records (tool, source, extractor, selection, environment, document, content features,
  artifacts, images, pages, parts, gaps, diagnostics, requested options, failure). The per-image
  inventory (`ManifestImage`) carries the full provenance the summary no longer prints —
  `sourcePage`/`sourcePages`, `referencedByTemplate`, `sourceRef`, `sha256`, `transform`, dimensions,
  `references`, and `description`/`descriptionSource`; `ManifestPart` carries each part's `kind`
  (including `chart`), `ordinal`, `title`, and `characterCount`; `ManifestContentFeature` is the
  machine twin of the summary's content outline. Uses only `string`, numeric, `bool`,
  nullable, and `IReadOnlyList<>` members so no enum converter is needed.
- **`DocDownJsonContext`** (`JsonSerializerContext`) — The source-generated serializer context for the
  manifest DTO graph, keeping serialization trim- and AOT-safe.
- **`ScratchFolderException`** (`sealed class : Exception`) — A structured scratch-folder refusal
  carrying a machine `Reason`; converted by the engine into a `ScratchFolderRefused` failure.
