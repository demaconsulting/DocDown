### PowerPointContentEmitter

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointContentEmitter` is the single emission path: it turns a read `PowerPointDeckModel` into the
output contract. Its single responsibility is that every output the model implies — the per-slide
content carrying title, body text, and speaker notes, the inline images, the content inventory, and
the plain notes for attempted image steps that could not be completed — is produced in one place,
driven only by the model and never by how the model was read. The COM backend reuses it by delegation,
so a deck's content reads identically whether or not it was rendered.

### Data Model

`PowerPointContentEmitter` is an `internal static class`. It holds no state. Named constants pin the
ledger target paths (`content.md`, `images/`) and the empty image-path map used when images are
suppressed.

### Key Methods

- **`ValueTask EmitAsync(IExtractionSink sink, ExtractionOptions options, PowerPointDeckModel model,
  CancellationToken)`** — the whole emission. For an empty deck it writes empty content, reports page
  count zero, reports zero-count inventory for the looked-for document structure, and returns.
  Otherwise it writes embedded images first, writes the per-slide content, reports document info and
  captured metadata, reports plain notes for attempted image steps that could not be completed, and
  reports the content inventory. Preconditions: all arguments non-null.
- **`WriteContentAsync`** (private) — writes the deck as one content flow, each slide carrying its
  title heading, body text, and speaker notes, with inline image links resolved from the sink's
  written-path map.
- **`ReportImages`** (private) — reports nothing when images were deliberately suppressed or when the
  deck embeds no images; otherwise records plain notes for images that exceeded a caller size limit.
- **`ReportSizeSkipNote`** (private) — records a one-sentence note naming how many embedded images
  exceeded the caller-supplied size limit and were not written.
- **`ReportContentFeatures`** (private) — reports the outline counts (slides, slide titles, sets of
  speaker notes, inline images) from the model. The speaker-notes count is declared looked for, so it
  is stated even at zero.

### The speaker-notes accounting

`EmitAsync` counts the slides carrying speaker notes and hands that count to `ReportContentFeatures`.
A deck that carries none reads `0 sets of speaker notes` in the summary's content outline and appears
with `"count": 0` in the manifest's `contentFeatures`, so a reader can tell "every notes slide was
read and there are none" from "notes are not something DocDown counts". No PowerPoint-specific note
accompanies that case because the extraction completed exactly what it set out to do.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed and propagates as
`OperationCanceledException`. No other error condition arises here: the emitter writes only through the
sink and reads only the model, so an adverse deck was already turned into an `Unreadable` result
upstream in the reader or Core.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`,
  `DocumentInfo`, `ContentFeature`, `EmbeddedImageWriter`, and `ExtractionNote`.
- **PowerPointDeckModel** — the read model it renders. See *PowerPointOpenXmlReader Design*.

### Callers

`PowerPointOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after the reader produces the model; the
COM backend reaches it through the same delegated managed extraction.
