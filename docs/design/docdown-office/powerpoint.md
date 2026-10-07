# DocDown.PowerPoint System Design

![DocDown.PowerPoint Structure](PowerPointView.svg)

`DocDown.PowerPoint` is the PowerPoint extraction system for the DocDown output contract. It reads a
modern `.pptx` deck's slide text, slide titles, speaker notes, slide order, embedded images, and deck
metadata from the file itself. Rendered slide images are a separate, opt-in concern provided by the
`DocDown.PowerPoint.Rendering` package; this system's own backend always states that it provides no
rendered pages. Reporting follows one rule throughout the system: DocDown reports what it extracted
and where it wrote it. The content inventory states what was looked for, including counted zeros, and
a short plain note is used only when DocDown attempted a step and could not complete it. Legacy
binary `.ppt` decks are not extracted and complete as `Unreadable` with a plain explanation.

## Architecture

The system has two subsystems and one direct unit:

- **PowerPointDocDownBuilderExtensions** *(direct unit)* is the registration seam: `AddPowerPoint`
  registers the managed PowerPoint backend explicitly. It stays free of deck I/O and eager environment
  probing, so host configuration remains a visible code decision.
- **OpenXml** *(subsystem)* is the managed content backend: the extractor the engine selects for a
  normal `.pptx` extraction, the reader that turns a `PresentationDocument` into the shared deck
  model, the image reader that resolves embedded image bytes and slide association, and
  `PowerPointExtractionException`, the one package-specific exception type this system raises.
- **Markdown** *(subsystem)* is the reader-neutral projection: the emitter that writes slide content,
  image links, document information, metadata, the content inventory, and plain notes about attempted
  steps that could not be completed. Every PowerPoint-specific mapping decision lives here.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap probe; no throw; no side effects |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Cheap enumeration; work runs in delegates |
| `IExtractionSink` | Outbound, to Core | .NET interface | Only output channel for content, pages, facts, notes |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPowerPoint` is the registration surface |
| Source document | Inbound | `.pptx` byte stream | Stream sources are not guaranteed seekable |

The constraints each interface carries are:

- **`IDocumentExtractor`** is implemented by `PowerPointOpenXmlExtractor`, which probes with
  `Available()` because nothing about it is environment-dependent.
- **`ISelfValidating`** enumeration is cheap; the managed backend contributes a parse round-trip case
  and an honest skipped rendering case.
- **`IExtractionSink`** is the only output channel. Every byte, inventory item, environment fact, and
  note goes through it. No output path is constructed in this package.
- **`DocDownBuilder`** is extended by one method, `AddPowerPoint`; there is no other registration
  surface for this package's backend.
- **The source document** is opened through `DocumentSource.OpenRead`. The backend buffers the deck
  into memory before opening it because the package reader must seek.

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

There is no dependency on a native library or an installed application. The managed backend ships no
native renderer and no native interop assembly.

## Risk Control Measures

- **One reader, one emitter.** `PowerPointOpenXmlReader` populates a `PowerPointDeckModel` and
  `PowerPointContentEmitter` writes everything that model implies, so a deck's content is not
  reimplemented in two places.
- **Speaker notes are always read from the file.** The Open XML reader reads the notes body's text for
  each slide, so the narration that never appears in a slide image is recovered directly from the deck.
- **Reviewer comments are not content, and only the legacy grammar is read.** A slide comment is
  commentary *about* the deck rather than part of it, so it is never written into the content flow;
  the emitter reports it to Core as a `DocumentComment` located `Slide {n}` and Core writes the
  dedicated `review-comments.md` artifact. Authors are resolved through the presentation's
  comment-author list. **The modern persona-based, cloud-synced comments that current PowerPoint
  writes are a stated scope limitation: they are deliberately not read. This is a scope choice, not
  a tooling limit — `DocumentFormat.OpenXml 3.5.1` does expose the part and its grammar; what could
  not be validated here is the modern author model, and attributing a reviewer's words on unverified
  reasoning is a worse outcome than stating the boundary.** A deck whose comments are all modern is
  not silently reported as having no comments at all: the reader counts those comments and the
  emitter states the unread count as a note. See *PowerPointOpenXmlReader Design* for the full
  statement of that boundary.
- **Two reporting forms only.** The system uses content inventory for what was looked for and found,
  including counted zeros, and plain notes only when an attempted step could not be completed. It does
  not classify ordinary document content as acceptable or unacceptable.
- **Failure containment is outcome-based.** Normal completion returns `Produced`. A document that
  cannot be read becomes `Unreadable` with a plain explanation.

## Data Flow

1. The engine detects a modern PowerPoint deck and selects the managed backend.
2. The managed backend reports two environment facts through the sink: which backend read the deck,
   and that rendered pages are not provided by that extractor. It buffers the source and calls
   `PowerPointOpenXmlReader.Read`.
3. `PowerPointOpenXmlReader.Read` produces a `PowerPointDeckModel`: slides in presentation order,
   each with its title, body text lines, speaker notes, inline image references, and legacy reviewer
   comments, plus deduplicated
   embedded images and document metadata.
4. `PowerPointContentEmitter.EmitAsync` writes embedded images, writes the deck content, reports
   document information and metadata, reports each slide comment to Core as a review comment, and
   reports the content inventory. A deck with no slides becomes
   an empty content file plus zero-count inventory. Image steps that exceed a caller size limit
   become short plain notes.
5. The backend returns `Produced` when the extraction completes. If no backend can read the detected
   format, or if a deck cannot be opened as a valid modern PowerPoint package, Core writes the standard
   layout and completes the result as `Unreadable`.

## Design Constraints

- **The managed backend is fully managed.** This is what makes the guaranteed content path deployable
  anywhere the .NET runtime is available. It can read `.pptx` content everywhere but it does not render
  slide pages itself; rendered slide images are a separate, opt-in concern provided by the
  `DocDown.PowerPoint.Rendering` package.
- **The whole deck is buffered into memory.** A stream source is not guaranteed seekable, and the SDK
  package reader must seek, so the backend buffers the document before opening it.
- **Legacy binary `.ppt` is detected but not extracted.** The system gives a plain `Unreadable`
  explanation rather than an "unknown format" answer, and it does not point at another package or
  environment because no DocDown PowerPoint backend reads that format.
