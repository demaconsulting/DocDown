namespace DocDown.Word.Markdown;

/// <summary>
///     The list membership of a <see cref="WordBlockKind.ListItem"/> block.
/// </summary>
/// <param name="Level">The zero-based nesting level, indented two spaces per level in the output.</param>
/// <param name="Ordered">
///     <see langword="true"/> for an ordered list (rendered <c>1. </c>), <see langword="false"/>
///     for a bulleted list (rendered <c>- </c>). Determined from the <c>numbering.xml</c>
///     <c>w:numFmt</c>: any format other than <c>bullet</c> is ordered.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordListInfo(int Level, bool Ordered);
