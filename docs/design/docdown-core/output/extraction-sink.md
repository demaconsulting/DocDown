### ExtractionSink

![Output Structure](OutputView.svg)

#### Purpose

`ExtractionSink` is the concrete `IExtractionSink` and the sole write path an extractor is ever handed:
it allocates every output path, writes image and page bytes, buffers content and parts, and records the
honesty stream (document info, diagnostics, gaps, environment facts, and found counts). Its single
responsibility is to accept an extractor's outputs and turn them into contained, deduplicated,
Core-numbered artifacts — so an extractor never constructs a path or sees a `System.IO` output API.

#### Data Model

`ExtractionSink` is a `sealed class` implementing `IExtractionSink`. It is **not thread-safe**: it is
driven by a single extractor on a single logical flow, matching the interface contract.

- **`_folder`** (`ScratchFolder`) — The containment gate every path is validated and written through.
- **`_options`** (`ExtractionOptions`) — The cloned effective options governing image suppression and
  other decisions.
- **`_images`** (`List<RecordedImage>`) — Recorded images in allocation order.
- **`_imageByDigest`** (`Dictionary<string, RecordedImage>`) — Maps an image SHA-256 to its entry for
  deduplication.
- **`_pages`** (`List<RecordedPage>`) and **`_pageIndex`** — Recorded pages, indexed by document page
  number for idempotent replacement.
- **`_parts`** (`List<RecordedPart>`) — Buffered content parts in call order.
- **`_allocatedPaths`** (`HashSet<string>`) — Allocated relative paths, used to resolve collisions.
- **`_content`** (`StringBuilder`) — Buffered single-flow markdown, accumulated across calls.
- **`_diagnostics`, `_gaps`, `_environmentFacts`, `_foundCounts`, `_contentFeatures`** (collections) —
  The honesty stream in emission order. `_contentFeatures` accumulates the counted structural features
  (headings, tables, comments, sheets, charts) under their label, dropping any zero count, and feeds
  both the summary's content outline and the manifest's `contentFeatures` twin.
- **`_documentInfo`** (`DocumentInfo?`) — The most recent reported orientation metadata (last write wins).
- **`_documentMetadata`** (`DocumentMetadata?`) — The most recent reported **self-reported** document
  metadata (last write wins), consumed by `MetadataWriter` for `metadata.json` and by the summary's
  document-metadata block. It is kept distinct from `_documentInfo` so the document's own claims never
  blend with the orientation counts.
- **`_nextImageOrdinal`, `_nextPartOrdinal`, `_nextGapNumber`** (`int`) — Monotonic 1-based
  allocators.

`RecordedImage`, `RecordedPage`, and `RecordedPart` are internal records capturing each artifact's
path, provenance, size, SHA-256, and (for parts) kind/ordinal/title/markdown/character count.
`RecordedImage.Transform` is an `ImageTransform` rather than free text, so the recorded provenance is
drawn from the one closed vocabulary described in _Output Subsystem Design_. `RecordedImage.Description`
and `RecordedImage.DescriptionSource` carry the human-meaningful text a document offered about the image
and its provenance, both taken from the hint and left absent when the hint carries none. The image-to-unit
association is likewise recorded from the hint: `SourcePages` (every 1-based page, slide, or worksheet
that references the image) and `ReferencedByTemplate` (set when a PowerPoint layout or master, or a
Visio master, references it) let the PowerPoint, Visio, and Excel backends surface which slide, page, or
sheet each embedded image belongs to, and tell a template-borne logo apart from a true orphan. The
`RecordedPart.Kind` is a `ContentPartKind`, which now includes `Chart` for a recovered chart's cached
data series.

`ImageHint` carries an optional `Transform` (`ImageTransform?`): `null` means the extractor made no
claim and accepts the passthrough default.

#### Key Methods

