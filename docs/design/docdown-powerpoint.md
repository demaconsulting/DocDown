# DocDown.PowerPoint System Design

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

`DocDown.PowerPoint` is the PowerPoint extraction system for the DocDown output contract. It reads a
modern `.pptx` deck's slide text, slide titles, speaker notes, slide order, embedded images, and deck
metadata from the file itself. When Microsoft PowerPoint is available, the COM backend also renders
each slide to a page image. Reporting follows one rule throughout the system: DocDown reports what it
extracted and where it wrote it. The content inventory states what was looked for, including counted
zeros, and a short plain note is used only when DocDown attempted a step and could not complete it.
Legacy binary `.ppt` decks are not extracted and complete as `Unreadable` with a plain explanation.

## Architecture

The system has three subsystems and one direct unit:

- **PowerPointDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddPowerPoint`
  registers both PowerPoint backends explicitly. It stays free of deck I/O and eager environment
  probing, so host configuration remains a visible code decision.
- **OpenXml** *(subsystem)* is the managed content backend: the extractor the engine selects for a
  normal `.pptx` extraction, the reader that turns a `PresentationDocument` into the shared deck
  model, the image reader that resolves embedded image bytes and slide association, and
  `PowerPointExtractionException`, the one package-specific exception type this system raises.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that writes slide content,
  image links, document information, metadata, the content inventory, and plain notes about attempted
  steps that could not be completed. Every PowerPoint-specific mapping decision lives here.
- **Com** *(subsystem)* is the rendering seam, active only where Microsoft PowerPoint is present: the
  extractor that reuses the managed content path and adds rendered slide pages, the availability
  probe, and the real automation adapter that is the single untestable COM boundary.

### Why there are two backends

The engine selects exactly one backend for an extraction. The managed backend therefore owns the
guaranteed content path for every `.pptx`: it always reads slide text, titles, notes, images, and
metadata from the file itself. The COM backend is not a second content implementation; it delegates
that same content work to the managed backend and adds only rendered slide pages. That keeps the deck's
textual content identical whether rendering was possible or not, while still letting a caller request
rendered pages when the environment can supply them.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap probe; no throw; no side effects |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Cheap enumeration; work runs in delegates |
| `IExtractionSink` | Outbound, to Core | .NET interface | Only output channel for content, pages, facts, notes |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPowerPoint` is the registration surface |
| Source document | Inbound | `.pptx` byte stream | Stream sources are not guaranteed seekable |
| Microsoft PowerPoint | Outbound, from the COM adapter | Late-bound IDispatch | Windows-only; environment-dependent |

The constraints each interface carries are:

- **`IDocumentExtractor`** is implemented by two backends. `PowerPointOpenXmlExtractor` probes with
  `Available()` because nothing about it is environment-dependent. `PowerPointComExtractor` probes
  through `PowerPointComAvailability`, which returns `Available(providesRenderedPages: true)` only
  when Microsoft PowerPoint can be reached and otherwise returns `Unavailable(reason)`.
- **`ISelfValidating`** enumeration is cheap; the managed backend contributes a parse round-trip case
  and an honest skipped rendering case, and the COM backend contributes an availability case that
  skips cleanly where PowerPoint is not present.
- **`IExtractionSink`** is the only output channel. Every byte, inventory item, environment fact, and
  note goes through it. No output path is constructed in this package.
- **`DocDownBuilder`** is extended by one method, `AddPowerPoint`; there is no other registration
  surface for this package's backends.
- **The source document** is opened through `DocumentSource.OpenRead`. Both backends buffer the deck
  into memory before opening it because the package reader must seek.
- **Microsoft PowerPoint** is reached only by the COM adapter, only on Windows, and only when the
  availability probe has already confirmed that the application can be reached.

## Dependencies

- **DocDown.Core** — the extraction contract, sink, output layout, content inventory, environment
  facts, plain notes, and the `Produced` and `Unreadable` outcomes. See the *DocDown.Core System
  Design*.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK used to read `.pptx` packages. See the
  *DocumentFormat.OpenXml* OTS design; its correctness reaches this package through the PowerPoint
  extraction tests.
