### PowerPointComExtractor

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointComExtractor` is the rendering backend the engine selects when rendered pages are requested
and Microsoft PowerPoint is available. Its single responsibility is composition: it produces the
guaranteed content — slide text, titles, speaker notes, images, and metadata — by delegating to the
managed Open XML backend, and adds the one thing that backend cannot, a rendered image of each slide,
by driving Microsoft PowerPoint over late-bound COM.

### Data Model

`PowerPointComExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds one field: a `Func<IPowerPointAutomation>?` automation factory, which is
the real adapter factory by default and a null or stub factory in tests. It holds no per-extraction
state.

- **`Id`** — `powerpoint-com`. **`DisplayName`** — `PowerPoint (COM automation)`.
- **`SupportedFormats`** — `Pptx` only. **`Priority`** — `0`, below the managed backend.
- **`PageRenderingApplicable`** — `true`; this backend remains eligible when a caller requests slide
  rendering.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — reports unavailable when the automation factory is
  null; otherwise defers to `PowerPointComAvailability.Probe()`. Cheap, side-effect free, and never
  launches PowerPoint.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  buffers the source once, runs a `PowerPointOpenXmlExtractor` against a `DelegatedExtractionContext`
  that suppresses page rendering and wraps the sink in a `ComposingDelegatedSink`, records the
  authoritative `pages.renderer` fact, and renders every slide through `RenderSlidesAsync`. When the
  extraction completes it returns the delegated managed outcome, which is `Produced` for normal
  completion.
- **`RenderSlidesAsync`** (private) — materializes the buffered bytes to the source file or a
  temporary path, opens one automation session, renders every slide, adds each successful slide's PNG
  as a page, and turns any per-slide failure into a one-sentence note naming the slide.
- **`MaterializePath`** / **`TryDelete`** (private) — prefer the existing source file, else write a
  temporary `.pptx`, and delete a temporary file afterward, ignoring any cleanup fault.
- **`ReportSlideFailureNote`** (private) — records a single slide's render failure as a plain note.
- **`CreateDefaultAutomation`** (private) — constructs the real `PowerPointAutomation` adapter,
  guarding the Windows-only type so it is never constructed off Windows.
- **`GetSelfTestCases`** / **`RunAvailable`** / **`RunRender`** — contribute two release-time cases.
  `powerpoint.com.available` passes where PowerPoint is registered on Windows and skips cleanly
  elsewhere. `powerpoint.com.render` walks through the door the availability case only knocks on: it
  builds a synthetic single-slide deck in the self-test work folder, renders it through the real
  `PowerPointAutomation` at 96 DPI, and passes only when exactly one slide came back carrying
  non-empty PNG bytes with the PNG signature and plausible pixel dimensions, and the PowerPoint
  process the render started has exited within a short grace period. It skips with a reason naming
  Microsoft PowerPoint off Windows or where the probe reports unavailable, and reports every fault as
  a failure message rather than an exception. It deletes its deck on every path.
- **`ProbeResourceName`** (private const) — names the embedded deck the render case rasterizes: a
  real `.pptx` authored in Microsoft PowerPoint, read through `SelfTestProbe.Load`. The synthesizer
  that once stood here built a theme, color scheme, font scheme, format scheme, slide master and
  layout, because PowerPoint refuses to open a deck without them.
- **`DescribeRenderShortfall`** / **`DescribeProcessShortfall`** / **`IsPng`** / `ReadPngDimensions`
  (private) — judge what the render returned and whether the owned host was released, as plain
  descriptions the case turns into a failure message.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. A missing automation factory at extraction
time raises a `PowerPointExtractionException`, which Core surfaces as an `Unreadable` result. A
per-slide export fault is isolated by the adapter into a failure reason and becomes a plain note here
rather than an exception. `CreateDefaultAutomation` throws `PlatformNotSupportedException` off
Windows, a path availability probing has already excluded. Cancellation is observed between slides.

### Dependencies

- **DocDown.Core** — the extractor and self-validation contracts, the sink, the options,
  `DocumentSource`, `EnvironmentFact`, `ExtractionNote`, and the outcome types.
- **PowerPointOpenXmlExtractor** — the delegated managed content extraction.
- **IPowerPointAutomation** / **PowerPointAutomation** — the rendering seam and its real adapter.
- **`ComposingDelegatedSink` / `DelegatedExtractionContext`** — reconcile the delegated backend's
  rendering statements with the render this backend performs.
- **PowerPointComAvailability** — the environment probe.

### Callers

The engine resolves and invokes this unit when rendered pages are requested and it probes available.
`PowerPointDocDownBuilderExtensions.AddPowerPoint` constructs it, through a factory, when a host builds
an engine.
