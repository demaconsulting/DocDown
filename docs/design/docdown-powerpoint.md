# DocDown.PowerPoint System Design

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

`DocDown.PowerPoint` is the PowerPoint extraction system for the DocDown output contract. It reads a
deck's slide text, the slide titles, the speaker notes that never appear in any render, the slide order,
and its embedded images and metadata, and — when Microsoft PowerPoint is available — a rendered image of
each slide, writing them all through the `IExtractionSink` the engine supplies. It is a separately
distributed NuGet package that a host registers explicitly alongside `DocDown.Core`, and it ships two
backends: a managed Open XML SDK backend that serves every `.pptx` as the guaranteed content path, and a
COM automation backend that adds a rendered image of each slide where PowerPoint is installed. Legacy
binary `.ppt` decks are not supported — by this package or by any other — and a request to extract one is
refused plainly rather than routed somewhere that cannot deliver.

A slide is a visual composition. What a slide means is largely how it looks — arrangement, emphasis,
diagrams drawn from many shapes — so the rendered appearance of each slide is primary evidence and is
captured when PowerPoint is available. But a deck also carries content that never appears in any render:
speaker notes hold the narration, the caveats, and the argument the slide only gestures at, and they are
invisible to any renderer. The whole design follows from that: the text of every slide, the slide title,
the speaker notes, and the slide order are always extracted from the file itself, independent of whether
rendering is possible, and the render is added on top when an environment can supply it. Neither half
substitutes for the other — rendering without notes loses the narration, notes without rendering loses
the composition.

## Architecture

The system has three subsystems and one direct unit:

- **PowerPointDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddPowerPoint`
  registers both backends. Reflection-free and free of any Open XML SDK or COM type, so a host can
  reference the surface without those types entering its own compilation.
- **OpenXml** *(subsystem)* is the guaranteed content backend: the extractor the engine selects for a
  content extraction, the reader that turns a `PresentationDocument` into the shared model — slide order,
  titles, body text, and speaker notes — the image reader that yields each embedded image's bytes and
  slide association, and `PowerPointExtractionException`, the one exception type this package translates
  itself. Fully managed, no native asset.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that walks the deck model
  through the sink — one section per slide carrying its title, text, and notes — plus the inline images,
  the content outline, the diagnostics, and the honest gaps. Every mapping decision that differentiates a
  PowerPoint extraction lives here and is testable from a hand-built model with no deck behind it.
- **Com** *(subsystem)* is the rendering seam, present only where Microsoft PowerPoint is installed: the
  full-superset extractor that reuses the managed backend's content extraction and adds a rendered image
  of each slide, the availability probe that keeps that environment-dependent backend honest, and the
  real automation adapter that is the single untestable COM boundary.

### Why there are two backends

The engine selects exactly one backend, so a rendering backend that advertised only `RenderedPages` would
lose selection to the managed backend and never render. The COM backend therefore declares the full
capability superset and delivers all of it — but rather than re-implement text and notes extraction, it
runs the managed backend against the same sink with rendering suppressed and adds only the rendered
slides the managed backend cannot. The managed backend has the higher priority, so it is the chosen
reader for every extraction that does not request rendered pages; the COM backend is chosen only when
rendering is requested and PowerPoint is present, and otherwise probes unavailable and steps aside. This
keeps the guaranteed content path independent of the environment while the render is added wherever an
environment can supply it.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPowerPoint` is the surface |
| Source document | Inbound | `.pptx` byte stream | Not guaranteed seekable |
| Microsoft PowerPoint | Outbound, from the COM adapter | Late-bound IDispatch | Windows-only; environment-dependent |
| `PowerPointExtractionException` | Outbound | .NET exception type | Carried out by Core as a structured failure |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by two backends. `PowerPointOpenXmlExtractor` (Id
  `powerpoint-openxml`, Priority 10) probes unconditionally, because nothing about it is
  environment-dependent. `PowerPointComExtractor` (Id `powerpoint-com`, Priority 0) probes the operating
  system and the PowerPoint ProgID. Both probes must be well under 50 ms, side-effect free, must not open
  the document, and must not throw.
- **`ISelfValidating`** enumeration is cheap; work happens only when a case's delegate is invoked. The
  managed backend contributes a parse round-trip case and a reasoned-skip page-rendering case; the COM
  backend contributes an availability case that skips cleanly off Windows.
- **`IExtractionSink`** is the only output channel. Every byte and every report goes through it, and no
  filesystem path for output is ever constructed in this package. The COM backend does materialize the
  source deck to a temporary file, because Microsoft PowerPoint opens a path rather than a stream, and it
  deletes that temporary file on every path.
