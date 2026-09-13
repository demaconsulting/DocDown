## WordTableWriter

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordTableWriter` renders a `WordTableModel` to a GitHub-flavored-markdown table, applying the
seven table rules and returning the count of cells it had to flatten. Tables are where a Word
extraction visibly beats a PDF extraction: Open XML carries genuine row-and-column structure where
PDF flattened it into concatenated runs. The edge cases — merged cells, nested tables, missing
header markers, ragged rows — are exactly where table extraction usually goes wrong, so each is
handled explicitly and, where GFM cannot represent the structure, counted rather than dropped.

### Data Model

`WordTableWriter` is an `internal static class` with no state; a `StringBuilder` lives for one
call. Its input is a `WordTableModel` populated by a backend reader; its output is a
`(string Markdown, int FlattenedCells)` tuple, where `FlattenedCells` is `MergedCellCount +
NestedTableCount` and is what lets the emitter raise a single counted structural gap plus
`WORD0005`. An empty `Markdown` — no rows, no columns, or no cell content anywhere — signals the
caller to skip the table and emit `WORD0003` rather than the writer producing a bare header-and-
delimiter shell.

### Key Methods

- **`static (string Markdown, int FlattenedCells) Write(WordTableModel table)`** — renders the
  table by the seven rules stated together:

  1. **Row one is the header.** GFM requires a delimiter row, so the writer always emits row one
     as the header and the delimiter row after it. When Word did not mark row one a header
     (`table.FirstRowIsHeader` is false), the caller emits `WORD0004 TableHeaderAssumed` so the
     assumption is stated rather than hidden.
  2. **The column count is the widest row.** Short rows are padded with empty cells so the grid
     stays rectangular, which is what keeps a GFM table readable when a document's rows carry
     different cell counts.
  3. **Cell content escapes structurally.** A newline inside a cell becomes `<br>` (a GFM row is
     a single line); a literal `|` is `\|`; the cell is trimmed. The inline renderer already
     escapes the rest of the markdown-structural set.
  4. **`w:gridSpan` puts the text in the first spanned column.** The spanned columns are emitted
     as empty cells and each empty column is counted into `MergedCellCount`.
  5. **`w:vMerge` continuations are empty.** A vertical-merge continuation cell is emitted as an
     empty cell and counted into `MergedCellCount`. The origin cell keeps its text.
  6. **Nested tables are flattened.** The reader flattens a nested table into `<br>`-joined rows
     and hands it back as a single raw inline (see *WordOpenXmlReader Design*); the count is
     already in `NestedTableCount`. The writer never re-recurses into a table cell.
  7. **An empty table is skipped.** When the widest row has zero columns, or every cell is empty
     after rendering, the writer returns an empty `Markdown` string. The caller then skips the
     table and emits `WORD0003 EmptyTableSkipped`.

  The flattening from rules 4–6 accumulates into one counted `GapKind.Structure` /
  `GapScope.PartiallyExtracted` gap plus `WORD0005 MergedCellsFlattened` — the emitter's single
  summary of every cell that GFM could not express. Preconditions: `table` non-null. Postcondition:
  pure — no I/O, no shared state.
- **`RenderCell`** (private) — renders one cell of a row using `WordMarkdownWriter.RenderInlines`,
  turns `\r\n` and `\n` into `<br>`, and trims. A cell beyond the row's own width is an empty
  string so the grid stays rectangular.
- **`AppendRow`** (private) — appends one pipe-delimited row `| a | b | c |` ending in `\n`.

### Error Handling

A null `table` is rejected with `ArgumentNullException`. No condition of the model raises an
exception: every edge case is a design decision the seven rules encode, and every loss GFM cannot
express is returned as a count. The writer performs no I/O.

### Dependencies

- **`WordTableModel`, `WordTableCell`** — the input.
- **`WordMarkdownWriter.RenderInlines`** — for cell inline rendering so cells and paragraphs
  escape and format identically.

### Callers

`WordMarkdownWriter.AppendBlock` for every `Table` block in the body and in each document-control
subsection. The returned flattened-cell count is discarded there because it is already reported by
the emitter (`WordContentEmitter.ReportTableDiagnostics`) from the model directly, so the writer's
return is used only by the emitter's own enumeration.
