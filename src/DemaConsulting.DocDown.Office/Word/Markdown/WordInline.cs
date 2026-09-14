namespace DocDown.Word.Markdown;

/// <summary>
///     A single inline run of text with the minimal formatting the markdown mapping preserves.
/// </summary>
/// <param name="Text">The literal text of the run. Escaped by the writer unless <paramref name="Raw"/> is set.</param>
/// <param name="Bold">Whether the run is bold, rendered as <c>**…**</c>.</param>
/// <param name="Italic">Whether the run is italic, rendered as <c>*…*</c>.</param>
/// <param name="Href">A hyperlink target, rendered as <c>[text](href)</c>, or <see langword="null"/> for plain text.</param>
/// <param name="Raw">
///     When <see langword="true"/>, <paramref name="Text"/> is already markdown and is emitted
///     verbatim without escaping (used for footnote reference markers such as <c>[^1]</c>).
/// </param>
/// <remarks>
///     Inline formatting is deliberately minimal — bold, italic, and hyperlinks only. A language
///     model consumer gains nothing from colors, fonts, or strikethrough, which would add tokens
///     without adding meaning. Instances are immutable and thread-safe.
/// </remarks>
internal sealed record WordInline(string Text, bool Bold = false, bool Italic = false,
    string? Href = null, bool Raw = false);
