## WordMarkdownWriter

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordMarkdownWriter` renders a `WordDocumentModel` — or any block sequence within it — to a single
markdown flow. It is the whole markdown mapping, presentational only: it shapes text and nothing
else, so the diagnostics and gaps that describe a table's flattening, a tracked-change decision,
or an omitted header live with the emitter rather than here. The single responsibility keeps the
mapping testable from a hand-built model with no document behind it.

### Data Model

`WordMarkdownWriter` is an `internal static class` with no state; a `StringBuilder` lives for one
call. The private `EscapedCharacters` set names the full markdown-structural set:
`` \ ` * _ { } [ ] ( ) # + - . ! | ``. Escaping the whole set keeps stray occurrences in document
prose from being reinterpreted as headings, emphasis, links, list markers, or table separators.

### Key Methods

- **`static string Write(WordDocumentModel model, IReadOnlyDictionary<string, string> imagePaths)`** —
  renders a whole document. Places the `## Document Control` section immediately after a leading
  title heading and before the body — the position of metadata about the whole document rather
  than interruption of the narrative. When the body carries no leading heading but the metadata
  names a title, synthesizes `# {Title}` so `content.md` is self-identifying (the same role the
  PDF backend's title line plays). Appends `## Comments` and `## Footnotes` last. Preconditions:
  both arguments non-null. Postcondition: pure — no I/O, no shared state.
- **`static string WriteBlocks(IReadOnlyList<WordBlock>, IReadOnlyDictionary<string, string>)`** —
  renders a block sequence, used for the body, each per-part section, and each document-control
  subsection. Pure.
- **`internal static string RenderInlines(IReadOnlyList<WordInline>?)`** — renders inline runs,
  escaping literal text and applying the minimal preserved formatting (bold as `**…**`, italic as
  `*…*`, hyperlink as `[text](href)`). A `Raw` inline is emitted verbatim so a footnote marker
  such as `[^1]` or a hard break survives escaping. Shared with `WordTableWriter` so a cell and a
  paragraph escape and format identically. Pure.
- **`internal static string Escape(string text)`** — backslash-escapes each character in the
  `EscapedCharacters` set. Pure.
- **`AppendBlocks`** (private) — tracks the previous block's kind so a run of list items is
  separated from surrounding paragraphs by a single blank line without a blank line between the
  items themselves, and a trailing list still gets its blank line. Side effect: appends to the
  builder.
- **`AppendBlock`** (private) — dispatches on `WordBlockKind`. A heading clamps its level to 1..6
  and emits `# …` at the clamped depth. A paragraph emits its inlines with a blank line after. A
  list item indents two spaces per `WordListInfo.Level` and prefixes `1.` or `-`. A table
  delegates to `WordTableWriter.Write`, appending the returned markdown when it is not empty; the
  flattened-cell count is discarded here because it is reported by the emitter, not the writer. A
  page break emits `---` as a thematic break. A document-control block is intentionally never
  rendered here — `AppendDocumentControl` handles it.
- **`AppendImage`** (private) — emits a link only when the sink returned a path for the image's
  source-reference URI, so a suppressed or deduplicated-away image never leaves a link pointing at
  nothing; the alt text is the image's descriptive text when a genuinely descriptive source was chosen,
  and the neutral `image` otherwise, so a heading or media name that only seeded the file name is never
  asserted as the picture's description. Escaped either way.
- **`AppendDocumentControl`** (private) — emits `## Document Control` when any subsection
  survived, then each subsection as `### {Label}` followed by its blocks.
- **`AppendComments`** (private) — emits `## Comments` with one bulleted entry per comment,
  author-bold-attributed when the comment carries an author.
- **`AppendFootnotes`** (private) — emits `## Footnotes` with each body as `[^n]: …`, indexed
  one-based to match the reference markers.

### Error Handling

Null arguments to `Write` and `WriteBlocks` are rejected with `ArgumentNullException` at the point
of the call. The unit performs no I/O, so it has no other error conditions and cannot degrade the
extraction on its own; any adverse condition of the document is already recorded on the model by
the reader.

### Dependencies

- **`WordDocumentModel`, `WordBlock`, `WordBlockKind`, `WordInline`, `WordListInfo`,
  `WordImageRef`, `WordDocumentControlSection`, `WordComment`** — the model this unit renders.
- **`WordTableWriter`** — delegated for every `Table` block.

### Callers

`WordContentEmitter.WriteContentAsync` uses `Write` for single-flow mode and `WriteBlocks` for
each split part; `WordOpenXmlReader.BuildDocumentControl` uses `WriteBlocks` internally to compute
the deduplication key for identical rendered header or footer content across sections.
