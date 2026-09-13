namespace DocDown.Core;

/// <summary>
///     Controls whether extracted content is emitted as a single document or split into parts.
/// </summary>
/// <remarks>
///     Splitting keeps very large or naturally sectioned documents (multi-sheet workbooks,
///     multi-slide decks) navigable, while a single flow is friendlier for small documents.
///     <see cref="Auto"/> lets Core choose based on the document's structure.
/// </remarks>
public enum ContentSplitMode
{
    /// <summary>
    ///     Let Core decide between a single document and per-part files based on structure.
    /// </summary>
    Auto,

    /// <summary>
    ///     Always emit a single <c>content.md</c>, concatenating parts under headings.
    /// </summary>
    Single,

    /// <summary>
    ///     Always emit an index <c>content.md</c> with each part in its own file under <c>parts/</c>.
    /// </summary>
    PerPart
}
