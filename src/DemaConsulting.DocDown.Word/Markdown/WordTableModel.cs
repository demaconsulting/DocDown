namespace DocDown.Word.Markdown;

/// <summary>
///     A backend-neutral table model: a grid of cells plus the accounting the caller needs to
///     report every flattening honestly.
/// </summary>
/// <param name="Rows">
///     The table's rows, each a list of cells. The reader emits a rectangular-enough grid: a
///     horizontally merged (<c>w:gridSpan</c>) or vertically merged (<c>w:vMerge</c>) continuation
///     cell is present but empty, so column alignment is preserved even though the merge itself
///     cannot be expressed in GitHub-flavored markdown.
/// </param>
/// <param name="FirstRowIsHeader">
///     <see langword="true"/> when Word itself marked the first row a header (<c>w:tblHeader</c>).
///     When <see langword="false"/> the writer still uses row one as the header — GFM requires a
///     header row — and the caller emits <c>WORD0004 TableHeaderAssumed</c> so the assumption is
///     stated rather than hidden.
/// </param>
/// <param name="MergedCellCount">
///     The number of cells emptied by a horizontal or vertical merge, so the caller can raise a
///     counted structural gap for the merges GFM cannot represent.
/// </param>
/// <param name="NestedTableCount">
///     The number of nested tables flattened into a parent cell as <c>&lt;br&gt;</c>-joined rows,
///     counted into the same structural gap.
/// </param>
/// <remarks>
///     Word carries genuine table structure where a PDF flattened it into concatenated runs, so
///     this model is the headline differentiator: it preserves rows and columns and accounts for
///     exactly what could not be preserved. Immutable and thread-safe.
/// </remarks>
internal sealed record WordTableModel(
    IReadOnlyList<IReadOnlyList<WordTableCell>> Rows,
    bool FirstRowIsHeader,
    int MergedCellCount,
    int NestedTableCount);

/// <summary>
///     One cell of a <see cref="WordTableModel"/>.
/// </summary>
/// <param name="Content">
///     The cell's inline content. A merge-continuation cell carries an empty list; a cell holding
///     a nested table carries the inner rows pre-joined with <c>&lt;br&gt;</c> markers so the
///     writer can render them without nesting.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordTableCell(IReadOnlyList<WordInline> Content);
