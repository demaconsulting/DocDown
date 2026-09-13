# DocDown.Visio System Design

![DocDown.Visio Structure](DocDownVisioView.svg)

`DocDown.Visio` is the Visio extraction system for the DocDown output contract. It reads a drawing's
page names, the text of every shape that carries text, and — above all — the directed connections
between shapes resolved into a readable topology, along with its embedded images and metadata, and —
when Microsoft Visio is available — a rendered image of each page, writing them all through the
`IExtractionSink` the engine supplies. It is a separately distributed NuGet package that a host registers
explicitly alongside `DocDown.Core`, and it ships two backends: a managed Open Packaging backend that
serves every `.vsdx` and `.vsdm` as the guaranteed content path, and a COM automation backend that adds
a rendered image of each page where Visio is installed. Legacy binary `.vsd` drawings are not supported —
by this package or by any other — and a request to extract one is refused plainly rather than routed
somewhere that cannot deliver.

A Visio drawing is an engineering schematic. Its meaning is carried by which shapes exist, what they are
labeled, which page they belong to, and — above all — how they are connected to one another. A list of
disconnected strings is not a schematic. The whole design follows from that: the page names, the shape
text, and the directed source-to-target topology are always extracted from the file itself, with no Visio
installation present, so the schematic's logical content is recoverable on any machine and in CI. Spatial
arrangement — layout, grouping, relative position — is itself engineering meaning and cannot be fully
recovered from the file's XML, so a rendered image of each page is added on top when an environment can
supply it. Rendering is the richer outcome and content extraction is the guaranteed one; the two are not
alternatives that hide each other.

## Architecture

The system has three subsystems and one direct unit:

- **VisioDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddVisio` registers both
  backends. Reflection-free and free of any COM type, so a host can reference the surface without those
  types entering its own compilation.
- **OpenXml** *(subsystem)* is the guaranteed content backend: the extractor the engine selects for a
  content extraction, the reader that turns a Visio Open Packaging container into the shared model — page
  names, shape text, master classification, and the directed connector topology — the image reader that
  yields each embedded image's bytes and page association, and `VisioExtractionException`, the one
  exception type this package translates itself. Fully managed, no native asset, because
  `DocumentFormat.OpenXml` has zero Visio types and the reader uses `System.IO.Packaging` directly.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that walks the drawing model
  through the sink — one section per page carrying its name, shape text, and directed topology — plus the
  inline images, the diagnostics, and the honest gaps, and the shape labeler that decides how each
  topology endpoint is named. Every mapping decision that differentiates a Visio extraction lives here
  and is testable from a hand-built model with no drawing behind it.
- **Com** *(subsystem)* is the rendering seam, present only where Microsoft Visio is installed: the
  full-superset extractor that reuses the managed backend's content extraction and adds a rendered image
  of each page, the availability probe that keeps that environment-dependent backend honest, and the real
  automation adapter that is the single untestable COM boundary.

### Why there are two backends

The engine selects exactly one backend, so a rendering backend that advertised only `RenderedPages` would
lose selection to the managed backend and never render. The COM backend therefore declares the full
capability superset and delivers all of it — but rather than re-implement the topology extraction, it
runs the managed backend against the same sink with rendering suppressed and adds only the rendered pages
the managed backend cannot. The managed backend has the higher priority, so it is the chosen reader for
every extraction that does not request rendered pages; the COM backend is chosen only when rendering is
requested and Visio is present, and otherwise probes unavailable and steps aside. This keeps the
guaranteed content path — the topology, the engineering content — independent of the environment while
the render is added wherever an environment can supply it.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddVisio` is the surface |
| Source document | Inbound | `.vsdx` / `.vsdm` byte stream | Not guaranteed seekable |
| Microsoft Visio | Outbound, from the COM adapter | Late-bound IDispatch | Windows-only; environment-dependent |
| `VisioExtractionException` | Outbound | .NET exception type | Carried out by Core as a structured failure message |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by two backends. `VisioOpenXmlExtractor` (Id `visio-openxml`,
  Priority 10) probes unconditionally, because nothing about it is environment-dependent.
  `VisioComExtractor` (Id `visio-com`, Priority 0) probes the operating system and the Visio ProgID. Both
  probes must be well under 50 ms, side-effect free, must not open the document, and must not throw.
- **`ISelfValidating`** enumeration is cheap; work happens only when a case's delegate is invoked. The
  managed backend contributes a topology round-trip case and a reasoned-skip page-rendering case; the COM
  backend contributes an availability case that skips cleanly off Windows.
- **`IExtractionSink`** is the only output channel. Every byte and every report goes through it, and no
  filesystem path for output is ever constructed in this package. The COM backend does materialize the
  source drawing to a temporary file, because Microsoft Visio opens a path rather than a stream, and it
  deletes that temporary file on every path.
