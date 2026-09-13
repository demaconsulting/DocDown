using System.Text;

namespace DocDown.Word.Markdown;

/// <summary>
///     Renders a <see cref="WordDocumentModel"/> — or any block sequence within it — to a single
///     markdown flow.
/// </summary>
/// <remarks>
///     <para>
///         This unit is the whole markdown mapping, and it is 100% testable from a hand-built model
///         with no document behind it. It is deliberately presentational —
///         it emits markdown and nothing else. The diagnostics and gaps that describe a table's
///         flattening, a tracked-change decision, or an omitted header live with the extractor,
///         which has the sink; the writer only shapes text.
///     </para>
///     <para>
///         Inline formatting is intentionally minimal (bold, italic, hyperlinks) and every literal
///         markdown-significant character in text is escaped, because the consumer is a language
///         model that gains nothing from richer formatting and is harmed by unescaped structure.
///         Stateless and thread-safe; a <see cref="StringBuilder"/> lives for one call.
///     </para>
/// </remarks>
internal static class WordMarkdownWriter
{
    /// <summary>
    ///     Renders a whole document model to one markdown flow, placing document control after the
    ///     title heading and appending comments and footnotes.
    /// </summary>
    /// <param name="model">The model to render. Must not be null.</param>
    /// <param name="imagePaths">A map from an image source reference to the relative path the sink allocated. Must not be null.</param>
    /// <returns>The rendered markdown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="imagePaths"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     The document-control section is placed immediately after the document's title heading and
    ///     before the body — the position of metadata about the whole document — rather than
    ///     interrupting the narrative. Side-effect free.
    /// </remarks>
    public static string Write(WordDocumentModel model, IReadOnlyDictionary<string, string> imagePaths)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(imagePaths);

        var builder = new StringBuilder();

        // Place document control right after a leading title heading, or at the very top when the
        // body opens with something other than a heading. When the body carries no leading heading
        // but the metadata names a title, synthesize one so content.md is self-identifying — the
        // same role the PDF backend's title line plays.
        var body = model.Body;
        var hasLeadingHeading = body.Count > 0 && body[0].Kind == WordBlockKind.Heading;

        if (!hasLeadingHeading && model.Title is { Length: > 0 } title)
        {
            builder.Append("# ").Append(Escape(title)).Append("\n\n");
            AppendDocumentControl(builder, model.DocumentControl, imagePaths);
            AppendBlocks(builder, body, imagePaths);
        }
        else
        {
            var insertIndex = hasLeadingHeading ? 1 : 0;
            AppendBlocks(builder, body.Take(insertIndex).ToList(), imagePaths);
            AppendDocumentControl(builder, model.DocumentControl, imagePaths);
            AppendBlocks(builder, body.Skip(insertIndex).ToList(), imagePaths);
        }

        AppendComments(builder, model.Comments);
        AppendFootnotes(builder, model.Footnotes);

