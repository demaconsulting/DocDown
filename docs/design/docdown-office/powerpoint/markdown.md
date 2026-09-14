## Markdown Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.PowerPoint`. It defines how the
backend-neutral `PowerPointDeckModel` becomes the output contract: the content emitter writes one
section per slide carrying its title, body text, and speaker notes; writes the deck's embedded images
through the sink; reports the content inventory; and records short plain notes when an attempted image
step could not be completed. Every mapping decision that differentiates a PowerPoint extraction lives
here and is testable from a hand-built model with no deck and no Open XML SDK behind it.

The subsystem exists because reading a deck and reporting what was read are separate concerns. Holding
the whole projection apart from the reader is what makes every mapping decision provable from a
hand-built model, and it is what lets the COM backend reuse the exact same emission by delegation, so
a deck's content reads identically whether or not it was rendered.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `PowerPointDeckModel` | Inbound, from the reader | .NET record | The pivot between reading and reporting |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from `IExtractionContext` | .NET record | Image suppression, image size limits, DPI |

### Design

**The deck model.** `PowerPointDeckModel` is the cross-reader contract: the slides in presentation
order, the deduplicated embedded images, and the deck metadata. Each `PowerPointSlideModel` carries
its 1-based ordinal, its title or null, its body text lines, its speaker notes or null, and the images
it references in reading order. These model records are defined by the OpenXml subsystem and are the
agreement both backends share.

**The content emitter.** `PowerPointContentEmitter` is the single emission path. It writes the embedded
images first so each slide can link its pictures inline, writes one section per slide carrying its
title, body text, and speaker notes in presentation order, reports the document information and
captured metadata, and reports the content inventory. A deck with no slides becomes an empty
`content.md` plus zero-count inventory entries for the looked-for document structure.

**The speaker-notes accounting.** The emitter counts the slides that carry speaker notes and reports
that count in the content inventory, marked as looked for. A deck carrying none therefore reads
`0 sets of speaker notes` instead of dropping the line, which is exactly what lets a reader tell "we
read every notes slide and there are none" from "notes are not something DocDown counts".

**The image reporting rule.** Embedded images that are written are counted as inline images. Vector
metafiles such as EMF and WMF are written as found, with no PowerPoint-specific note, because the
extraction completed exactly what the deck supplied. When an attempted image step cannot be completed
— for example, an image exceeded a caller size limit — the emitter records a short plain note
describing that attempted step.

**The unit split.** The subsystem has one unit, `PowerPointContentEmitter`, which owns the sink walk,
the per-slide markdown layout, the content inventory, and the note policy for PowerPoint-specific image
steps. No PowerPoint-specific code set or classification vocabulary sits beside it; the subsystem uses
Core's shared `ContentFeature`, `EnvironmentFact`, and `ExtractionNote` types directly.