- **`DocDownBuilder`** is extended by one method, `AddVisio`; there is no other way to register this
  package's backends.
- **The source document** is opened through `DocumentSource.OpenRead` and is not guaranteed seekable for a
  stream source, so both backends buffer the whole drawing into memory before opening the package, because
  the package reader must seek.
- **Microsoft Visio** is reached only by the COM adapter, only on Windows, and only when the availability
  probe has already confirmed it is registered.
- **`VisioExtractionException`** is the only exception this package translates itself: a package that is
  not a valid Open Packaging container, a drawing with no pages part, or a COM automation failure produces
  a clear message, which Core carries into an `ExtractorFailed` failure with the full layout still
  written.

No COM type appears on any of these public surfaces. That containment is what keeps
`VisioDocDownBuilderExtensions` usable by a host that has not itself referenced any interop assembly.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout. See the
  *DocDown.Core System Design*.
- **System.IO.Packaging** (OTS) — the OPC (Zip) container reader the managed backend uses directly,
  because `DocumentFormat.OpenXml` has no Visio types. It opens the `.vsdx`/`.vsdm` package and resolves
  its parts and relationships. See *System.IO.Packaging* under the OTS integration design; its evidence
  reaches this package transitively through the Visio extraction tests.
- **Microsoft Visio** (installed application, optional, Windows-only) — reached by the COM backend over
  late-bound IDispatch to render pages. It is not a build or restore dependency and is never required; its
  absence is a counted-honesty concern handled by the availability probe and the rendering-unavailable
  gap, not a failure. It is deliberately not modeled as an OTS item, because it is an end-user application
  discovered at runtime rather than a package this repository restores or ships.

There is no dependency of any kind on a native library. The package's build output contains no
`runtimes/` folder and no native `.dll`, `.so`, or `.dylib`; the COM backend reaches Visio through late
binding with no interop assembly.

## Risk Control Measures

- **COM containment.** COM plumbing appears only in the `Com` subsystem; no COM type appears in a public
  signature. A host referencing `VisioDocDownBuilderExtensions` never pulls those types into its
  compilation.
- **One reader, one emitter.** The reader populates a `VisioDocumentModel` and hands it to
  `VisioContentEmitter`, so every mapping decision is made once, in a place testable from a hand-built
  model with no drawing behind it. The COM backend reuses that same emitter through delegation, so a
  drawing's content reads identically whether or not it was rendered.
- **The topology is the guaranteed content.** The reader resolves every connector into a real directed
  edge with no Visio present, so the engineering content — the wiring — is recovered on any machine and in
  CI. An endpoint is labeled from what the drawing actually says about it (the shape's text, else its
  master type, else the bare shape id) and never invented, and the labeling convention and the honest
  endpoint coverage are published so a reader can tell an authored name from a classification.
- **The single untestable boundary is isolated.** Everything the COM backend does apart from talking to
  Visio — content delegation, availability, the gap and diagnostic policy, per-page fault isolation, and
  outcome mapping — is exercised cross-platform by injecting a stub `IVisioAutomation`. The real adapter
  is the one boundary CI cannot reach; its correctness in a deployed environment is proven by release-time
  self-tests.
- **Rendering degrades honestly.** When rendering is requested but Visio is absent, the COM backend probes
  unavailable and steps aside, the managed backend delivers the topology, and the render request becomes a
  counted `pages` gap naming the reason rather than a silent omission. A page that cannot be exported when
  Visio is present becomes a counted, named gap while the remaining pages still render.
- **Failure containment.** The reader wraps a package it cannot open or a missing pages part in a plain
  `VisioExtractionException`; every other adverse condition propagates to Core, which converts it into a
  coded, structured `ExtractorFailed` failure that still writes the full output layout — so a malformed
  drawing in a batch run cannot abort the run or produce a partial layout.
- **Count-before-decide accounting.** Every page, image, and rendered page is counted before any gap
  decision is taken, so the ledger's denominator cannot be reduced by the same code path that failed to
  deliver content. An image beyond a caller limit is a counted size-skip; a render that lost pages names
  them.

## Data Flow

1. The engine selects a backend for a document detected as `.vsdx` or `.vsdm`. For a content extraction it
   selects the managed backend; when rendered pages are requested and Visio is present it selects the COM
   backend. It calls `ExtractAsync` with a context exposing the options, the sink, and a cancellation
   token — but no filesystem path for output.
2. The managed backend records two environment facts through the sink: `visio.backend` names what parsed
   this drawing (`System.IO.Packaging`), and `visio.pageRendering` states that this extractor does not
   provide rendering.
