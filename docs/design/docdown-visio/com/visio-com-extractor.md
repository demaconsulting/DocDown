## VisioComExtractor

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioComExtractor` is the full-superset rendering backend the engine selects when rendered pages are
requested and Microsoft Visio is available. Its single responsibility is composition: it produces the
guaranteed content — page names, shape text, the directed topology, images, and metadata — by delegating
to the managed Open Packaging backend, and adds the one thing that backend cannot, a rendered image of
each page, by driving Microsoft Visio over late-bound COM.

### Data Model

`VisioComExtractor` is a `public sealed class` implementing `IDocumentExtractor` and `ISelfValidating`. It
holds one field: a `Func<IVisioAutomation>?` automation factory, which is the real adapter factory by
default and a null or stub factory in tests. It holds no per-extraction state.

- **`Id`** — `visio-com`. **`DisplayName`** — `Visio (COM automation)`.
- **`SupportedFormats`** — `Vsdx` and `Vsdm`. **`Priority`** — `0`, below the managed backend.
- **`Capabilities`** — the full superset `Text | EmbeddedImages | DocumentMetadata | DocumentStructure |
  RenderedPages`.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — reports unavailable when the automation factory is null
  (the backend cannot work here regardless of the machine); otherwise defers to `VisioComAvailability.Probe`.
  Cheap, side-effect free, and never launches Visio.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  buffers the source once, runs a `VisioOpenXmlExtractor` against a `DelegatedExtractionContext`
  (render-suppressed options, a `ComposingDelegatedSink` wrapping the real sink), records the authoritative
  `pages.renderer` environment fact, and renders every foreground page through `RenderPagesAsync`. Returns
  `Degraded` when the delegated run degraded or any page failed to render, else the delegated outcome.
- **`RenderPagesAsync`** (private) — materializes the buffered bytes to the source file or a temporary path
  (Visio opens a file), opens one automation session, renders every page, adds each successful page's PNG,
  and turns any per-page failure into a `VISIO0004` diagnostic and a counted `pages` gap; the temporary
  file is deleted on every path.
- **`MaterializePath`** / **`TryDelete`** (private) — prefer the existing source file, else write a
  temporary `.vsdx`/`.vsdm`, and delete a temporary file afterward, ignoring any cleanup fault.
- **`ReportPageFailure`** / **`ReportPageFailuresGap`** (private) — the per-page diagnostic and the counted
  gap that names every page that could not be rendered.
- **`CreateDefaultAutomation`** (private) — constructs the real `VisioAutomation` adapter, guarding the
  Windows-only type so it is never constructed off Windows.
- **`GetSelfTestCases`** / **`RunAvailable`** — contribute the `visio.com.available` case, which passes
  where Visio is registered on Windows and skips cleanly elsewhere.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. A missing automation factory at extraction time
raises a `VisioExtractionException`, which Core surfaces as a structured failure. A per-page export fault
is isolated by the adapter into a failure reason and becomes a counted gap here rather than an exception.
`CreateDefaultAutomation` throws `PlatformNotSupportedException` off Windows, a path availability probing
has already excluded. Cancellation is observed between pages.

### Dependencies

- **DocDown.Core** — the extractor and self-validation contracts, the sink, the options, `DocumentSource`,
  `EnvironmentFact`, `ExtractionGap`, `ExtractionDiagnostic`, and the outcome types.
- **VisioOpenXmlExtractor** — the delegated managed content extraction.
- **IVisioAutomation** / **VisioAutomation** — the rendering seam and its real adapter.
- **ComposingDelegatedSink** / **DelegatedExtractionContext** — reconcile the delegated backend's rendering
  statements with the render this backend performs.
- **VisioComAvailability** — the environment probe.
- **VisioDiagnosticCodes** — the `VISIO0004` page-render-failed code.

### Callers

The engine resolves and invokes this unit when rendered pages are requested and it probes available.
`VisioDocDownBuilderExtensions.AddVisio` constructs it, through a factory, when a host builds an engine.
