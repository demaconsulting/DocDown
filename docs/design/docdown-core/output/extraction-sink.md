### ExtractionSink

![Output Structure](OutputView.svg)

#### Purpose

`ExtractionSink` is the concrete `IExtractionSink` and the only write surface an extractor ever sees.
It allocates output paths, writes image and page bytes, buffers content and parts, and records the
content inventory, notes, metadata, and environment facts needed by the later writers.

#### Data Model

`ExtractionSink` is a `sealed class` implementing `IExtractionSink`. It is intentionally not
thread-safe because one extractor drives it on one logical flow.

Its key fields are:

- **`_folder`** — the `ScratchFolder` containment gate.
- **`_options`** — the cloned effective `ExtractionOptions`.
- **`_images`** and **`_imageByDigest`** — image inventory and SHA-256 deduplication index.
- **`_pages`** and **`_pageIndex`** — rendered pages keyed by document page number.
- **`_parts`** — buffered content parts.
- **`_content`** — buffered single-flow markdown.
- **`_notes`**, **`_environmentFacts`**, and **`_contentFeatures`** — reporting data recorded in
  emission order.
- **`_documentInfo`** and **`_documentMetadata`** — reported orientation metadata and the document's
  self-reported metadata.
- **`_nextImageOrdinal`** and **`_nextPartOrdinal`** — dense Core-side allocators.

#### Key Methods

- **`AddImageAsync`** — buffers image bytes, deduplicates by SHA-256, allocates a safe
  ordinal-prefixed path, and writes the bytes through `ScratchFolder`. When images are disabled it
  returns `string.Empty` and writes nothing.
- **`AddPageAsync`** — writes `pages/pageNNNN.png`, where `NNNN` is the 1-based source page number.
- **`WriteContentAsync`** — appends markdown to the buffered single-flow content.
- **`AddContentPartAsync`** — allocates a dense ordinal-kind-slug path and buffers the part for
  `ContentWriter`.
- **`ReportDocumentInfo`** and **`ReportDocumentMetadata`** — record document-level metadata.
- **`ReportContentFeature`** — accumulates counted content features, keeping a zero only when the
  backend marked the feature as `LookedFor`.
- **`ReportNote`** — records one `ExtractionNote` verbatim.
- **`ReportEnvironmentFact`** — records one environment fact in emission order.

#### Error Handling

The sink rejects null arguments and rejects invalid content-feature and page-number inputs with the
documented exceptions. Path validation is delegated to `ScratchFolder.Combine`, so unsafe names fail
before any byte reaches disk. A blank note is rejected because notes are user-visible facts and must
never be empty.

#### Dependencies

- **`ScratchFolder`** — containment, subfolder creation, and writes.
- **`ExtractionOptions`**, **`ImageHint`**, **`ContentPart`**, **`ContentFeature`**,
  **`DocumentInfo`**, **`DocumentMetadata`**, **`ExtractionNote`**, and **`EnvironmentFact`** —
  supporting types.
- `System.Security.Cryptography` — SHA-256 deduplication.

#### Callers

`DocDownEngine` constructs the sink and hands it to the selected backend through `IExtractionContext`.
`ContentWriter`, `MetadataWriter`, `ManifestWriter`, and `SummaryWriter` later read the sink's
recorded state.