- **`DocDownBuilder`** is extended by one method, `AddPowerPoint`; there is no other way to register this
  package's backends.
- **The source document** is opened through `DocumentSource.OpenRead` and is not guaranteed seekable for a
  stream source, so both backends buffer the whole deck into memory before opening the package, because
  the package reader must seek.
- **Microsoft PowerPoint** is reached only by the COM adapter, only on Windows, and only when the
  availability probe has already confirmed it is registered. The whole session is watchdog-bounded so a
  modal-dialog hang cannot stall the run.
- **`PowerPointExtractionException`** is the only exception this package translates itself: a package the
  Open XML SDK cannot open (encrypted, malformed), one with no presentation part, or a COM automation
  failure produces a clear message, which Core carries into an `ExtractorFailed` failure with the full
  layout still written.

No Open XML SDK type and no COM type appears on any of these public surfaces. That containment is what
keeps `PowerPointDocDownBuilderExtensions` usable by a host that has not itself referenced the SDK.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout. See the
  *DocDown.Core System Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the content backend is built on, pinned to
  the exact version range for restore determinism and SBOM reproducibility. See the
  *DocumentFormat.OpenXml* OTS design; its evidence reaches this package transitively through the
  PowerPoint extraction tests.
- **System.IO.Packaging** (OTS, transitive) — the OPC (Zip) container reader underneath the SDK, which
  opens the `.pptx` package and resolves its parts and relationships. See *System.IO.Packaging* under the
  OTS integration design.
- **Microsoft PowerPoint** (installed application, optional, Windows-only) — reached by the COM backend
  over late-bound IDispatch to render slides. It is not a build or restore dependency and is never
  required; its absence is a counted-honesty concern handled by the availability probe, not a failure.
  It is deliberately not modeled as an OTS item, because it is an end-user application discovered at
  runtime rather than a package this repository restores or ships.

There is no dependency of any kind on a native library. The package's build output contains no
`runtimes/` folder and no native `.dll`, `.so`, or `.dylib`; the COM backend reaches PowerPoint through
late binding with no interop assembly.

## Risk Control Measures

- **SDK and COM containment.** `DocumentFormat.OpenXml` types appear only in the `OpenXml` subsystem, and
  COM plumbing only in the `Com` subsystem; no SDK or COM type appears in a public signature. A host
  referencing `PowerPointDocDownBuilderExtensions` never pulls those types into its compilation.
- **One reader, one emitter.** The reader populates a `PowerPointDeckModel` and hands it to
  `PowerPointContentEmitter`, so every mapping decision is made once, in a place testable from a
  hand-built model with no deck behind it. The COM backend reuses that same emitter through delegation, so
  a deck's content reads identically whether or not it was rendered.
- **Notes are always read.** The reader reads the notes slide's body placeholder on every extraction, so
  the narration no render can supply is recovered from the file itself; a deck that carries no notes is
  reported so a reader can tell a notes-less deck from one whose notes were missed. *(See the finding in
  the developer report: the intent requires this whole-deck absence to be a counted gap, but the shipped
  code reports it as an informational `PPTX0002` diagnostic. The requirement is written to the intent.)*
- **The single untestable boundary is isolated.** Everything the COM backend does apart from talking to
  PowerPoint — content delegation, availability, the gap and diagnostic policy, per-slide fault
  isolation, and outcome mapping — is exercised cross-platform by injecting a stub `IPowerPointAutomation`.
  The real adapter is the one boundary CI cannot reach; its correctness in a deployed environment is
  proven by release-time self-tests.
- **Watchdog-bounded rendering.** A whole PowerPoint session runs inside one watchdog-guarded call on a
  dedicated single-threaded-apartment thread, so no COM object outlives the timeout and a modal-dialog
  hang is bounded by force-terminating the owned process, leaving no orphan. A slide that cannot be
  exported becomes a counted, named gap while the remaining slides still render.
- **Failure containment.** The reader wraps a package it cannot open or a missing presentation part in a
  plain `PowerPointExtractionException`; every other adverse condition propagates to Core, which converts
  it into a coded, structured `ExtractorFailed` failure that still writes the full output layout — so an
  encrypted or malformed deck in a batch run cannot abort the run or produce a partial layout.
- **Count-before-decide accounting.** Every slide, image, and chart is counted before any gap decision is
  taken, so the ledger's denominator cannot be reduced by the same code path that failed to deliver
  content. Charts the backend does not read are counted and named; a render that lost slides names them.

