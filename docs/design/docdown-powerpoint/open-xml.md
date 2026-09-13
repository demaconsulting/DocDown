## OpenXml Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The OpenXml subsystem is the managed PowerPoint extraction backend and the guaranteed content path. It
reads a `.pptx` package through the Open XML SDK, populates the shared `PowerPointDeckModel` — slides in
presentation order, each with its title, body text, and speaker notes — and hands the model to
`PowerPointContentEmitter`, which routes every byte through the sink. The subsystem is fully managed,
carries no native asset, and is deployable anywhere the .NET runtime is; it is the reason
`DocDown.PowerPoint` can guarantee a deck's content is recoverable on any machine and in CI, whether or
not a render is possible.

The subsystem exists because several responsibilities are cleanly separable: the descriptor the engine
selects and probes (`PowerPointOpenXmlExtractor`), the SDK-to-model translation of slide order, titles,
body text, and speaker notes (`PowerPointOpenXmlReader`), and the image reader factored out of it because
resolving a deck's embedded images across slides, notes, layouts, and masters is a distinct responsibility
(`PowerPointOpenXmlImageReader`).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability` under 50 ms, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream may not seek |
| `IExtractionSink` | Outbound, via `PowerPointContentEmitter` | .NET interface | The only output channel |
| `PowerPointDeckModel` | Outbound, from the reader | .NET record | The pivot between reading and rendering |
| `PresentationDocument` | Internal | Open XML SDK | Opened read-only |

### Design

**The descriptor.** `PowerPointOpenXmlExtractor` declares `Id = "powerpoint-openxml"`, `DisplayName =
"PowerPoint (Open XML SDK)"`, `SupportedFormats = [Pptx]`, `Priority = 10`, and `Capabilities = Text |
EmbeddedImages | DocumentMetadata | DocumentStructure`. `RenderedPages` is pointedly absent, because this
backend ships no renderer; rendering is delivered by the COM backend. `ProbeAvailability` returns
`Available(Capabilities)` unconditionally, with no I/O: there is nothing to probe because the SDK is a
managed assembly shipped inside this package.

**The self-test set.** Two cases: a `powerpoint.openxml.parseRoundTrip` case that builds a one-slide deck
in memory with `PresentationDocument.Create`, reads it back with the reader, and passes when the deck
carries at least one slide; and a `powerpoint.pageRendering` case that reports a reasoned skip because the
managed backend does not provide rendering. Building rather than embedding a fixture keeps the case free
of a shipped binary payload and exercises the writer and reader together.

**The reader.** `PowerPointOpenXmlReader` opens the package read-only and walks the presentation's
slide-id list — never the package parts, which are unordered — so slides come out in presentation order
with their 1-based ordinals. For each slide it separates the title placeholder from the body shapes,
joins each paragraph's text runs into one line, and reads the notes slide's body placeholder so the
narration is recovered from the file itself, dropping the notes-slide furniture. It resolves the embedded
images through `PowerPointOpenXmlImageReader`, maps the OPC core properties to the deck metadata, and
counts the DrawingML chart parts the slides and their notes embed so the emitter can report them. It wraps
a package it cannot open or a missing presentation part in a plain `PowerPointExtractionException` so the
message reaching the caller is the backend's own explanation rather than the SDK's raw package error.

**The image reader.** `PowerPointOpenXmlImageReader` gathers image parts from slides (with their notes)
in presentation order, then from the slide masters and their layouts, and deduplicates by package-part
identity so a media part shared across many slides is yielded once. It records every slide that references
an image in that image's referrer set, exposes each slide's ordered image occurrences for inline linking,
and flags an image reached only through a layout or master as template furniture rather than fabricating a
slide — so a reader can tell "on slides 3 and 7" from "template furniture" from "true orphan". A picture
that carries both a raster fallback and a scalable vector graphic yields both.

**Adverse cases.** The reader translates only the conditions it can recognize better than the SDK — a package
it cannot open and a missing presentation part — into `PowerPointExtractionException`. Every other
fault propagates to Core, which converts it into an `ExtractorFailed` failure with the full layout still
written. Translating everything locally would duplicate that machinery and discard the SDK's own
explanation of what was wrong.

**Self-containment.** No SDK type reaches the public surface: `PowerPointOpenXmlExtractor` is public but
its public members (from `IDocumentExtractor` and `ISelfValidating`) name Core types only; the reader and
the image reader are internal, exposed to the test project through `InternalsVisibleTo`. This subsystem's
inline supporting-type source files — reviewed in its review-set rather than in unit docs — are the model
records `PowerPointDocumentModel` defines (`PowerPointDeckModel`, `PowerPointSlideModel`,
`PowerPointSlideImageRef`, and the image collection the reader returns) and `PowerPointExtractionException`,
the shared exception Core surfaces, which the COM adapter also raises. The `Text`-projection supporting
type — `PowerPointDiagnosticCodes` — belongs to the Markdown subsystem, where the output codes are
documented.
