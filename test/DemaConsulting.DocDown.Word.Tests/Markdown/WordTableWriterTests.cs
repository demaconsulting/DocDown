using DocDown.Word.Markdown;

namespace DemaConsulting.DocDown.Word.Tests.Markdown;

/// <summary>
///     Unit tests for <see cref="WordTableWriter"/>, covering the table rules from a hand-built model
///     with no document behind it.
/// </summary>
public class WordTableWriterTests
{
    /// <summary>
    ///     Proves a full table renders a GFM table with a header row, a delimiter row, and its body.
    /// </summary>
    [Fact]
    public void WordTableWriter_Write_FullTable_ProducesGfmWithHeaderAndAlignment()
    {
        // Arrange: a two-column table with a marked header row and two body rows
        var table = new WordTableModel(
            [
                Row("Component", "Status"),
                Row("Alpha", "Ready"),
                Row("Beta", "Pending")
            ],
            FirstRowIsHeader: true, MergedCellCount: 0, NestedTableCount: 0);

        // Act
        var (markdown, flattened) = WordTableWriter.Write(table);

        // Assert: the header, the delimiter, and the body are all present, nothing flattened
        Assert.Contains("| Component | Status |", markdown, StringComparison.Ordinal);
        Assert.Contains("| --- | --- |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Alpha | Ready |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Beta | Pending |", markdown, StringComparison.Ordinal);
        Assert.Equal(0, flattened);
    }

    /// <summary>
    ///     Proves a short row is padded with empty cells so the grid stays rectangular.
    /// </summary>
    [Fact]
    public void WordTableWriter_Write_ShortRow_PadsWithEmptyCells()
    {
        // Arrange: a header of three columns and a body row of one cell
        var table = new WordTableModel(
            [Row("A", "B", "C"), Row("only")],
            FirstRowIsHeader: true, MergedCellCount: 0, NestedTableCount: 0);

        // Act
        var (markdown, _) = WordTableWriter.Write(table);

        // Assert: the short row is padded to three columns
        Assert.Contains("| only |  |  |", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a cell's newline becomes a line break and its pipe is escaped.
    /// </summary>
    [Fact]
    public void WordTableWriter_Write_CellWithNewlineAndPipe_EscapesAndBreaks()
    {
        // Arrange: a single cell holding a newline and a pipe
        var cell = new WordTableCell([new WordInline("line one\nline two | end")]);
        var table = new WordTableModel(
            [[cell]], FirstRowIsHeader: false, MergedCellCount: 0, NestedTableCount: 0);

        // Act
        var (markdown, _) = WordTableWriter.Write(table);

        // Assert: the newline is a break and the pipe is escaped
        Assert.Contains("line one<br>line two \\| end", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an empty table renders nothing so the caller can skip it.
    /// </summary>
    [Fact]
    public void WordTableWriter_Write_EmptyTable_ReturnsEmpty()
    {
        // Arrange: a table whose cells hold no content
        var table = new WordTableModel(
            [[new WordTableCell([]), new WordTableCell([])]],
            FirstRowIsHeader: false, MergedCellCount: 0, NestedTableCount: 0);

        // Act
        var (markdown, _) = WordTableWriter.Write(table);

        // Assert: nothing is rendered
        Assert.Equal(string.Empty, markdown);
    }

    /// <summary>
    ///     Proves merged and nested cells are reported as a flattened count.
    /// </summary>
    [Fact]
    public void WordTableWriter_Write_MergedAndNestedCells_CountsFlattened()
    {
        // Arrange: a table declaring two merged cells and one nested table
        var table = new WordTableModel(
            [Row("Header"), Row("Body")],
            FirstRowIsHeader: true, MergedCellCount: 2, NestedTableCount: 1);

        // Act
        var (_, flattened) = WordTableWriter.Write(table);

        // Assert: the flattened count is the sum of merges and nested tables
        Assert.Equal(3, flattened);
    }

    /// <summary>
    ///     Builds a table row from cell texts.
    /// </summary>
    /// <param name="cells">The cell texts.</param>
    /// <returns>The row.</returns>
    private static IReadOnlyList<WordTableCell> Row(params string[] cells) =>
        cells.Select(text => new WordTableCell([new WordInline(text)])).ToList();
}
