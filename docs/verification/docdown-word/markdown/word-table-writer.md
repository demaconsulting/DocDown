## WordTableWriter Verification Design

This document describes the unit-level verification strategy for `WordTableWriter`, the unit that
renders a table model as a GitHub-Flavored-Markdown table and counts every cell it flattens.

### Verification Approach

`WordTableWriter` is verified through unit tests in `Markdown/WordTableWriterTests.cs` in
`DemaConsulting.DocDown.Word.Tests`, with method names beginning with `WordTableWriter_`.

The unit is driven against hand-built `WordTableModel` instances with no document behind them,
because the writer's contract is the projection from a grid onto GFM. Each scenario supplies the
minimal shape it needs — a full header-plus-body table, a short row against a wider header, a cell
carrying a newline and a pipe, an all-empty table, or a table declaring merged and nested cell
counts — so a failure names one table rule rather than one document.

Both return values are asserted. The rendered markdown proves the shape reached the output, and the
flattened count is asserted separately because it is what the emitter later uses when it decides
whether to report a table-flattening note.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `WordTableModel` instances constructed from `WordTableCell` and
  `WordInline` records
- **Filesystem**: none
- **Mocking**: none
- **Isolation**: each test constructs its own table

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordTableWriter` unit test run passes when a full table renders a header
row, delimiter row, and body as GFM; when a row shorter than the widest is padded to the column
count with empty cells; when a pipe within a cell is escaped and a newline is converted to a line
break so the cell stays within one grid cell; when a table whose cells hold no content renders as
an empty string; and when the flattened count returned equals the sum of the model's merged-cell
count and its nested-table count.

### Test Scenarios

#### A full table renders as GFM with a header and delimiter row

**Test**: `WordTableWriter_Write_FullTable_ProducesGfmWithHeaderAndAlignment`

Proves the header row (`| Component | Status |`), delimiter row (`| --- | --- |`), and both body
rows appear in the rendered markdown, and that the flattened count is zero because no cell had to
be flattened. Evidence for `DocDownWord-Markdown-WordTableWriter-RendersGfmTable`.

#### A short row is padded with empty cells

**Test**: `WordTableWriter_Write_ShortRow_PadsWithEmptyCells`

Proves a one-cell row against a three-column header renders as `| only |  |  |`, keeping the grid
rectangular. Evidence for `DocDownWord-Markdown-WordTableWriter-PadsShortRows`.

#### A cell's newline and pipe are transformed

**Test**: `WordTableWriter_Write_CellWithNewlineAndPipe_EscapesAndBreaks`

Proves a cell containing `line one\nline two | end` renders as `line one<br>line two \| end`, so
the newline becomes a line break within the cell and the pipe is escaped rather than starting a new
cell. Evidence for `DocDownWord-Markdown-WordTableWriter-EscapesCellContent`.

#### An empty table renders as an empty string

**Test**: `WordTableWriter_Write_EmptyTable_ReturnsEmpty`

Proves a table whose cells hold no content renders as `string.Empty`, so the caller can skip the
table entirely. Evidence for `DocDownWord-Markdown-WordTableWriter-SkipsEmptyTable`.

#### Merged and nested cells are counted as flattened

**Test**: `WordTableWriter_Write_MergedAndNestedCells_CountsFlattened`

Proves a table declaring two merged cells and one nested table returns a flattened count of three —
the sum of merges and nested tables. Evidence for
`DocDownWord-Markdown-WordTableWriter-FlattensMergedAndNested`.
