### DocDownEngine

![Extraction Structure](ExtractionView.svg)

#### Purpose

`DocDownEngine` is the public facade that orchestrates one extraction end to end: options snapshot,
scratch preparation, source read, format detection, extractor selection, backend invocation, note
completion, and artifact serialization. It returns `ExtractionResult` rather than throwing ordinary
extraction failures.

#### Data Model

`DocDownEngine` is a `sealed class` with two private fields and no per-call mutable state.

| Field | Type | Description |
| --- | --- | --- |
| `_registry` | `ExtractorRegistry` | Immutable extractor inventory plus cached availability. |
| `_defaults` | `ExtractionOptions` | Private default options cloned from the builder. |

Because each call clones the effective options before any other work, the engine is safe for
concurrent extractions targeting different scratch folders.

#### Key Methods

- **`ExtractAsync(string documentPath, string scratchFolder, ExtractionOptions? options = null,
  CancellationToken cancellationToken = default)`** — convenience overload building a file-backed
  `DocumentSource`.
- **`ExtractAsync(DocumentSource source, string scratchFolder, ExtractionOptions? options = null,
  CancellationToken cancellationToken = default)`** — the full pipeline.
  - *Preconditions*: `source` non-null; `documentPath` and `scratchFolder` non-empty.
  - *Pipeline*:
    1. clone the effective options;
    2. prepare the scratch folder;
    3. read the source bytes;
    4. detect the format;
    5. enumerate candidates;
    6. select one extractor;
    7. invoke the backend with only `DocumentSource` and `IExtractionContext`;
    8. add any Core-derived notes;
    9. write `content.md`, `manifest.json`, `metadata.json`, and `summary.txt` in that order;
    10. return the immutable `ExtractionResult`.
- **`GetBackends()`** — returns each registered `ExtractorCandidate` using cached availability.
- **`RefreshAvailability()`** — clears cached availability.
- **`GetSelfTestCases()`** — returns Core's two built-in cases,
  `core.layout-invariance` and `core.manifest-schema`, followed by any backend-contributed cases.

#### Page rendering is a request, not a guarantee

`RenderPages` affects selection and note generation in only two ways:

- If the selected extractor's formats are paginated and no available candidate can render pages in
  this environment, the engine still returns `Produced` when extraction succeeded and records a note
  stating that pages were requested but not rendered.
- If page rendering does not apply to the selected format, the request is honored with silence.

A renderer that was available but produced zero page images also yields a note. These are facts about
the extraction, not a third outcome state.

#### Error Handling

The engine throws only for programming errors and cancellation. All ordinary adverse conditions are
returned as data:

- unreadable source;
- unrecognized format;
- no registered or no available extractor for the detected format;
- backend exception;
- scratch-folder refusal.

A scratch-folder refusal is the one case with no written artifacts. Every other unreadable result
writes the normal root artifacts, with the `ExtractionFailure` prose recorded verbatim in both the
summary and the manifest.

#### Dependencies

- **`ExtractorRegistry`** — candidate enumeration, extractor resolution, and self-test contribution.
- **`FormatSniffer`** — format detection.
- **`ExtractorSelector`** — deterministic selection.
- **`ScratchFolder`**, **`ExtractionSink`**, **`ContentWriter`**, **`MetadataWriter`**,
  **`ManifestWriter`**, and **`SummaryWriter`** — output preparation and serialization.

#### Callers

`DocDownBuilder.Build()` creates `DocDownEngine`. Consuming applications and the command-line tool are
its intended callers.
