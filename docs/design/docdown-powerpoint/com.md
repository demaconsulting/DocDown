## Com Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The Com subsystem is the rendering seam of `DocDown.PowerPoint`, active only where Microsoft
PowerPoint is installed. It captures the rendered appearance of each slide by driving Microsoft
PowerPoint over late-bound IDispatch. Rather than re-implement the guaranteed content extraction, its
extractor runs the managed Open XML backend against the same sink with rendering suppressed and adds
only the rendered slides the managed backend cannot produce.

The subsystem exists because rendering is environment-dependent and its one true dependency — talking
to Microsoft PowerPoint — cannot be exercised cross-platform. It is therefore built around a narrow
seam, `IPowerPointAutomation`, behind which the whole extraction path is tested with a stub and behind
which the real Windows-only adapter is the one boundary proven by release-time self-tests rather than
CI.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap probe; no throw; no side effects |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `IExtractionSink` | Outbound, to Core | .NET interface | Only output channel; pages are added through it |
| `IPowerPointAutomation` | Internal seam | .NET interface | Injected as a stub in tests; real adapter is Windows-only |
| Microsoft PowerPoint | Outbound, from adapter | Late-bound IDispatch | Windows-only and watchdog-bounded |

### Design

**The extractor.** `PowerPointComExtractor` declares `Id = "powerpoint-com"`,
`DisplayName = "PowerPoint (COM automation)"`, `SupportedFormats = [Pptx]`, and `Priority = 0`, so the
managed backend remains the default. The descriptor is still applicable to rendering requests. When no
automation factory is present, `ProbeAvailability()` returns `Unavailable(reason)` immediately.
Otherwise it defers to `PowerPointComAvailability`, whose available result is
`Available(providesRenderedPages: true)`. `ExtractAsync` buffers the source once, runs a
`PowerPointOpenXmlExtractor` against a delegated context whose options suppress page rendering and
whose sink is a `ComposingDelegatedSink`, records the authoritative `pages.renderer` environment fact,
materializes the deck to a file path, and renders every slide through the automation seam. A slide
that fails to export becomes a plain note naming the slide while the remaining slides still render.

**The availability probe.** `PowerPointComAvailability` checks the operating system first and then, on
Windows, whether the `PowerPoint.Application` ProgID resolves — a registry lookup only, never an
activation. It opens no deck, launches no application, and never throws. Its unavailable reasons are
declarative and use the word "available", never "install", so they state a fact about the environment
rather than instructing the reader.

**The automation adapter.** `PowerPointAutomation` is the whole untestable COM boundary and is
deliberately mechanical: it activates a PowerPoint session, opens the deck read-only without a window
and without adding it to the recent-file list, exports every slide to a PNG at the requested
resolution, and tears the session down deterministically. The whole session runs inside one
watchdog-guarded call on a dedicated single-threaded-apartment thread, so no COM object outlives the
timeout window and a modal dialog hang is bounded by force-terminating the owned process.

**The inline supporting types.** Several source files in this subsystem are supporting types documented
here and reviewed in the subsystem review-set rather than as units of their own:

- **`IPowerPointAutomation`** — the narrow seam through which the backend reaches PowerPoint, plus the
  `PowerPointRenderedSlide` record that carries one slide's number and either its PNG bytes or its
  failure reason.
- **`ComposingDelegatedSink`** — an `IExtractionSink` wrapper that forwards every call to the real
  sink except the managed backend's `powerpoint.pageRendering : NOT available` fact, which a
  successful COM run makes false. It forwards plain notes unchanged.
- **`DelegatedExtractionContext`** — a private `IExtractionContext` that reuses the outer context but
  substitutes render-suppressed options and the composing sink so the managed backend can run against
  the same format, environment, and cancellation token.
- **`PowerPointComDispatch`** — the low-level IDispatch plumbing: single-instance activation, the
  watchdog, property and method invocation, wrapper release, and forced process termination.
