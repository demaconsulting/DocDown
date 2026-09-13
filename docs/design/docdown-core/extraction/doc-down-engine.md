### DocDownEngine

![Extraction Structure](ExtractionView.svg)

#### Purpose

`DocDownEngine` is the public facade that orchestrates one extraction end to end: options snapshot,
scratch preparation, source read, format detection, extractor selection, backend invocation, gap
derivation, ledger reconciliation, and serialization of the invariant output layout. Its single
responsibility is *orchestration* — it delegates detection, selection, and writing to their units and
returns a structured `ExtractionResult` rather than throwing.

#### Data Model

`DocDownEngine` is a `sealed class` with three private fields; it holds no per-call mutable state.

| Field | Type | Description |
| ------- | ------ | ------------- |
| `_registry` | `ExtractorRegistry` | The immutable registry of extractors; only its availability cache is mutable. |
| `_defaults` | `ExtractionOptions` | The engine's private default options, cloned from the builder; never mutated. |
| `_selector` | `ExtractorSelector` | The stateless, pure selector, reused across concurrent extractions. |

Because the engine holds no per-call state and each call clones its options on entry, it is safe for
concurrent `ExtractAsync` calls targeting *different* scratch folders. Two concurrent calls into the
*same* scratch folder are a caller error and are not supported.

#### Key Methods

- **`ValueTask<ExtractionResult> ExtractAsync(string documentPath, string scratchFolder,
  ExtractionOptions? options = null, CancellationToken cancellationToken = default)`** — a convenience
  overload that builds a `DocumentSource` from the path and delegates to the stream overload.
- **`ValueTask<ExtractionResult> ExtractAsync(DocumentSource source, string scratchFolder,
  ExtractionOptions? options = null, CancellationToken cancellationToken = default)`** — the pipeline.
  - *Preconditions*: `source` non-null; `scratchFolder` (and `documentPath` on the other overload)
    non-null and non-empty. These are the **only** exceptions the method throws by design (plus
    `OperationCanceledException`).
  - *Pipeline order* (fixed): (1) validate arguments; (2) **clone** the effective options before any
    other work (Correction C5) and resolve the timestamp from `TimestampUtc` or the wall clock; (3)
    prepare the scratch folder — a refusal short-circuits to a `ScratchFolderRefused` result with no
    files written; (4) read and hash the source — an I/O fault becomes `SourceUnreadable` with the full
    layout written; (5) sniff the format — `Unknown` becomes `FormatNotRecognized` with the full layout
    written; (6) select a backend — a selection failure is written with a full candidate trace; (7)
    invoke the selected backend with only the `DocumentSource` and an `IExtractionContext`; (8) add
    Core-derived gaps and reconcile the completeness ledger; (9) finalize `content.md`/`parts/`, then
    write `manifest.json`, then `summary.txt`; (10) return the `ExtractionResult`.
- **`IReadOnlyList<BackendStatus> GetBackendStatus()`** — projects each candidate's descriptor and
  cached availability into a flat status, without opening any document.
- **`void RefreshAvailability()`** — delegates to the registry to discard cached availability.
- **`IReadOnlyList<SelfTestCase> GetSelfTestCases()`** — returns Core's three own cases (layout
  invariance, gap accuracy via `ContractVerifier`, manifest schema; category `core`) followed by the
  union of the cases contributed by every registered `ISelfValidating` extractor, in registration
  order. A backend whose cached probe reports unavailable has its cases wrapped to return
  `SelfTestResult.Skipped(reason)` without running, so a case that cannot run is never mistaken for a
  pass.
- **`IReadOnlyList<ExtractorDescriptor> Extractors`** — the registry descriptors; probe-free.

The extractor receives only the `DocumentSource` and an `IExtractionContext`; it is never given
`scratchFolder` or any absolute path, which is the load-bearing isolation invariant of the library.

#### Page rendering is a request, not a promise

Step 8's Core-derived gaps include how the engine answers a `RenderPages` request the selected backend
cannot meet, and the answer depends on whether the format is paginated:

- **Paginated format the backend cannot render** (`PageRenderingApplicable` is true, but the effective
  capabilities lack `RenderedPages`) — the run **degrades** with a counted `pages` gap
  (`GapScope.Unavailable`) plus `DD0301` and a degraded-capability diagnostic, and the gap names any
  registered-but-unavailable backend that *could* have rendered this format, so the reader learns
  precisely which backend would have delivered pages and why it did not.
- **Non-paginated format** (`PageRenderingApplicable` is false) — the request is honored with
  **silence**: an informational `DD0303` diagnostic records that page rendering applied to nothing
  because the format has no page grid, **no gap is emitted, and the run is not degraded**. This is the
  deliberate application of the rule that *an absence no environment could ever fill is not a
  shortfall* — no host, however configured, would make a non-paginated format paginate, so treating
  its missing pages as a gap would be a false failure. Consistently, the `RenderedPages` capability is
  masked out of the selection-fit comparison for such a format, so a legitimately inapplicable
  capability never reads as a missing one.

A render request that *was* applicable and supported but yielded zero pages is the third case: a
`PartiallyExtracted` `pages` gap with `DD0302`, because there the environment could have produced pages
and did not.

#### Error Handling

The engine **returns failures rather than throwing them**. Exceptions are reserved for programming
errors — a null or empty argument raises `ArgumentNullException`/`ArgumentException` — and for
`OperationCanceledException`, which propagates (from the source read or the backend) so a caller can
observe cancellation. Every other adverse condition becomes a structured `ExtractionFailure` on the
returned result:

- A **backend that throws** any other exception is caught and converted to `ExtractorFailed`
  (`DD0703`), carrying the exception type and message; the full layout is still written.
- A **failed extraction still writes the full layout**, including a `summary.txt` whose `Failure`
  section reproduces the explanation verbatim and a `manifest.json` with the same structured failure.
- The **one documented exception** is a refused scratch folder: because Core cannot write into a folder
  it refused, `ScratchFolderRefused` (`DD0501`) returns with `ScratchFolder` set to the requested
  absolute path and `SummaryPath`/`ManifestPath` set to the paths that *would* have been used, but no
  files are written.

Availability-probe faults are already contained by the registry; the engine surfaces the resulting
diagnostics into the result.

#### Dependencies

- **ExtractorRegistry** — candidates, availability diagnostics, resolution, self-test enumeration.
- **FormatSniffer** (Detection) — format detection through the static `FormatSniffer.Detect`; the
  engine holds no sniffer instance.
- **ExtractorSelector** — candidate ranking.
- **ScratchFolder**, **ExtractionSink**, **ContentWriter**, **ManifestWriter**, **SummaryWriter**,
  **ContractVerifier** (Output) — scratch preparation, the write path, serialization, and the self-test
  reconciliation. See *Output Subsystem Design*.
- Supporting types throughout (`ExtractionOptions`, `ExtractionResult`, `ExtractionFailure`,
  `ExtractionEnvironment`, `ExtractionGap`, and others; see *Extraction Subsystem Design*).

#### Callers

`DocDownEngine` is the product of `DocDownBuilder.Build()` and the primary public entry point for a
consuming application (and, in a later phase, the `docdown` command-line tool). It is not called by any
other unit within the subsystem.
