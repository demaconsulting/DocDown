## OpenXml Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The OpenXml subsystem is the managed PowerPoint extraction backend and the guaranteed content path. It
reads a `.pptx` package through the Open XML SDK, populates the shared `PowerPointDeckModel` — slides
in presentation order, each with its title, body text, speaker notes, and inline image references —
and hands the model to `PowerPointContentEmitter`, which routes every byte through the sink. The
subsystem is fully managed, carries no native asset, and is deployable anywhere the .NET runtime is.

The subsystem exists because several responsibilities are cleanly separable: the extractor the engine
selects and probes (`PowerPointOpenXmlExtractor`), the SDK-to-model translation of slide order, titles,
body text, and speaker notes (`PowerPointOpenXmlReader`), and the image reader factored out of it
because resolving a deck's embedded images across slides, notes, layouts, and masters is a distinct
responsibility (`PowerPointOpenXmlImageReader`).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability()` returns `Available()` |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream may not seek |
| `IExtractionSink` | Outbound, via `PowerPointContentEmitter` | .NET interface | The only output channel |
| `PowerPointDeckModel` | Outbound, from the reader | .NET record | The pivot between reading and reporting |
| `PresentationDocument` | Internal | Open XML SDK | Opened read-only |

### Design

**The extractor surface.** `PowerPointOpenXmlExtractor` declares `Id = "powerpoint-openxml"`,
`DisplayName = "PowerPoint (Open XML SDK)"`, `SupportedFormats = [Pptx]`, and `Priority = 10`. It
remains applicable to rendering requests at the descriptor level, but `ProbeAvailability()` returns
`Available()` with rendered-page support left false, because this backend does not itself render slide
pages. During extraction it reports `powerpoint.backend = Open XML SDK (managed)` and
`powerpoint.pageRendering = not provided by this extractor`.

**The self-test set.** Two cases are exposed: a `powerpoint.openxml.parseRoundTrip` case that builds a
one-slide deck in memory with `PresentationDocument.Create`, reads it back with the reader, and passes
when the deck carries at least one slide; and a `powerpoint.pageRendering` case that reports a reasoned
skip because the managed backend does not render slide images.

**The reader.** `PowerPointOpenXmlReader` opens the package read-only and walks the presentation's
slide-id list — never the package parts, which are unordered — so slides come out in presentation
order with their 1-based ordinals. For each slide it separates the title placeholder from the body
shapes, joins each paragraph's text runs into one line, reads the notes slide's body placeholder so
the narration is recovered from the file itself, resolves embedded images through
`PowerPointOpenXmlImageReader`, and maps the OPC core properties to deck metadata.

**The image reader.** `PowerPointOpenXmlImageReader` gathers image parts from slides and notes in
presentation order, then from the slide masters and layouts, and deduplicates by package-part
identity. It records every slide that references an image in that image's referrer set, exposes each
slide's ordered image occurrences for inline linking, and flags an image reached only through a layout
or master as template furniture rather than fabricating a slide association.

**Adverse cases.** The reader translates only the conditions it can recognize better than the SDK — a
package it cannot open and a missing presentation part — into `PowerPointExtractionException`. Every
other fault propagates to Core, which converts it into an `Unreadable` result with the standard output
layout still written.

**Self-containment.** No Open XML SDK type reaches the public surface: `PowerPointOpenXmlExtractor` is
public, but its public members name Core types only; the reader and image reader remain internal and
are exposed to the test project through `InternalsVisibleTo`. The subsystem's inline supporting types
are the deck-model records and `PowerPointExtractionException`.
