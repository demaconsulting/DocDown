## VisioComExtractor

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioComExtractor` is the rendering backend the engine selects when rendered pages were requested
and Microsoft Visio is available. Its single responsibility is composition: it produces the managed
content path — page names, shape text, topology, images, and metadata — by delegating to the Open
Packaging backend, and adds the one thing that backend cannot, a rendered image of each page, by
driving Microsoft Visio over late-bound COM.

### Data Model

`VisioComExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds one field: a `Func<IVisioAutomation>?` automation factory, which is the
real adapter factory by default and a null or stub factory in tests. It holds no per-extraction
state.

- **`Id`** — `visio-com`. **`DisplayName`** — `Visio (COM automation)`.
- **`SupportedFormats`** — `Vsdx` and `Vsdm`. **`Priority`** — `0`, below the managed backend.
- **`PageRenderingApplicable`** — inherited `true`, because Visio drawings are paginated.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — reports unavailable when the automation factory
  is null and otherwise defers to `VisioComAvailability.Probe()`. It is cheap, side-effect free,
  and never launches Visio.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`**
  — buffers the source once, runs `VisioOpenXmlExtractor` against a `DelegatedExtractionContext`,
  records `pages.renderer`, renders every foreground page through `RenderPagesAsync`, and returns
  `Produced` on normal completion.
- **`RenderPagesAsync`** (private) — materializes the buffered bytes to the source file or a
  temporary path, opens one automation session, renders every page, adds each successful page's
  PNG, and turns any per-page failure into a plain note. Temporary files are deleted on every path.
- **`MaterializePath`** / **`TryDelete`** (private) — prefer the existing source file, otherwise
  write and later delete a temporary `.vsdx` or `.vsdm` copy.
- **`ReportPageFailure`** (private) — records the one-sentence note for a page that could not be
  rendered.
- **`CreateDefaultAutomation`** (private) — constructs the real `VisioAutomation` adapter, guarding
  the Windows-only type so it is never constructed off Windows.
- **`GetSelfTestCases()` / `RunAvailable`** — contribute the `visio.com.available` case, which
  passes where Visio is registered on Windows and skips cleanly elsewhere.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. A missing automation factory at extraction
time raises `VisioExtractionException`, which Core surfaces as an unreadable result. A per-page
export fault is isolated by the adapter into a failure reason and becomes a note here rather than an
exception. `CreateDefaultAutomation` throws `PlatformNotSupportedException` off Windows, a path the
availability probe has already excluded. Cancellation is observed between pages.

### Dependencies

- **DocDown.Core** — the extractor and self-validation contracts, sink, options,
  `DocumentSource`, `EnvironmentFact`, `ExtractionNote`, and outcome types.
- **VisioOpenXmlExtractor** — the delegated managed content extraction.
- **`IVisioAutomation` / `VisioAutomation`** — the rendering seam and its real adapter.
- **`ComposingDelegatedSink` / `DelegatedExtractionContext`** — reconcile the delegated backend's
  rendering statement with the render this backend performs.
- **VisioComAvailability** — the environment probe.

### Callers

The engine resolves and invokes this unit when rendered pages were requested and the backend probes
available. `VisioDocDownBuilderExtensions.AddVisio` constructs it through a factory when a host
builds an engine.
