## Extraction Subsystem

![Extraction Structure](ExtractionView.svg)

The Extraction subsystem owns the backend model and the pipeline. It holds the registered
extractors, probes their availability, selects one deterministically for the detected format, and
runs one extraction from a source document to the invariant output layout.

### Overview

Extraction turns a detected format and a set of registered backends into a completed extraction. It
is built around four software units:

- **DocDownBuilder** — collects extractor registrations and default options, then builds an engine.
  See _DocDownBuilder Design_.
- **ExtractorRegistry** — materializes the registered extractors, exposes descriptors, and caches
  availability. See _ExtractorRegistry Design_.
- **ExtractorSelector** — a pure static function that chooses one extractor or returns prose
  explaining why none can be chosen. See _ExtractorSelector Design_.
- **DocDownEngine** — orchestrates the whole pipeline and returns `ExtractionResult` instead of
  throwing. See _DocDownEngine Design_.

The subsystem deliberately keeps the selection model small. After Detection names a format,
selection reasons only about four things: supported formats, current availability, whether page
rendering was requested and available, and the stable priority-plus-identifier tie-break. Once those
facts are known, selection is complete.

### Interfaces

The subsystem exposes the public backend contract and the engine facade:

- **`IDocumentExtractor`** — implemented by external backend packages; declares identity,
  supported formats, priority, page-rendering applicability, availability probing, and async
  extraction.
- **`DocDownBuilder`** — `AddExtractor`, `ConfigureDefaults`, and `Build`.
- **`DocDownEngine`** — `ExtractAsync`, `GetBackends`, `RefreshAvailability`,
  `GetSelfTestCases`, and `Extractors`.
- **`ExtractorSelector.Select`** — the pure selection function, exposed for direct testing.

It consumes `FormatDetection` from Detection and the Output subsystem's `ScratchFolder`,
`ExtractionSink`, `ContentWriter`, `MetadataWriter`, `ManifestWriter`, and `SummaryWriter`.
Backends receive only a `DocumentSource` and an `IExtractionContext`; they never see a scratch path.

### Design

`DocDownBuilder` stores extractor instances or factories and snapshots default options at build
time. `ExtractorRegistry` eagerly materializes every registration once so duplicate-identifier errors
surface at build time, then probes availability lazily and caches the result until explicitly
refreshed.

`ExtractorSelector.Select` is a pure function of `FormatDetection`, `ExtractionOptions`, and the
candidate list. It applies four ordered steps:

1. Keep only candidates whose descriptor supports the detected format.
2. Keep only candidates currently available.
3. When `RenderPages` is true, prefer the available candidates whose availability reports
   `ProvidesRenderedPages = true`.
4. Choose the remaining candidate with the highest priority, breaking any tie by the lower ordinal
   identifier.

If step 1 or step 2 produces an empty set, selection returns prose failure naming the detected
format. For well-known modern formats the prose names the DocDown package that provides the
extractor; for legacy binary Office formats it states plainly that no DocDown package supports them.

`DocDownEngine` clones options before any other work, prepares the scratch folder, reads and hashes
source bytes, detects the format, selects an extractor, invokes the backend through
`IExtractionContext`, adds any Core-derived notes, writes the output artifacts, and returns the
immutable `ExtractionResult`.

#### Supporting types

The subsystem defines the following supporting types inline rather than as separate unit documents:

- **`IExtractionContext`** and **`ExtractionContext`** — what the running backend can see.
- **`ExtractorAvailability`** — available or unavailable, plus whether rendered pages are available
  in this environment.
- **`ExtractorDescriptor`** — immutable snapshot of extractor identity, supported formats,
  priority, and page-rendering applicability.
- **`ExtractorCandidate`** — descriptor paired with current availability.
- **`ExtractionOptions`** — render-pages, page-range, image, split, scratch-mode, and timestamp
  options; cloned before use.
- **`ExtractionOutcome`** — `Produced` or `Unreadable`.
- **`ExtractionFailure`** — prose `Summary` and `Explanation` for an unreadable result.
- **`ExtractionResult`** — returned outcome, paths, detected format, selected extractor, failure,
  environment, and notes.
- **`ScratchFolderMode`** — `CleanIfDocDownFolder` or `Overwrite`.
- **`ContentSplitMode`**, **`ImageOutputMode`**, **`PageRange`**, **`DocumentSource`**,
  **`DocumentInfo`**, **`ISelfValidating`**, **`SelfTestCase`**, **`SelfTestContext`**,
  **`SelfTestResult`**, and **`SelfTestStatus`**.
