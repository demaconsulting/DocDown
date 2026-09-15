## Com Subsystem

![DocDown.Visio Structure](VisioView.svg)

### Overview

The Com subsystem is the rendering seam of `DocDown.Visio`, active only where Microsoft Visio is
available. It captures the rendered appearance of each page by driving Microsoft Visio over late-bound
IDispatch. Rather than re-implement the guaranteed content path, its extractor runs the managed
backend against the same sink with rendering suppressed and adds only the rendered pages the managed
backend cannot supply.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap, non-throwing availability probe |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs in delegates |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `IVisioAutomation` | Internal seam | .NET interface | Injected as a stub in tests; the real adapter is Windows-only |
| Microsoft Visio | Outbound, from the adapter | Late-bound IDispatch | Windows-only; one session per render |

### Design

**The extractor.** `VisioComExtractor` identifies `Vsdx` and `Vsdm`, carries priority `0`, and
keeps page rendering applicable for Visio drawings. `ProbeAvailability()` reports `Unavailable(...)`
when no automation factory was supplied and otherwise defers to `VisioComAvailability.Probe()`.
`ExtractAsync` buffers the source once, delegates the managed content path through a render-suppressed
context and a composing sink, records `pages.renderer`, materializes the drawing to a path when
needed, renders each foreground page through `IVisioAutomation`, records a plain note for any page
that could not be rendered, and returns `Produced` on normal completion.

**The availability probe.** `VisioComAvailability` checks the operating system first and then, on
Windows, whether the `Visio.Application` ProgID resolves. It never opens a drawing, never launches
Visio, and never throws. When Visio is available it returns
`Available(providesRenderedPages: true)`; otherwise it returns `Unavailable(reason)` with declarative
prose that never instructs an installation.

**The automation adapter.** `VisioAutomation` is deliberately mechanical. It opens the drawing
read-only, exports each foreground page to a PNG at the requested resolution, returns either PNG
bytes or a failure reason per page, and tears its session down on every path. It holds no extraction
or reporting policy of its own.

### The inline supporting types (D8)

- **`IVisioAutomation`** and **`VisioRenderedPage`** — the rendering seam and its one-result-per-page
  contract.
- **`ComposingDelegatedSink`** and **`DelegatedExtractionContext`** — not owned by this
  subsystem. They live in the shared `DocDown.Office.Com` namespace, because Visio and PowerPoint
  needed byte-equivalent copies of both. The sink suppresses the delegated managed backend's
  `visio.pageRendering` unavailable fact so it does not contradict the rendering this run performed; the fact
  key is a constructor argument, which was the only real difference between the two former copies.
- **`NamespaceDoc`** — the namespace documentation type for `Com`.
