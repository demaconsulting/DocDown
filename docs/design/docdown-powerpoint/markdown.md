## Markdown Subsystem

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.PowerPoint`. It defines how the
backend-neutral `PowerPointDeckModel` becomes the output contract: the content emitter that walks the
model through the extraction sink — one section per slide carrying its title, body text, and speaker
notes — and reports the deck's inline images, its content outline, its diagnostics, and the honest gaps
the model and options imply. Every mapping decision that differentiates a PowerPoint extraction — the
per-slide layout, the speaker-notes accounting, the inline image links, the charts-not-read gap, and the
single-flow-versus-per-part split — lives here and is testable from a hand-built model with no deck and no
Open XML SDK.

The subsystem exists because reading a deck and rendering what was read are separate concerns. Holding the
whole projection apart from the reader is what makes every mapping decision provable from a hand-built
model, and it is what lets the COM backend reuse the exact same emission by delegation, so a deck's
content reads identically whether or not it was rendered.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `PowerPointDeckModel` | Inbound, from the reader | .NET record | The pivot between reading and rendering |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from `IExtractionContext` | .NET record | Split mode, force-PNG, images |
| `PowerPointDiagnosticCodes` | Outbound to callers | .NET constants | `PPTX0001`–`PPTX0005` codes this package owns |

### Design

**The deck model.** `PowerPointDeckModel` is the whole cross-reader contract: the slides in presentation
order, the deduplicated embedded images, the deck metadata, and the count of charts the deck embeds. Each
`PowerPointSlideModel` carries its 1-based ordinal, its title (or null), its body text lines, its speaker
notes (or null), and the images it references in reading order. These model records are the inbound
contract this subsystem renders; they are defined and documented by the OpenXml subsystem, where the files
live, and are the pivot both subsystems agree on.

**The content emitter.** `PowerPointContentEmitter` is the single emission path. It reports an empty deck
as a counted gap and writes an empty content document rather than nothing; otherwise it writes the
embedded images first so each slide can link its pictures inline, writes one section per slide carrying
its title, body text, and speaker notes in presentation order (as a single flow or, in per-part mode, one
part per slide), reports the document info and metadata, and reports every diagnostic and counted gap the
model implies. Its gap policy is the honesty of the extraction: an empty deck is a counted gap that
degrades, a vector image carries an informational readability caveat, and charts the backend does not read
are a counted gap naming them with a remedy that points at the workbook route.

**The speaker-notes accounting.** The emitter counts the slides that carry speaker notes and reports that
count in the content outline, declared as a feature it looked for. A deck carrying none therefore reads
`0 sets of speaker notes` rather than dropping the line, which is exactly what lets a reader tell "every
notes slide was read and there are none" from "notes are not something DocDown counts". The absence
raises no gap, no diagnostic, and no degradation: it is a fact about the deck, and DocDown's gaps state
only what DocDown could not do. *Supersession note:* an earlier statement of intent required this
absence to be a **counted gap**, and the shipped emitter reported it as an **informational** `PPTX0002`
diagnostic. Both are superseded by the inventory zero; the unit requirement
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceGap` is replaced by
`...-ReportsSpeakerNotesAbsenceInInventory`.

**The diagnostic-code contract.** `PowerPointDiagnosticCodes` defines the four codes this package owns —
`PPTX0001` `NoSlides`, `PPTX0003` `VectorImageWrittenAsIs`, `PPTX0004` `SlideRenderFailed`, `PPTX0005`
`ChartsNotExtracted`. The distinct `PPTX` prefix cannot collide with Core's `DD` range or another
package's prefix in any shared manifest, which makes ownership self-evident. A published code number is
never reused, so the set is stable rather than contiguous: `PPTX0002` `NoSpeakerNotes` is retired and
permanently reserved, and a table-pinning test guards both the surviving set and the hole. It is a
supporting type documented here.

**The unit split.** The subsystem has one unit, `PowerPointContentEmitter`, which owns the sink walk, the
per-slide rendering, the speaker-notes accounting, and the whole gap-and-diagnostic policy. The supporting
diagnostic-code and `NamespaceDoc` types live in the same subsystem folder because they are the vocabulary
of the unit, not units of their own.