3. `VisioPackageReader.Read` produces a `VisioDocumentModel` — pages in document order, each with its
   name, its shapes and their text and master classification, and the directed connections resolved from
   the connector records — plus the drawing's deduplicated images (excluding the package thumbnail) and
   the drawing metadata.
4. `VisioContentEmitter.EmitAsync` writes every image first (so each page can link its pictures inline),
   writes one section per page carrying its name, shape text, and directed topology, reports the document
   info and metadata, publishes the topology-labeling convention and the endpoint coverage, and reports
   every diagnostic and gap the model implies: `VISIO0001` for an empty drawing, `VISIO0002` for an empty
   page, `VISIO0003` for a vector image, `VISIO0005`/`VISIO0006` for the topology-labeling convention and
   coverage.
5. When the COM backend runs, it delegates steps 2–4 to the managed backend against a composing sink,
   records the authoritative `pages.renderer` fact, materializes the drawing to a temporary path, and
   drives Microsoft Visio to export each foreground page to a PNG; a page that fails becomes a counted
   `pages` gap with a `VISIO0004` diagnostic.
6. Each backend returns `Degraded` when any gap was reported and `Succeeded` otherwise. Core finalizes the
   content, reconciles the ledger, and writes `summary.txt`, `manifest.json`, and `metadata.json`.

### Mapping decisions

The mapping decisions this system makes are worth stating together, because they are what makes a Visio
extraction faithful to the schematic:

- **The directed topology.** Each connector is resolved into a directed edge in the direction its begin
  and end records name, scoped to the page that owns it, and rendered as a readable
  `source -> target` edge list. This is the engineering content and the guaranteed outcome no environment
  can take away.
- **Endpoint labeling.** An endpoint is named by the shape's own text where it has any; failing that, by
  the master the shape was instantiated from — parenthesized and paired with the shape id so a type is
  never mistaken for a name — unless that master describes connective geometry, in which case the honest
  shape-id fallback is kept. *(This endpoint-label provenance exceeds the Visio intent, which asked only
  for the directed edges; it is covered because it is genuinely tested. See the developer report.)*
- **Informative shape text only.** A shape whose text is a bare callout number keys into a graphical
  legend and reads as equipment named "1", so it is dropped from the listing and the count of what was
  dropped is stated. Multi-line shape text — fittings lists, valve tables — is kept intact inside its list
  item and never truncated.
- **Rendered pages when available.** When Visio is present, each foreground page is rasterized to a PNG at
  the caller's resolution and added as a page; a page that cannot be exported is a counted, named gap, and
  a render request with no Visio present is a counted `pages` gap naming the reason.
- **Images.** Embedded images — followed from page and master image relationships, deduplicated by package
  part, and excluding the package thumbnail — are written through the sink and linked inline under the
  page that shows them, recording which page each belongs to and flagging master-only images as template
  furniture. EMF and WMF metafiles are written as-is with an informational readability caveat.

## Design Constraints

- **The managed backend is 100% managed, runtime-identifier agnostic, and ships no native asset.** This is
  what makes the guaranteed content path — the topology — deployable and recoverable anywhere the .NET
  runtime is, and it is consistent with the decision not to declare `RenderedPages` on the managed
  backend, which ships no renderer. Because `DocumentFormat.OpenXml` has zero Visio types, the reader uses
  `System.IO.Packaging` directly.
- **Rendering is environment-dependent and Windows-only.** The COM backend reaches Microsoft Visio over
  late-bound IDispatch, so it runs only on Windows and only where Visio is registered; off Windows or
  without Visio it probes unavailable with a declarative reason and steps aside, and the managed backend
  still delivers the topology. The rendered appearance is captured whenever an environment can supply it
  and never faked when it cannot; when it is requested and cannot be supplied, the absence is a counted
  gap.
- **The whole drawing is buffered into memory.** A very large drawing is therefore bounded by available
  memory. This is a documented characteristic of the design, not an enforced limit — the reader neither
  streams nor imposes a size cap.
- **The symbol-font recovery table is deliberately minimal.** Only a code point whose symbol-font
  provenance and Unicode equivalent are both documented is recovered, and only within a run whose font
  declares that symbol font, because a wrong mapping is worse than a faithful oddity and a blind
  replacement would corrupt genuine text.
- **The legacy binary format is detected but never extracted.** Core recognizes `.vsd` deliberately,
  because "this is a `.vsd`, and DocDown does not support legacy binary formats" is a far better answer
  than "unrecognized format". No DocDown package extracts one, so the refusal names the format and states
  that fact declaratively rather than pointing at a package, an installation, or an environment that could
  deliver the capability — because none ever will.
