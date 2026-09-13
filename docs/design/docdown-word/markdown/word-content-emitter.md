## WordContentEmitter

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordContentEmitter` emits a rendered `WordDocumentModel` through the extraction sink, applying the
package's reporting policy in one place. It writes content, images, `DocumentInfo`, content
inventory, metadata, and short extraction notes derived from the model. Nothing is omitted
silently.

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
  the sink-allocated links, writes either a single flow or per-part content, reports
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
  records the returned path by source reference, and reports one note when
  `ImageOutputMode.ForcePng` was requested but the images were written in their source encoding.
- **`WriteContentAsync()`** (private) — honors `ContentSplitMode.Single` and `ContentSplitMode.Auto`
  by writing one `content.md`. Under `ContentSplitMode.PerPart` it calls `BuildParts()` and writes
  one `parts/*.md` file per top-level section, falling back to a single flow when the body has no
  `Heading 1` boundary.
- **`BuildParts()`** (private) — walks the body once collecting `Heading 1` positions. Leading
  matter together with `## Document Control` becomes the first part when present, each later
  section is titled from its heading text, and comments and footnotes are appended to the final
  part so they remain discoverable in split output.
- **`ReportContentFeatures()`** (private) — reports the content inventory from the model: text
  blocks, headings, tables, list items, inline images, comments, distinct comment authors, and
  footnotes. Text blocks, comments, distinct comment authors, and footnotes are marked looked-for
  so zero remains explicit.
- **`ReportExtractionNotes()`** (private) — emits only the incomplete-step notes the current design
  allows: charts whose chart parts were not read, merged or nested table structure flattened for
  markdown, and force-PNG requests that could not be completed because the package does not
  re-encode images.
- **`CountTextualBlocks()`** and **`EnumerateTables()`** (private) — the small pure helpers used to
  derive looked-for inventory counts and flattened-table totals from the model.

### Error Handling

Every null argument to `EmitAsync()` is rejected with `ArgumentNullException`. Cancellation is
observed between images and between parts. No condition of the model raises an exception here:
every incomplete step that must be surfaced becomes a note, and every empty-but-looked-for category
becomes a zero-count inventory entry. The unit performs no filesystem I/O of its own.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `DocumentInfo`, `ContentFeature`,
  `ImageHint`, `ImageTransform`, `ContentPart`, `ContentPartKind`, `ImageOutputMode`,
  `ContentSplitMode`, and `ExtractionNote`.
- **`WordDocumentModel`, `WordBlock`, `WordImageRef`, `WordTableModel`, and
  `WordDocumentControlSection`** — the model and its supporting records.
- **`WordMarkdownWriter` and `WordTableWriter`** — the rendering helpers used for content and
  tables.

### Callers

`WordOpenXmlExtractor.ExtractAsync()` calls this unit after it has produced a model.
`WordOpenXmlImageReader.Read()` also reuses `HintFor()` so tests can observe the same image
provenance a real extraction would report.
