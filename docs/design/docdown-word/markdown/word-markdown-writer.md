## WordMarkdownWriter

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordMarkdownWriter` renders a `WordDocumentModel` — or any block sequence within it — to a single
markdown flow. It is presentational only: it shapes text, headings, lists, tables, images,
comments, footnotes, and the `## Document Control` section, while inventory and extraction-note
reporting live with the emitter. The single responsibility keeps the mapping testable from a
hand-built model with no document behind it.

### Data Model

`WordMarkdownWriter` is an `internal static class` with no state; a `StringBuilder` lives for one
call. The private `EscapedCharacters` set names the full markdown-structural set:
`` \ ` * _ { } [ ] ( ) # + - . ! | ``. Escaping the whole set keeps stray occurrences in document
prose from being reinterpreted as headings, emphasis, links, list markers, or table separators.

### Key Methods

- **`static string Write(WordDocumentModel model, IReadOnlyDictionary<string, string> imagePaths)`**
  — renders a whole document. Places the `## Document Control` section immediately after a leading
  title heading and before the body, synthesizing `# {Title}` when the body has no leading heading
  but the metadata names a title. Appends `## Comments` and `## Footnotes` last. Pure.
- **`static string WriteBlocks(IReadOnlyList<WordBlock>, IReadOnlyDictionary<string, string>)`** —
  renders a block sequence, used for the body, each per-part section, and each document-control
  subsection. Pure.
- **`internal static string RenderInlines(IReadOnlyList<WordInline>?)`** — renders inline runs,
  escaping literal text and applying the minimal preserved formatting: bold as `**…**`, italic as
  `*…*`, and hyperlinks as `[text](href)`. A `Raw` inline is emitted verbatim so footnote markers
  and hard breaks survive escaping. Pure.
- **`internal static string Escape(string text)`** — backslash-escapes each character in the
  `EscapedCharacters` set. Pure.
- **`AppendBlocks()`** and **`AppendBlock()`** (private) — walk the block sequence and emit
  headings, paragraphs, list items, tables, images, and page breaks with the spacing rules markdown
  requires. Tables delegate to `WordTableWriter.Write()`; images emit links only when the sink
  supplied a resolved path.
- **`AppendDocumentControl()`**, **`AppendComments()`**, and **`AppendFootnotes()`** (private) —
  append the surviving document-control subsections, the comment list, and the footnote
  definitions in the positions the system design requires.

### Error Handling

Null arguments to `Write()` and `WriteBlocks()` are rejected with `ArgumentNullException` at the
point of the call. The unit performs no I/O, so it has no other error conditions and cannot make
an extraction unreadable on its own; any adverse document state is already represented on the model
the writer received.

### Dependencies

- **`WordDocumentModel`, `WordBlock`, `WordBlockKind`, `WordInline`, `WordListInfo`, `WordImageRef`,
  `WordDocumentControlSection`, and `WordComment`** — the model this unit renders.
- **`WordTableWriter`** — delegated for every `Table` block.

### Callers

`WordContentEmitter.WriteContentAsync()` uses `Write()` for single-flow mode and `WriteBlocks()`
for each split part; `WordOpenXmlReader.BuildDocumentControl()` uses `WriteBlocks()` internally to
compute the deduplication key for identical rendered header or footer content across sections.
