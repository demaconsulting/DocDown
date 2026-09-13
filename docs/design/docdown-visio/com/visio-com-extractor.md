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
- **`GetSelfTestCases()` / `RunAvailable` / `RunRender`** — contribute two release-time cases.
  `visio.com.available` passes where Visio is registered on Windows and skips cleanly elsewhere.
  `visio.com.render` walks through the door the availability case only knocks on: it builds a
  synthetic single-page drawing in the self-test work folder, renders it through the real
  `VisioAutomation` at 96 DPI, and passes only when exactly one page came back carrying non-empty
  PNG bytes with the PNG signature and plausible pixel dimensions, and the Visio process the render
  started has exited within a short grace period. It skips with a reason naming Microsoft Visio off
  Windows or where the probe reports unavailable, and reports every fault as a failure message rather
  than an exception. It deletes its drawing on every path.
- **`RenderExclusively`** (private) — holds a machine-wide gate across the render, because Visio
  automation shares one host per session: two self-tests running at once would tear each other's
  session down and each would see the other's process. Waiting is bounded, and a case that cannot get
  the gate skips with that reason. The adapter's own watchdog still bounds the render itself.
- **`BuildSelfTestDrawing`** and its element builders (private) — synthesize the render case's
  drawing: the document part, the window part, a letter-sized page, and two invented labeled
  rectangles with explicit geometry. It writes more than `VisioPackageBuilder` does because that
  builder feeds the managed reader, whereas Visio itself refuses a package with no window part and
  draws nothing for a shape with no geometry.
- **`DescribeRenderShortfall`** / **`DescribeProcessShortfall`** / **`IsPng`** / `ReadPngDimensions`
  (private) — judge what the render returned and whether the owned host was released, as plain
  descriptions the case turns into a failure message.

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
