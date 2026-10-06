## WordContentEmitter

![DocDown.Word Structure](WordView.svg)

### Purpose

`WordContentEmitter` emits a rendered `WordDocumentModel` through the extraction sink, applying the
package's reporting policy in one place. It writes content, images, `DocumentInfo`, content
inventory, reviewer comments, metadata, and short extraction notes derived from the model. Nothing
is omitted silently.

The unit exists because model-to-sink emission is one responsibility. Holding it apart from the
reader is what lets every emission decision be proved from a hand-built model with no document
behind it, and what keeps the output contract from acquiring a second implementation that could
quietly diverge from the first.

### Data Model

`WordContentEmitter` is an `internal static class` with no state. Two private path constants —
`ContentTarget = "content.md"` and `ImagesTarget = "images/"` — define the content and image layout
it writes through the sink. The unit reads only the shared `WordDocumentModel`,
`ExtractionOptions`, and image references passed in.

### Key Methods

- **`static ValueTask EmitAsync(IExtractionSink sink, ExtractionOptions options, WordDocumentModel model,
  CancellationToken cancellationToken)`** — writes images first so the content renderer can place
  the sink-allocated links, writes the document as one continuous flow, reports each of the model's
  comments as a review comment, reports
  `DocumentInfo`, reports the content inventory, reports document metadata when present, and
  reports any extraction notes implied by the model. Preconditions: every argument non-null.
  Postcondition: every artifact was routed through the sink; no filesystem path was written
  directly.
- **`internal static ImageHint HintFor(WordImageRef image)`** — builds the passthrough hint every
  image is added with: `Transform = Passthrough`, `WidthPx = null`, `HeightPx = null`,
  `SourcePage = null`, plus the media type, preferred name, source part URI, optional description,
  and description source from the image reference. Pure.
- **`WriteImagesAsync()`** (private) — returns immediately when embedded images are suppressed.
  Otherwise it walks the model's image references in document order, adds each through the sink,
  and records the returned path by source reference. Images are written in whatever format the
  document stored them in; nothing is re-encoded, so there is nothing to report here.
- **`WriteContentAsync()`** (private) — writes the whole document as one `content.md`. A Word
  document is one continuous flow, so splitting it at headings would invent a structure the
  document does not assert.
- **`ReportContentFeatures()`** (private) — reports the content inventory from the model: text
  blocks, headings, tables, list items, inline images, comments, distinct comment authors, and
  footnotes. Text blocks, comments, distinct comment authors, and footnotes are marked looked-for
  so zero remains explicit.
- **`ReportReviewComments()`** (private) — reports each of the model's comments to the sink as a
  `DocumentComment` carrying the comment's author, its body as **literal text** rendered through
  `WordMarkdownWriter.RenderPlainText`, and its
  resolved location hint. A comment the reader could not anchor is reported with the literal
  `(location unknown)` rather than an invented location. The body is reported unescaped because it
  reaches `manifest.json` as well as `review-comments.md`, and the manifest's documented contract is
  "the comment text as the document records it"; escaping is `ReviewCommentsWriter`'s job, at the
  point the markdown is written, which is also what makes every backend report the same thing. A
  comment whose rendered body is blank is skipped: the reader already drops those, so this is the
  second of two layers, placed here because this is the only unit that calls
  `IExtractionSink.ReportReviewComment` for Word and that method rejects a blank body by contract.
  This is the only route reviewer comments
  take: they are never rendered into `content.md`, because a reviewer's remark is commentary about
  the document rather than part of it.
- **`ReportExtractionNotes()`** (private) — emits only the incomplete-step notes the current design
  allows: charts whose chart parts were not read, merged or nested table structure flattened for
  markdown, and comments whose only content was a picture or ink.
- **`ReportImageOnlyCommentsNote()`** (private) — states, when `CommentsWithUnreadableContent` is
  non-zero, how many comments carry only a picture or ink and therefore do not appear in
  `review-comments.md`. This is the narrow case a note exists for: the reviewer did leave a remark,
  the extractor attempted to read it, and what it found cannot be rendered as comment text. A
  comment nobody ever typed into is not reported, because nothing was lost there.
- **`CountTextualBlocks()`** and **`EnumerateTables()`** (private) — the small pure helpers used to
  derive looked-for inventory counts and flattened-table totals from the model.

### Error Handling

Every null argument to `EmitAsync()` is rejected with `ArgumentNullException`. Cancellation is
observed between images and between parts. No condition of the model raises an exception here:
every incomplete step that must be surfaced becomes a note, and every empty-but-looked-for category
becomes a zero-count inventory entry. The unit performs no filesystem I/O of its own.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `DocumentInfo`, `ContentFeature`,
  `ImageHint`, `ImageTransform`, `ContentPart`, `ContentPartKind`, `DocumentComment`, and
  `ExtractionNote`.
- **`WordDocumentModel`, `WordBlock`, `WordImageRef`, `WordTableModel`, and
  `WordDocumentControlSection`** — the model and its supporting records.
- **`WordMarkdownWriter` and `WordTableWriter`** — the rendering helpers used for content and
  tables.

### Callers

`WordOpenXmlExtractor.ExtractAsync()` calls this unit after it has produced a model.
`WordOpenXmlImageReader.Read()` also reuses `HintFor()` so tests can observe the same image
provenance a real extraction would report.
