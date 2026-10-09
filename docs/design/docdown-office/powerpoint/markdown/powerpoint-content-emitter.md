### PowerPointContentEmitter

![DemaConsulting.DocDown.PowerPoint Structure](PowerPointView.svg)

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
  captured metadata, reports plain notes for attempted image steps that could not be completed,
  reports every slide comment as a review comment, and
  reports the content inventory. Preconditions: all arguments non-null.
- **`WriteContentAsync`** (private) — writes the deck as one content flow, each slide carrying its
  title heading, body text, and speaker notes, with inline image links resolved from the sink's
  written-path map.
- **`ReportImages`** (private) — reports nothing when images were deliberately suppressed or when the
  deck embeds no images; otherwise records plain notes for images that exceeded a caller size limit.
- **`ReportSizeSkipNote`** (private) — records a one-sentence note naming how many embedded images
  exceeded the caller-supplied size limit and were not written.
- **`ReportReviewComments`** (private) — reports each slide's comments to the sink as `DocumentComment`
  entries located `Slide {n}` using the slide's 1-based ordinal. A reviewer's remark is commentary
  *about* the deck rather than part of it, so it is never written into the content flow, where a
  consumer could not tell a reviewer's words from the author's; Core collects the reported comments
  into the dedicated `review-comments.md` artifact. The slide number is the location because a deck's
  argument is sequential and the number is how a reader navigates back to it. A deck with no comments
  reports none, and Core writes no artifact. Modern persona comments never reach the model's comment
  lists at all; the reader counts them instead, and `ReportModernCommentsNote` below states their
  presence. See *PowerPointOpenXmlReader Design* for that stated limitation.
- **`ReportContentFeatures`** (private) — reports the outline counts (slides, slide titles, sets of
  speaker notes, inline images, comments, distinct comment authors) from the model. The
  speaker-notes count and both comment counts are declared looked for, so each
  is stated even at zero. The comment counts matter particularly: reviewer commentary leaves
  `content.md` entirely for `review-comments.md`, so the inventory is the only place the summary says
  a deck carries any at all.
- **`ReportModernCommentsNote`** (private) — records a one-sentence note, when the deck carries any,
  naming how many modern persona-based comments sit on how many slides and stating that this
  extractor does not read them. Without it a deck whose comments are all modern reads exactly like a
  deck nobody commented on, which reports a boundary of the extractor as a fact about the document.
  Nothing is reported when the deck carries none, because there is no shortfall to state.

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

- **DemaConsulting.DocDown.Core** — `IExtractionSink`, `ExtractionOptions`,
  `DocumentInfo`, `ContentFeature`, `EmbeddedImageWriter`, and `ExtractionNote`.
- **PowerPointDeckModel** — the read model it renders. See *PowerPointOpenXmlReader Design*.

### Callers

`PowerPointOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after the reader produces the model; the
COM backend reaches it through the same delegated managed extraction.