        return builder.ToString();
    }

    /// <summary>
    ///     Renders a block sequence to markdown, used for the body, one per-part section, and each
    ///     document-control subsection.
    /// </summary>
    /// <param name="blocks">The blocks to render. Must not be null.</param>
    /// <param name="imagePaths">A map from an image source reference to the relative path the sink allocated. Must not be null.</param>
    /// <returns>The rendered markdown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> or <paramref name="imagePaths"/> is <see langword="null"/>.</exception>
    /// <remarks>Side-effect free.</remarks>
    public static string WriteBlocks(IReadOnlyList<WordBlock> blocks, IReadOnlyDictionary<string, string> imagePaths)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(imagePaths);

        var builder = new StringBuilder();
        AppendBlocks(builder, blocks, imagePaths);
        return builder.ToString();
    }

    /// <summary>
    ///     Renders a sequence of inline runs to markdown, escaping literal text and applying the
    ///     minimal inline formatting the mapping preserves.
    /// </summary>
    /// <param name="inlines">The inline runs to render, or <see langword="null"/> for none.</param>
    /// <returns>The rendered inline markdown.</returns>
    /// <remarks>
    ///     Shared by the table writer so a cell and a paragraph escape and format identically. A raw
    ///     run is emitted verbatim so a footnote marker or a hard break survives escaping. Pure.
    /// </remarks>
    internal static string RenderInlines(IReadOnlyList<WordInline>? inlines)
    {
        if (inlines is null || inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            if (inline.Raw)
            {
                // A raw run is already markdown (a footnote marker or a hard break); never escape it
                builder.Append(inline.Text);
                continue;
            }

            var text = Escape(inline.Text);
            if (inline.Italic)
            {
                text = "*" + text + "*";
            }

            if (inline.Bold)
            {
                text = "**" + text + "**";
            }

            if (inline.Href is { Length: > 0 } href)
            {
                text = "[" + text + "](" + href + ")";
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Escapes every literal markdown-significant character in a text run.
    /// </summary>
    /// <param name="text">The literal text to escape.</param>
    /// <returns>The text with each significant character backslash-escaped.</returns>
    /// <remarks>
    ///     Escaping the full set keeps a stray <c>*</c>, <c>|</c>, or <c>#</c> in document text from
    ///     being read as structure by a downstream markdown parser. Pure.
    /// </remarks>
    internal static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (EscapedCharacters.Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>The literal characters escaped in inline text.</summary>
    /// <remarks>
    ///     The markdown-structural set: escaping them keeps document prose from being reinterpreted
    ///     as headings, emphasis, links, list markers, or table separators.
    /// </remarks>
    private static readonly HashSet<char> EscapedCharacters =
        ['\\', '`', '*', '_', '{', '}', '[', ']', '(', ')', '#', '+', '-', '.', '!', '|'];

    /// <summary>
    ///     Appends a block sequence to the builder.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="blocks">The blocks to render.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <remarks>
    ///     Tracks the previous block kind so a run of list items is separated from the surrounding
    ///     paragraphs by a single blank line without a blank line between the items themselves. Side
    ///     effect: appends to <paramref name="builder"/>.
    /// </remarks>
    private static void AppendBlocks(
        StringBuilder builder, IReadOnlyList<WordBlock> blocks, IReadOnlyDictionary<string, string> imagePaths)
    {
        WordBlockKind? previous = null;
        foreach (var block in blocks)
        {
            // Close a preceding list run with a blank line before a non-list block
            if (previous == WordBlockKind.ListItem && block.Kind != WordBlockKind.ListItem)
            {
                builder.Append('\n');
            }

            AppendBlock(builder, block, imagePaths);
            previous = block.Kind;
        }

        // A list that runs to the end of the sequence still needs its trailing blank line
        if (previous == WordBlockKind.ListItem)
        {
            builder.Append('\n');
        }
    }

    /// <summary>
    ///     Appends a single block to the builder.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="block">The block to render.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <remarks>Side effect: appends to <paramref name="builder"/>.</remarks>
    private static void AppendBlock(
        StringBuilder builder, WordBlock block, IReadOnlyDictionary<string, string> imagePaths)
    {
        switch (block.Kind)
        {
            case WordBlockKind.Heading:
                var level = Math.Clamp(block.HeadingLevel, 1, 6);
                builder.Append('#', level).Append(' ').Append(RenderInlines(block.Inlines)).Append("\n\n");
                break;

            case WordBlockKind.Paragraph:
                var paragraph = RenderInlines(block.Inlines);
                if (paragraph.Length > 0)
                {
                    builder.Append(paragraph).Append("\n\n");
                }

                break;

            case WordBlockKind.ListItem:
                var info = block.List ?? new WordListInfo(0, false);
                builder.Append(' ', Math.Max(0, info.Level) * 2)
                    .Append(info.Ordered ? "1. " : "- ")
                    .Append(RenderInlines(block.Inlines))
                    .Append('\n');
                break;

            case WordBlockKind.Table:
                if (block.Table is { } table)
                {
                    var (markdown, _) = WordTableWriter.Write(table);
                    if (markdown.Length > 0)
                    {
                        builder.Append(markdown).Append('\n');
                    }
                }

                break;

            case WordBlockKind.Image:
                AppendImage(builder, block.Image, imagePaths);
                break;

            case WordBlockKind.PageBreak:
                builder.Append("---\n\n");
                break;

            case WordBlockKind.DocumentControl:
                // Document-control subsections are rendered through AppendDocumentControl, never here
                break;

            default:
                break;
        }
    }

    /// <summary>
    ///     Appends an image link for an image block, using the path the sink allocated.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="image">The image reference, or <see langword="null"/>.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <remarks>
    ///     Emits a link only when the sink returned a path for the image, so a suppressed or
    ///     deduplicated-away image never leaves a link pointing at nothing. The alt text is the
    ///     image's descriptive text only when a genuinely descriptive source was chosen; a heading or
    ///     a bare media name yields a neutral <c>image</c> placeholder rather than implying a
    ///     description the document never gave. Side effect: appends.
    /// </remarks>
    private static void AppendImage(
        StringBuilder builder, WordImageRef? image, IReadOnlyDictionary<string, string> imagePaths)
    {
        if (image?.SourceRef is not { } sourceRef || !imagePaths.TryGetValue(sourceRef, out var path))
        {
            return;
        }

        var alt = Escape(image.AltText ?? "image");
        builder.Append("![").Append(alt).Append("](").Append(path).Append(")\n\n");
    }

    /// <summary>
    ///     Appends the <c>## Document Control</c> section when any subsection survived.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="sections">The surviving document-control subsections.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <remarks>Side effect: appends to <paramref name="builder"/>.</remarks>
    private static void AppendDocumentControl(
        StringBuilder builder, IReadOnlyList<WordDocumentControlSection> sections,
        IReadOnlyDictionary<string, string> imagePaths)
    {
        if (sections.Count == 0)
        {
            return;
        }

        builder.Append("## Document Control\n\n");
        foreach (var section in sections)
        {
            builder.Append("### ").Append(Escape(section.Label)).Append("\n\n");
            AppendBlocks(builder, section.Blocks, imagePaths);
        }
    }

    /// <summary>
    ///     Appends the <c>## Comments</c> section when the document has comments.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="comments">The document comments.</param>
    /// <remarks>Side effect: appends to <paramref name="builder"/>.</remarks>
    private static void AppendComments(StringBuilder builder, IReadOnlyList<WordComment> comments)
    {
        if (comments.Count == 0)
        {
            return;
        }

        builder.Append("## Comments\n\n");
        foreach (var comment in comments)
        {
            builder.Append("- ");
            if (comment.Author is { Length: > 0 } author)
            {
                builder.Append("**").Append(Escape(author)).Append("**: ");
            }

            builder.Append(RenderInlines(comment.Content)).Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>
    ///     Appends the <c>## Footnotes</c> section when the document has footnotes.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="footnotes">The footnote bodies, indexed one-based in output.</param>
    /// <remarks>Side effect: appends to <paramref name="builder"/>.</remarks>
    private static void AppendFootnotes(StringBuilder builder, IReadOnlyList<IReadOnlyList<WordInline>> footnotes)
    {
        if (footnotes.Count == 0)
        {
            return;
        }

        builder.Append("## Footnotes\n\n");
        for (var index = 0; index < footnotes.Count; index++)
        {
            builder.Append("[^").Append(index + 1).Append("]: ").Append(RenderInlines(footnotes[index])).Append('\n');
        }

        builder.Append('\n');
    }
}