- **`ValueTask<string> AddImageAsync(Stream content, ImageHint hint, CancellationToken)`** — buffers the
  bytes, computes their SHA-256, and, if the digest is new, allocates a 1-based ordinal, derives a safe
  slug from the (untrusted) hint, and writes `images/{ordinal:D4}-{slug}.{ext}` — where `ext` follows
  the written bytes' media type (`image/png`→`png`, `image/jpeg`→`jpg`, and so on, else `bin`). A
  byte-identical repeat writes nothing, increments the existing entry's `References`, and returns the
  existing path. Returns the relative link path. The provenance recorded for the image is the
  `ImageTransform` carried on the hint, defaulted to `ImageTransform.Passthrough` when the extractor
  reported none — the honest default, because Core writes whatever bytes it is given verbatim and so
  transforms nothing itself. A byte-identical repeat keeps the first record's transform, since
  identical bytes cannot honestly carry two different provenances.
- **`ValueTask<string> AddPageAsync(int pageNumber, Stream pngContent, CancellationToken)`** — writes
  `pages/page{n:D4}.png` where `n` is the 1-based **document** page number (not a sequence number), so a
  file maps unambiguously to a page even under a page-range request. Re-rendering a page replaces its
  entry so the manifest lists it once. Precondition: `pageNumber >= 1`.
- **`ValueTask WriteContentAsync(string markdown, CancellationToken)`** — appends to the single-flow
  buffer (finalized later by `ContentWriter`).
- **`ValueTask<string> AddContentPartAsync(ContentPart part, string markdown, CancellationToken)`** —
  allocates the real ordinal in call order (ignoring the extractor's advisory `Ordinal`), derives the
  path `parts/{ordinal:D4}-{kind}-{slug}.md` (or `parts/{ordinal:D4}-{kind}.md` for an empty slug),
  validates it now, and buffers the part for `ContentWriter`.
- **`ReportDocumentInfo` / `ReportDocumentMetadata` / `ReportContentFeature` / `ReportDiagnostic` /
  `ReportGap` / `ReportEnvironmentFact` / `ReportFound`** —
  record the honesty stream. `ReportDocumentMetadata` captures the document's own self-reported claims
  for `metadata.json`; `ReportContentFeature` accumulates a counted feature under its label (rejecting a
  blank label or negative count, and silently dropping a zero count so the outline never prints "0
  tables"). `ReportGap` overwrites any caller-supplied `Id` with a dense `GAP-n`, and
  substitutes a Core-authored reason (plus a `DD0701`-adjacent warning) if the gap arrives with no
  reason, so a gap is never silent. `ReportFound` records the ledger denominators (for example "4
  found").

Slug rules and SHA-256 deduplication are shared with `ScratchFolder.Slugify`; ordinals are
Core-allocated so
numbering is dense, stable, and independent of extractor hints.

#### Error Handling

Each method rejects null arguments (`ArgumentNullException`) and `AddPageAsync`/`ReportFound` reject a
non-positive page number / negative count (`ArgumentOutOfRangeException`) — all caller errors. When
`IncludeEmbeddedImages` is `false`, `AddImageAsync` writes nothing, records a single `DD0201`
suppression diagnostic (exactly once), and returns `string.Empty` so a well-behaved extractor emits no
link. Every path is validated through `ScratchFolder.Combine` before any byte reaches disk, so an
unsafe title fails fast with a `ScratchFolderException`. A gap reported without a reason is repaired
rather than accepted, upholding the no-silent-absence invariant.

#### Dependencies

- **ScratchFolder** — the containment gate and byte/text writer. See _ScratchFolder Design_.
- **ExtractionOptions**, **ImageHint**, **ContentPart**, **ContentFeature**, **ExtractionGap**,
  **ExtractionDiagnostic**, **DocumentInfo**, **DocumentMetadata**, **EnvironmentFact**, **GapKind**
  (supporting types).
- `System.Security.Cryptography` (SHA-256), `System.Globalization` — no runtime NuGet dependencies.

#### Callers

`DocDownEngine` constructs the sink and hands it to the backend inside an `IExtractionContext`.
`ContentWriter`, `ManifestWriter`, and `SummaryWriter` read the sink's recorded state to finalize the
layout. See _DocDownEngine Design_ and _ContentWriter Design_.