- **System.IO.Packaging** (OTS, transitive) — the OPC container reader under the SDK, which opens the
  package and resolves its parts and relationships. See *System.IO.Packaging* under the OTS
  integration design.
- **Microsoft PowerPoint** *(installed application, optional, Windows-only)* — reached by the COM
  backend over late-bound IDispatch to render slides. It is not a restore dependency and is never
  required for content extraction.

There is no dependency on a native library. The managed backend ships no native renderer and no native
interop assembly.

## Risk Control Measures

- **One reader, one emitter.** `PowerPointOpenXmlReader` populates a `PowerPointDeckModel` and
  `PowerPointContentEmitter` writes everything that model implies. The COM backend reuses that same
  emission path by delegation, so a deck's content is not reimplemented in two places.
- **Speaker notes are always read from the file.** The Open XML reader reads the notes body's text for
  each slide, so the narration that never appears in a slide image is recovered directly from the deck.
- **Two reporting forms only.** The system uses content inventory for what was looked for and found,
  including counted zeros, and plain notes only when an attempted step could not be completed. It does
  not classify ordinary document content as acceptable or unacceptable.
- **The single untestable boundary is isolated.** Everything the COM backend does apart from talking to
  Microsoft PowerPoint is exercised cross-platform through an injected `IPowerPointAutomation` stub.
  The real adapter is the one boundary proven by release-time self-tests rather than CI.
- **Failure containment is outcome-based.** Normal completion returns `Produced`. A document that
  cannot be read becomes `Unreadable` with a plain explanation. A partially rendered deck still
  returns `Produced`; the incomplete render step is stated as a note naming the slide.
- **Watchdog-bounded rendering.** A whole PowerPoint session runs inside one watchdog-guarded call on
  a dedicated single-threaded-apartment thread so a modal dialog cannot stall extraction indefinitely.

## Data Flow

1. The engine detects a modern PowerPoint deck and selects a backend. For a normal `.pptx`
   extraction, the managed backend is selected. When rendered pages are requested and Microsoft
   PowerPoint is available, the COM backend is selected.
2. The managed backend reports two environment facts through the sink: which backend read the deck,
   and that rendered pages are not provided by that extractor. It buffers the source and calls
   `PowerPointOpenXmlReader.Read`.
3. `PowerPointOpenXmlReader.Read` produces a `PowerPointDeckModel`: slides in presentation order,
   each with its title, body text lines, speaker notes, and inline image references, plus deduplicated
   embedded images and document metadata.
4. `PowerPointContentEmitter.EmitAsync` writes embedded images, writes the deck content, reports
   document information and metadata, and reports the content inventory. A deck with no slides becomes
   an empty content file plus zero-count inventory. Image steps that exceed a caller size limit or
   cannot honor `ForcePng` become short plain notes.
5. When the COM backend runs, it delegates steps 2-4 to the managed backend against a composing sink,
   records the authoritative `pages.renderer` fact, materializes the deck to a file path, and asks
   Microsoft PowerPoint to render each slide. A slide that cannot be rendered produces a plain note
   naming the slide and the renderer's reason when one is available.
6. The backend returns `Produced` when the extraction completes. If no backend can read the detected
   format, or if a deck cannot be opened as a valid modern PowerPoint package, Core writes the standard
   layout and completes the result as `Unreadable`.

## Design Constraints

- **The managed backend is fully managed.** This is what makes the guaranteed content path deployable
  anywhere the .NET runtime is available. It can read `.pptx` content everywhere but it does not render
  slide pages by itself.
- **Rendering is environment-dependent and Windows-only.** The COM backend reaches Microsoft
  PowerPoint over late-bound IDispatch, so it runs only where the application is available. The system
  states plainly when rendered pages are not available rather than fabricating them.
- **The whole deck is buffered into memory.** A stream source is not guaranteed seekable, and the SDK
  package reader must seek, so both backends buffer the document before opening it.
- **Legacy binary `.ppt` is detected but not extracted.** The system gives a plain `Unreadable`
  explanation rather than an "unknown format" answer, and it does not point at another package or
  environment because no DocDown PowerPoint backend reads that format.
