## WordTableWriter

![DocDown.Word Structure](WordView.svg)

### Purpose

`WordTableWriter` renders a `WordTableModel` to a GitHub-flavored-markdown table, applying the
table rules that preserve a Word grid as faithfully as markdown allows and returning the count of
cells it had to flatten. Tables are where a Word extraction visibly beats a PDF extraction: Open
XML carries genuine row-and-column structure where PDF flattened it into concatenated runs.

### Data Model

`WordTableWriter` is an `internal static class` with no state; a `StringBuilder` lives for one
call. Its input is a `WordTableModel` populated by a backend reader; its output is a
`(string Markdown, int FlattenedCells)` tuple, where `FlattenedCells` is
`MergedCellCount + NestedTableCount`. An empty `Markdown` — no rows, no columns, or no cell
content anywhere — signals the caller to skip the table entirely.

### Key Methods

- **`static (string Markdown, int FlattenedCells) Write(WordTableModel table)`** — renders the
  table by these rules:

  1. **Row one is the header.** GFM requires a delimiter row, so the writer always emits row one as
     the header.
  2. **The column count is the widest row.** Short rows are padded with empty cells so the grid
     stays rectangular.
  3. **Cell content escapes structurally.** A newline inside a cell becomes `<br>`, a pipe stays
     escaped through the inline renderer, and the final cell text is trimmed.
  4. **Merge continuations stay aligned.** Horizontal and vertical merge continuations are already
     represented as empty cells in the model, so the grid remains readable even though markdown
     cannot express the merge.
  5. **Nested tables stay flattened.** The reader pre-flattens a nested table into a raw
     `<br>`-joined inline and records the nested-table count separately, so the writer never
     recurses into a table cell.
  6. **An empty table is skipped.** When no cell of any row carries rendered text, the writer
     returns an empty string instead of an empty shell.

  Preconditions: `table` non-null. Postcondition: pure — no I/O, no shared state. The
  flattened-cell count is what lets the emitter later state how much table structure markdown could
  not preserve.
- **`RenderCell()`** (private) — renders one cell of a row using `WordMarkdownWriter.RenderInlines()`,
  turns `\r\n` and `\n` into `<br>`, and trims. A cell beyond the row's own width is an empty
  string so the grid stays rectangular.
- **`AppendRow()`** (private) — appends one pipe-delimited row `| a | b | c |` ending in `\n`.

### Error Handling

A null `table` is rejected with `ArgumentNullException`. No condition of the model raises an
exception: every edge case is a design decision the table rules encode, and every loss GFM cannot
express is returned as a count. The writer performs no I/O.

### Dependencies

- **`WordTableModel` and `WordTableCell`** — the input.
- **`WordMarkdownWriter.RenderInlines()`** — for cell inline rendering so cells and paragraphs
  escape and format identically.

### Callers

`WordMarkdownWriter.AppendBlock()` for every `Table` block in the body and in each document-control
subsection. The flattened-cell count is used downstream by `WordContentEmitter` when it decides
whether a table-flattening note must be reported.
