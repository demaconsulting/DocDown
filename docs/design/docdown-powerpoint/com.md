## Com Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The Com subsystem is the rendering seam of `DocDown.PowerPoint`, active only where Microsoft PowerPoint is
installed. It captures the rendered appearance of each slide — the primary evidence of what a slide means —
by driving Microsoft PowerPoint over late-bound IDispatch. Rather than re-implement the guaranteed content
extraction, its extractor runs the managed Open XML backend against the same sink with rendering suppressed
and adds only the rendered slides the managed backend cannot, so the two backends never diverge on a deck's
content.

The subsystem exists because rendering is environment-dependent and its one true dependency — talking to
Microsoft PowerPoint — cannot be exercised cross-platform. It is therefore built around a narrow seam: the
`IPowerPointAutomation` interface, behind which the whole extraction path is tested with a stub, and behind
which the single Windows-only adapter is the one boundary proven by release-time self-tests rather than by
CI.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability` under 50 ms, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel; pages added through it |
| `IPowerPointAutomation` | Internal seam | .NET interface | Injected as a stub in tests; real adapter Windows-only |
| Microsoft PowerPoint | Outbound, from adapter | Late-bound IDispatch | Windows-only; watchdog-bounded session |

### Design

**The extractor.** `PowerPointComExtractor` declares `Id = "powerpoint-com"`, `SupportedFormats = [Pptx]`,
`Priority = 0`, and the full capability superset `Text | EmbeddedImages | DocumentMetadata |
DocumentStructure | RenderedPages`. The lower priority keeps the managed backend the default; this backend
is chosen only when rendered pages are requested and it probes available. `ProbeAvailability` reports
unavailable when no automation factory was supplied (a test seam) and otherwise defers to
`PowerPointComAvailability`. `ExtractAsync` buffers the source once, runs a `PowerPointOpenXmlExtractor`
against a delegated context whose options force rendering off and whose sink is a `ComposingDelegatedSink`,
records the authoritative `pages.renderer` environment fact, materializes the deck to a temporary path
(because PowerPoint opens a file), and renders every slide through the automation seam. A slide that fails
to export becomes a counted `pages` gap and a `PPTX0004` diagnostic while the remaining slides render; the
temporary file is deleted on every path. The outcome is `Degraded` when the delegated run degraded or any
slide failed to render.

**The availability probe.** `PowerPointComAvailability` checks the operating system first and then, on
Windows, whether the `PowerPoint.Application` ProgID resolves — a registry lookup only, never an
activation. It opens no deck, launches no application, and never throws: any fault is treated as "not
registered". Its unavailable reasons are declarative and use the word "available", never "install", so
they state a fact about the environment the reader cannot act on, matching the library-wide convention.

**The automation adapter.** `PowerPointAutomation` is the whole untestable COM boundary and is deliberately
mechanical: it holds no extraction or gap policy. It activates a PowerPoint session, opens the deck
read-only without a window and without adding it to the recent-file list, exports every slide to a PNG at
the requested resolution, and tears the session down deterministically. The whole session runs inside one
watchdog-guarded call on a dedicated single-threaded-apartment thread, so no COM object outlives the
timeout window and a modal-dialog hang is bounded by force-terminating the owned process, leaving no
orphan. Its correctness in a deployed environment is proven by the release-time self-test cases.

**The inline supporting types (D8).** Several source files in this subsystem are supporting types
documented here and reviewed in the subsystem review-set rather than as units of their own:

- **`IPowerPointAutomation`** — the narrow seam through which the backend reaches PowerPoint, plus the
  `PowerPointRenderedSlide` record (one slide's number and either its PNG bytes or its failure reason).
  Injecting a stub implementation is what makes the whole extraction path testable cross-platform.
- **`ComposingDelegatedSink`** — an `IExtractionSink` wrapper that forwards every call to the real sink
  except the managed backend's `powerpoint.pageRendering : NOT available` fact, which a successful COM run
  makes false. The filter is deliberately narrow so no other honest fact or gap is lost.
- **`DelegatedExtractionContext`** — a private `IExtractionContext` that reuses the outer context but
  substitutes the render-suppressed options and the composing sink, so the managed backend can run against
  the same format, environment, and cancellation token.
- **`PowerPointComDispatch`** — the low-level IDispatch plumbing: single-instance activation, the
  watchdog, property and method invocation, wrapper release, and forced process termination. Confining it
  here keeps `PowerPointAutomation` readable as a session script.
