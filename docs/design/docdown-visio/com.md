## Com Subsystem

![DocDown.Visio Structure](DocDownVisioView.svg)

### Overview

The Com subsystem is the rendering seam of `DocDown.Visio`, active only where Microsoft Visio is
installed. It captures the rendered appearance of each page — the spatial arrangement that the file's XML
cannot recover — by driving Microsoft Visio over late-bound IDispatch. Rather than re-implement the
guaranteed content extraction, its extractor runs the managed Open Packaging backend against the same
sink with rendering suppressed and adds only the rendered pages the managed backend cannot, so the two
backends never diverge on a drawing's page names, shape text, or directed topology.

The subsystem exists because rendering is environment-dependent and its one true dependency — talking to
Microsoft Visio — cannot be exercised cross-platform. It is therefore built around a narrow seam: the
`IVisioAutomation` interface, behind which the whole extraction path is tested with a stub, and behind
which the single Windows-only adapter is the one boundary proven by release-time self-tests rather than
by CI.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability` under 50 ms, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel; pages added through it |
| `IVisioAutomation` | Internal seam | .NET interface | Injected as a stub in tests; the real adapter is Windows-only |
| Microsoft Visio | Outbound, from the adapter | Late-bound IDispatch | Windows-only; one session per render |

### Design

**The extractor.** `VisioComExtractor` declares `Id = "visio-com"`, `SupportedFormats = [Vsdx, Vsdm]`,
`Priority = 0`, and the full capability superset `Text | EmbeddedImages | DocumentMetadata |
DocumentStructure | RenderedPages`. The lower priority keeps the managed backend the default; this backend
is chosen only when rendered pages are requested and it probes available. `ProbeAvailability` reports
unavailable when no automation factory was supplied (a test seam) and otherwise defers to
`VisioComAvailability`. `ExtractAsync` buffers the source once, runs a `VisioOpenXmlExtractor` against a
delegated context whose options force rendering off and whose sink is a `ComposingDelegatedSink`, records
the authoritative `pages.renderer` environment fact, materializes the drawing to a temporary path (because
Visio opens a file), and renders every foreground page through the automation seam. A page that fails to
export becomes a counted `pages` gap and a `VISIO0004` diagnostic while the remaining pages render; the
temporary file is deleted on every path. The outcome is `Degraded` when the delegated run degraded or any
page failed to render.

**The availability probe.** `VisioComAvailability` checks the operating system first and then, on Windows,
whether the `Visio.Application` ProgID resolves — a registry lookup only, never an activation. It opens no
drawing, launches no application, and never throws: any fault is treated as "not registered". Its
unavailable reasons are declarative and use the word "available", never "install", so they state a fact
about the environment the reader cannot act on, matching the library-wide convention.

**The automation adapter.** `VisioAutomation` is the whole untestable COM boundary and is deliberately
mechanical: it holds no extraction or gap policy. It opens the drawing read-only, exports every foreground
page to a PNG at the requested resolution — cropping to the drawing extent so pixel dimensions are read
back from the written image — and releases its session on every path so no COM object outlives the render.
Its correctness in a deployed environment is proven by the release-time self-test cases.

### The inline supporting types (D8)

Several source files in this subsystem are supporting types documented here and reviewed in the subsystem
review-set rather than as units of their own:

- **`IVisioAutomation`** — the narrow seam through which the backend reaches Visio, plus the
  `VisioRenderedPage` record (one page's number and either its PNG bytes or its failure reason). Injecting
  a stub implementation is what makes the whole extraction path testable cross-platform.
- **`ComposingDelegatedSink`** — an `IExtractionSink` wrapper that forwards every call to the real sink
  except the managed backend's `visio.pageRendering : NOT available` fact, which a successful COM run
  makes false. The filter is deliberately narrow so no other honest fact or gap is lost.
- **`DelegatedExtractionContext`** — a private `IExtractionContext` that reuses the outer context but
  substitutes the render-suppressed options and the composing sink, so the managed backend can run against
  the same format, environment, and cancellation token.
- **`VisioComDispatch`** — the low-level IDispatch plumbing: activation, property and method invocation,
  and wrapper release. Confining it here keeps `VisioAutomation` readable as a session script.
- **`NamespaceDoc`** — the namespace documentation type for the `Com` namespace.