## Data Flow

1. The engine selects a backend for a document detected as `.pptx`. For a content extraction it selects
   the managed backend; when rendered pages are requested and PowerPoint is present it selects the COM
   backend. It calls `ExtractAsync` with a context exposing the options, the sink, and a cancellation
   token — but no filesystem path for output.
2. The managed backend records two environment facts through the sink: `powerpoint.backend` names what
   parsed this deck (the Open XML SDK), and `powerpoint.pageRendering` states that this extractor does not
   provide rendering.
3. `PowerPointOpenXmlReader.Read` produces a `PowerPointDeckModel` — slides in presentation order, each
   with its title, body text lines, speaker notes, and inline image references, plus the deck's
   deduplicated images, the deck metadata, and the count of charts the deck embeds.
4. `PowerPointContentEmitter.EmitAsync` writes every image first (so each slide can link its pictures
   inline), writes one section per slide carrying its title, text, and notes, reports the document info
   and metadata, and reports every diagnostic and gap the model implies: `PPTX0001` for an empty deck,
   `PPTX0002` for a deck with no speaker notes, `PPTX0003` for a vector image, `PPTX0005` for charts this
   backend does not read.
5. When the COM backend runs, it delegates steps 2–4 to the managed backend against a composing sink,
   records the authoritative `pages.renderer` fact, materializes the deck to a temporary path, and drives
   Microsoft PowerPoint to export each slide to a PNG; a slide that fails becomes a counted `pages` gap
   with a `PPTX0004` diagnostic.
6. Each backend returns `Degraded` when any gap was reported and `Succeeded` otherwise. Core finalizes the
   content, reconciles the ledger, and writes `summary.txt`, `manifest.json`, and `metadata.json`.

### Mapping decisions

The mapping decisions this system makes are worth stating together, because they are what makes a
PowerPoint extraction faithful to both halves of a deck:

- **Always-extracted content.** Every slide becomes a section carrying its title as a heading, its body
  text, and its speaker notes marked as notes, in presentation order. This content is produced with no
  rendering and is the guaranteed outcome no environment can take away.
- **Speaker-notes accounting.** Notes are read from every slide's notes-slide body placeholder; a deck
  that carries no notes has that whole-deck absence reported so a reader can tell it from notes that were
  never looked for.
- **Rendered pages when available.** When PowerPoint is present, each slide is rasterized to a PNG at the
  caller's resolution and added as a page; a slide that cannot be exported is a counted, named gap.
- **Charts as a stated absence.** A chart on a slide stores its plotted data in a part carrying neither
  text nor an image blip, and this backend does not read it, so the charts are counted and named as a gap
  with a remedy pointing at the workbook route, rather than dropped in silence.
- **Images.** Embedded images — gathered from slides, notes, layouts, and masters — are written through
  the sink and linked inline under the slide that shows them, recording which slide each belongs to. EMF
  and WMF metafiles are written as-is with an informational readability caveat.

## Design Constraints

- **The managed backend is 100% managed, runtime-identifier agnostic, and ships no native asset.** This
  is what makes the guaranteed content path deployable anywhere the .NET runtime is, and it is consistent
  with the decision not to declare `RenderedPages` on the managed backend, which ships no renderer.
- **Rendering is environment-dependent and Windows-only.** The COM backend reaches Microsoft PowerPoint
  over late-bound IDispatch, so it runs only on Windows and only where PowerPoint is registered; off
  Windows or without PowerPoint it probes unavailable with a declarative reason and steps aside, and the
  managed backend still delivers the content. The rendered appearance is captured whenever an environment
  can supply it and never faked when it cannot.
- **The whole deck is buffered into memory, and the SDK builds an in-memory object model.** A very large
  deck is therefore bounded by available memory. This is a documented characteristic of the design, not an
  enforced limit — the reader neither streams nor imposes a size cap.
- **`DocumentFormat.OpenXml` is pinned to an exact version range.** A floating reference would let a
  restore substitute a different patch, changing the resolved dependency set recorded in the generated
  SBOM and breaking build reproducibility. This is a supply-chain reproducibility pin, not an
  API-fragility pin. See *DocumentFormat.OpenXml* under the OTS integration design.
- **The legacy binary formats are detected but never extracted.** Core recognizes `.ppt` deliberately,
  because "this is a `.ppt`, and DocDown does not support legacy binary formats" is a far better answer
  than "unrecognized format". No DocDown package extracts one, so the refusal names the format and states
  that fact declaratively rather than pointing at a package, an installation, or an environment that could
  deliver the capability.
