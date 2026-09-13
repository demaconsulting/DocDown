using System.Text;

namespace DocDown.Word.Markdown;

/// <summary>
///     Renders a <see cref="WordTableModel"/> to a GitHub-flavored-markdown table, applying the
///     seven table rules and returning the count of cells it had to flatten.
/// </summary>
/// <remarks>
///     <para>
///         Tables are where Word must visibly beat PDF: a PDF flattened a table into concatenated
///         runs, but Open XML carries genuine row-and-column structure, so this unit preserves it.
///         The edge cases — merged cells, nested tables, missing header markers, ragged rows — are
///         exactly where table extraction usually goes wrong, so each is handled explicitly and,
///         where GFM cannot represent the structure, counted rather than dropped.
///     </para>
///     <para>
///         The returned flattened-cell count is what lets the caller raise a single counted
///         structural gap plus <c>WORD0005</c>: GFM cannot express a horizontal or vertical merge or
///         a nested table, so those are rendered as faithfully as the format allows and the loss is
///         stated with a number. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordTableWriter
{
    /// <summary>
    ///     Renders a table model to a GFM table.
    /// </summary>
    /// <param name="table">The table to render. Must not be null.</param>
    /// <returns>
    ///     The rendered markdown (empty when the table has no cell content, so the caller can skip
    ///     it and emit <c>WORD0003</c>), and the number of cells flattened by a merge or a nested
    ///     table, so the caller can raise a counted structural gap and <c>WORD0005</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Row one is always the header because GFM requires a delimiter row; when Word did not mark
    ///     it a header the caller states that assumption with <c>WORD0004</c>. The column count is
    ///     the widest row, and short rows are padded so the grid stays rectangular. Pure.
    /// </remarks>
    public static (string Markdown, int FlattenedCells) Write(WordTableModel table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var flattened = table.MergedCellCount + table.NestedTableCount;

        // A table with no rows, no columns, or no cell content anywhere contributes nothing; the
        // caller skips it and records WORD0003 rather than emitting an empty header-and-delimiter shell
        var columnCount = table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count);
        if (columnCount == 0)
        {
            return (string.Empty, flattened);
        }

        var rendered = table.Rows
            .Select(row => Enumerable.Range(0, columnCount).Select(column => RenderCell(row, column)).ToList())
            .ToList();

        if (rendered.All(row => row.All(cell => cell.Length == 0)))
        {
            return (string.Empty, flattened);
        }

        var builder = new StringBuilder();
        AppendRow(builder, rendered[0]);
        AppendRow(builder, Enumerable.Repeat("---", columnCount).ToList());
        foreach (var row in rendered.Skip(1))
        {
            AppendRow(builder, row);
        }

        return (builder.ToString(), flattened);
    }

    /// <summary>
    ///     Renders one cell of a row, padding a short row with an empty cell.
    /// </summary>
    /// <param name="row">The row being rendered.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <returns>The cell's markdown, with newlines turned into line breaks.</returns>
    /// <remarks>
    ///     A cell beyond the row's own width is an empty string so the grid stays rectangular. A
    ///     newline inside a cell becomes <c>&lt;br&gt;</c> because a GFM table row is a single line;
    ///     the pipe is already escaped by the inline renderer. Pure.
    /// </remarks>
    private static string RenderCell(IReadOnlyList<WordTableCell> row, int column)
    {
        if (column >= row.Count)
        {
            return string.Empty;
        }

        var content = WordMarkdownWriter.RenderInlines(row[column].Content);
        return content
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Trim();
    }

    /// <summary>
    ///     Appends one table row, pipe-delimited, to the builder.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="cells">The already-rendered cells of the row.</param>
    /// <remarks>Side effect: appends a single line ending in a newline.</remarks>
    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        builder.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
    }
}
