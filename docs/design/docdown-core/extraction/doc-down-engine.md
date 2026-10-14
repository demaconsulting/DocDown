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
    9. write `content.md` through `ContentWriter`, then `review-comments.md` through
       `ReviewCommentsWriter` (only when comments were reported), then `manifest.json`,
       `metadata.json`, and `summary.txt` in that order;
    10. return the immutable `ExtractionResult`.
  - *Failure path*: an unreadable source, an unrecognized format, a selection failure, or a backend
    exception is returned as an `Unreadable` result through the same serialization steps, except that
    `content.md` is not written.
    `ReviewCommentsWriter` still runs, because a backend may have reported comments before the step
    that failed; it writes nothing when none were reported.
- **`Extractors`** — the descriptors of the registered extractors, in registration order, without
  probing availability.
- **`GetBackends()`** — returns each registered `ExtractorCandidate` using cached availability.
- **`RefreshAvailability()`** — clears cached availability.
- **`GetSelfTestCases()`** — returns Core's two built-in cases,
  `core.layout-invariance` and `core.manifest-schema`, in category `core`, followed by the cases
  contributed by each registered `ISelfValidating` extractor in registration order. Each Core case
  runs a trivial extraction through the real pipeline with an in-process stub extractor in a unique
  case folder, applies its assertion, and always removes the folder afterwards. A backend whose cached
  probe reports it unavailable has its cases wrapped so they return `Skipped` with a reason naming the
  backend rather than running, and a backend that throws while enumerating its cases contributes none,
  so one misbehaving backend cannot break the suite.

#### Page rendering is a request, not a guarantee

`RenderPages` affects selection and note generation in only two ways:

- If the selected extractor's formats are paginated and no available candidate can render pages in
  this environment, the engine still returns `Produced` when extraction succeeded and records a note
  stating that pages were requested but not rendered.
- If page rendering does not apply to the selected format, the request is honored with silence.

Separately, when the caller sets `IncludeEmbeddedImages` to `false`, the engine records a note that
embedded image extraction was disabled by the caller and no images were written.

A page-rendering backend that produces zero page images is responsible for explaining that outcome
itself, using only its own local knowledge (for example, the page or slide count it selected to
render). The engine does not attempt to infer or suppress such a note from the backend's reported
note count: a backend's extraction phase may legitimately record unrelated notes before rendering
even starts (for example, a delegated base extractor's own content notes), and the engine cannot
tell those apart from a note that actually explains the empty-pages outcome. Leaving this
responsibility to the backend, which has perfect local knowledge, avoids both missing and duplicated
notes. These are facts about the extraction, not a third outcome state.

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
- **`ScratchFolder`**, **`ExtractionSink`**, **`ContentWriter`**, **`ReviewCommentsWriter`**,
  **`MetadataWriter`**, **`ManifestWriter`**, and **`SummaryWriter`** — output preparation and
  serialization.

#### Callers

`DocDownBuilder.Build()` creates `DocDownEngine`. Consuming applications and the command-line tool are
its intended callers.
