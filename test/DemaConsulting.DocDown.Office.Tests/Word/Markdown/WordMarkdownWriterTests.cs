using DemaConsulting.DocDown.Word.Markdown;

namespace DemaConsulting.DocDown.Office.Tests.Word.Markdown;

/// <summary>
///     Unit tests for <see cref="WordMarkdownWriter"/>, covering the block and inline mapping from a
///     hand-built model with no document behind it.
/// </summary>
public class WordMarkdownWriterTests
{
    /// <summary>An empty image path map for text-only models.</summary>
    private static readonly Dictionary<string, string> NoImages = new(StringComparer.Ordinal);

    /// <summary>
    ///     Proves headings render as the matching number of hash marks.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_Headings_RendersHashLevels()
    {
        var model = Model(
        [
            new WordBlock(WordBlockKind.Heading, [new WordInline("Top")], HeadingLevel: 1),
            new WordBlock(WordBlockKind.Heading, [new WordInline("Sub")], HeadingLevel: 2)
        ]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("# Top", markdown, StringComparison.Ordinal);
        Assert.Contains("## Sub", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves ordered and bulleted list items render with the correct markers and indentation.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_OrderedAndBulletedLists_RendersMarkers()
    {
        var model = Model(
        [
            new WordBlock(WordBlockKind.ListItem, [new WordInline("Bullet")], List: new WordListInfo(0, false)),
            new WordBlock(WordBlockKind.ListItem, [new WordInline("Nested")], List: new WordListInfo(1, true))
        ]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("- Bullet", markdown, StringComparison.Ordinal);
        Assert.Contains("  1. Nested", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves markdown-significant characters in text are escaped.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_SpecialCharacters_AreEscaped()
    {
        var model = Model([new WordBlock(WordBlockKind.Paragraph, [new WordInline("a*b_c|d#e")])]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("a\\*b\\_c\\|d\\#e", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves bold, italic, and hyperlink inline formatting renders.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_BoldItalicAndLink_RenderInline()
    {
        var model = Model(
        [
            new WordBlock(WordBlockKind.Paragraph,
            [
                new WordInline("bold", Bold: true),
                new WordInline("italic", Italic: true),
                new WordInline("link", Href: "https://example.com")
            ])
        ]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("**bold**", markdown, StringComparison.Ordinal);
        Assert.Contains("*italic*", markdown, StringComparison.Ordinal);
        Assert.Contains("[link](https://example.com)", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an image with descriptive text renders its description as the markdown alt text.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_DescriptiveImage_RendersDescriptionAsAltText()
    {
        var image = new WordImageRef([1, 2, 3], "image/png", "logo", "/word/media/image1.png",
            AltText: "logo", Description: "logo", DescriptionSource: "description");
        var model = Model([new WordBlock(WordBlockKind.Image, Image: image)]);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal) { ["/word/media/image1.png"] = "images/0001-logo.png" };

        var markdown = WordMarkdownWriter.Write(model, paths);

        Assert.Contains("![logo](images/0001-logo.png)", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an image named only from a heading keeps a neutral alt text rather than asserting
    ///     the heading as a description.
    /// </summary>
    /// <remarks>
    ///     The heading may seed the file name (so the link points at <c>0001-references.png</c>), but
    ///     presenting "References" as the picture's alt text would imply a description the document
    ///     never gave, so the alt text stays the neutral <c>image</c>.
    /// </remarks>
    [Fact]
    public void WordMarkdownWriter_Write_HeadingNamedImage_KeepsNeutralAltText()
    {
        var image = new WordImageRef([1, 2, 3], "image/png", "references", "/word/media/image1.png");
        var model = Model([new WordBlock(WordBlockKind.Image, Image: image)]);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal) { ["/word/media/image1.png"] = "images/0001-references.png" };

        var markdown = WordMarkdownWriter.Write(model, paths);

        Assert.Contains("![image](images/0001-references.png)", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("![references]", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves comments present on the model are not rendered into the content flow.
    /// </summary>
    /// <remarks>
    ///     A reviewer's remark is commentary about the document rather than part of it, and
    ///     interleaving it into <c>content.md</c> left a consumer unable to tell the author's words
    ///     from a reviewer's. The comments still travel on the model — the emitter reports them to
    ///     Core as review comments — so this asserts the writer's silence rather than the model's
    ///     emptiness.
    /// </remarks>
    [Fact]
    public void WordMarkdownWriter_Write_Comments_NotRenderedIntoContent()
    {
        var model = Model(
            [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body")])],
            comments: [new WordComment("Reviewer", [new WordInline("Please clarify")], "§Scope")]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("Body", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("## Comments", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Please clarify", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Reviewer", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the literal renderer returns the text as the document records it, escaping nothing,
    ///     and returns empty for an absent or empty run sequence.
    /// </summary>
    /// <remarks>
    ///     The escaped and literal forms exist for two different consumers: <c>content.md</c>, which
    ///     is markdown and needs escaping, and the review comments Core carries into
    ///     <c>manifest.json</c>, whose contract is the text as the document records it. Asserting the
    ///     markdown-significant characters pass through untouched is what keeps the two from being
    ///     quietly collapsed back into one.
    /// </remarks>
    [Fact]
    public void WordMarkdownWriter_RenderPlainText_MarkdownCharacters_AreNotEscaped()
    {
        var inlines = new[] { new WordInline("Flag *urgent* "), new WordInline("[see §4].") };

        var text = WordMarkdownWriter.RenderPlainText(inlines);

        Assert.Equal("Flag *urgent* [see §4].", text);
        Assert.DoesNotContain("\\", text, StringComparison.Ordinal);

        // The same runs rendered as markdown do escape, so the two forms are genuinely different
        Assert.Contains("\\*", WordMarkdownWriter.RenderInlines(inlines), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the literal renderer treats an absent or empty run sequence as empty text rather
    ///     than throwing.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_RenderPlainText_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, WordMarkdownWriter.RenderPlainText(null));
        Assert.Equal(string.Empty, WordMarkdownWriter.RenderPlainText([]));
    }

    /// <summary>
    ///     Proves footnotes render into a footnotes section with numbered definitions.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_Footnotes_RendersFootnotesSection()
    {
        var model = Model(
            [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body"), new WordInline("[^1]", Raw: true)])],
            footnotes: [[new WordInline("The footnote text")]]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        Assert.Contains("[^1]", markdown, StringComparison.Ordinal);
        Assert.Contains("## Footnotes", markdown, StringComparison.Ordinal);
        Assert.Contains("[^1]: The footnote text", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the document-control section is placed after the title heading and before the body.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_Write_DocumentControl_PlacedAfterTitleHeading()
    {
        var model = Model(
        [
            new WordBlock(WordBlockKind.Heading, [new WordInline("Title")], HeadingLevel: 1),
            new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body paragraph")])
        ],
        control:
        [
            new WordDocumentControlSection("Header", [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Revision 3.0")])])
        ]);

        var markdown = WordMarkdownWriter.Write(model, NoImages);

        var titleIndex = markdown.IndexOf("# Title", StringComparison.Ordinal);
        var controlIndex = markdown.IndexOf("## Document Control", StringComparison.Ordinal);
        var bodyIndex = markdown.IndexOf("Body paragraph", StringComparison.Ordinal);
        Assert.True(titleIndex >= 0 && controlIndex > titleIndex && bodyIndex > controlIndex,
            "document control must sit between the title heading and the body");
        Assert.Contains("### Header", markdown, StringComparison.Ordinal);
        Assert.Contains("Revision 3\\.0", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds a model with the given body and optional sections.
    /// </summary>
    /// <param name="body">The body blocks.</param>
    /// <param name="control">The document-control subsections, or none.</param>
    /// <param name="comments">The comments, or none.</param>
    /// <param name="footnotes">The footnotes, or none.</param>
    /// <param name="title">The title, or none.</param>
    /// <returns>The model.</returns>
    private static WordDocumentModel Model(
        IReadOnlyList<WordBlock> body,
        IReadOnlyList<WordDocumentControlSection>? control = null,
        IReadOnlyList<WordComment>? comments = null,
        IReadOnlyList<IReadOnlyList<WordInline>>? footnotes = null,
        string? title = null) =>
        new(body, control ?? [], comments ?? [], footnotes ?? [], title, null, null, 0, 0, 0, 0, 0);
}
