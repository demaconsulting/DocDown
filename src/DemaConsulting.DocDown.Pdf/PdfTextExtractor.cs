using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DocDown.Pdf;

/// <summary>
///     Turns the glyphs of a PDF page into markdown paragraphs in reading order, inferring headings
///     from tagged structure and placing the links to the images extracted from the same page.
/// </summary>
/// <remarks>
///     <para>
///         A PDF stores positioned glyphs, not text: the order they appear in the content stream is
///         the order the producer chose to draw them, which for a two-column page is routinely not
///         the order a human reads them. PdfPig's <c>Page.Text</c> is exactly that draw order, and
///         its own documentation warns against using it; this unit therefore never touches it.
///         Instead it groups letters into words, words into blocks, and orders the blocks spatially,
///         so a multi-column page reads as a human would read it rather than as it was painted.
///     </para>
///     <para>
///         Heading inference is deliberately conservative. A tagged PDF marks its headings, and those
///         marks are honored; an untagged PDF — the overwhelmingly common case — yields plain
///         paragraphs rather than headings guessed from font size, because a wrong heading is worse
///         for a downstream consumer than no heading at all.
///     </para>
///     <para>
///         Segmentation is heuristic and can legitimately produce nothing on a page whose glyphs
///         defeat it, so a page that yields no blocks falls back to PdfPig's content-order text
///         extractor before being reported as empty. Stateless and thread-safe; the markdown builder
///         lives for the duration of a single call.
///     </para>
/// </remarks>
internal static class PdfTextExtractor
{
    /// <summary>The marked-content tags that denote a heading, mapped to their markdown level.</summary>
    /// <remarks>
    ///     Covers the PDF structure tags for headings plus the document title. Ordinary levels map
    ///     one-to-one; the untyped <c>H</c> tag maps to level two because it is a heading of
    ///     unspecified rank and the document title already owns level one.
    /// </remarks>
    private static readonly Dictionary<string, int> HeadingTagLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = 1,
        ["H"] = 2,
        ["H1"] = 1,
        ["H2"] = 2,
        ["H3"] = 3,
        ["H4"] = 4,
        ["H5"] = 5,
        ["H6"] = 6
    };

    /// <summary>
    ///     Builds the markdown for a document's selected pages.
    /// </summary>
    /// <param name="pages">The pages to render, already restricted to any requested range. Must not be null.</param>
    /// <param name="documentTitle">The document title to head the output with, or <see langword="null"/> when unknown.</param>
    /// <param name="images">The images extracted from the same pages, linked under the page they came from. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     The rendered markdown and whether any glyphs were present at all, so the caller can tell an
    ///     empty document from one whose text simply could not be laid out.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pages"/> or <paramref name="images"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Emits a <c>&lt;!-- docdown:page N --&gt;</c> marker before each page's content, matching the
    ///     marker convention Core passes through untouched, so a consumer can map any passage back to
    ///     its source page. Pure apart from reading the pages.
    /// </remarks>
    internal static PdfTextResult Extract(
        IReadOnlyList<Page> pages, string? documentTitle,
        IReadOnlyList<PdfExtractedImage> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(images);

        var markdown = new StringBuilder();
        var anyGlyphs = false;

        // Head the document with its own title when the PDF declares one, so content.md is self-identifying
        if (!string.IsNullOrWhiteSpace(documentTitle))
        {
            markdown.Append("# ").Append(documentTitle.Trim()).Append("\n\n");
        }

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            anyGlyphs |= page.Letters.Count > 0;

            // Mark the page boundary before its content so passages remain traceable to a source page
            markdown.Append("<!-- docdown:page ")
                .Append(page.Number.ToString(CultureInfo.InvariantCulture))
                .Append(" -->\n\n");

            AppendPageText(markdown, page);
            AppendPageImages(markdown, page.Number, images);
        }

        return new PdfTextResult(markdown.ToString(), anyGlyphs);
    }

    /// <summary>
    ///     Appends one page's text as markdown paragraphs and headings.
    /// </summary>
    /// <param name="markdown">The builder to append to.</param>
    /// <param name="page">The page to render.</param>
    /// <remarks>
    ///     Uses the spatial pipeline first and the content-order extractor only as a fallback, because
    ///     the former produces paragraph boundaries markdown needs while the latter produces a single
    ///     undifferentiated flow. Side effect: appends to <paramref name="markdown"/>.
    /// </remarks>
    private static void AppendPageText(StringBuilder markdown, Page page)
    {
        // A page with no glyphs has no text to render; the caller reports the absence
        if (page.Letters.Count == 0)
        {
            return;
        }

        var headings = CollectHeadingTexts(page);
        var blocks = SegmentIntoReadingOrder(page);

        // Segmentation is heuristic; when it yields nothing, fall back rather than lose the text
        if (blocks.Count == 0)
        {
            AppendParagraph(markdown, ContentOrderTextExtractor.GetText(page, true));
            return;
        }

        foreach (var block in blocks)
        {
            var text = Normalize(block.Text);
            if (text.Length == 0)
            {
                continue;
            }

            // Honor a heading the document itself declared; never guess one from appearance
            if (headings.TryGetValue(text, out var level))
            {
                markdown.Append('#', level).Append(' ').Append(text).Append("\n\n");
                continue;
            }

            AppendParagraph(markdown, text);
        }
    }

    /// <summary>
    ///     Groups a page's letters into blocks and orders them as a reader would read them.
    /// </summary>
    /// <param name="page">The page to segment.</param>
    /// <returns>The page's text blocks in reading order, or an empty list when segmentation yields none.</returns>
    /// <remarks>
    ///     Words come from nearest-neighbour grouping, blocks from the Docstrum algorithm, and the
    ///     final sequence from unsupervised reading-order detection — the combination that turns a
    ///     two-column page into two sequential columns rather than interleaved lines. Pure apart from
    ///     reading the page.
    /// </remarks>
    private static IReadOnlyList<TextBlock> SegmentIntoReadingOrder(Page page)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
        if (words.Count == 0)
        {
            return [];
        }

        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        if (blocks.Count == 0)
        {
            return [];
        }

        return UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .OrderBy(block => block.ReadingOrder)
            .ToList();
    }

    /// <summary>
    ///     Collects the normalized text of every marked-content heading on a page.
    /// </summary>
    /// <param name="page">The page whose tagged structure is inspected.</param>
    /// <returns>A map from normalized heading text to its markdown level; empty for an untagged page.</returns>
    /// <remarks>
    ///     Matching on text rather than on geometry lets an independently segmented block be
    ///     recognized as a heading without having to reconcile two different groupings of the same
    ///     glyphs. An untagged page contributes nothing, which is why it degrades to plain
    ///     paragraphs. Pure apart from reading the page.
    /// </remarks>
    private static Dictionary<string, int> CollectHeadingTexts(Page page)
    {
        var headings = new Dictionary<string, int>(StringComparer.Ordinal);

        // Walk the marked-content tree; tagged structure nests, so children must be visited too
        var pending = new Stack<MarkedContentElement>();
        foreach (var element in page.GetMarkedContents())
        {
            pending.Push(element);
        }

        while (pending.Count > 0)
        {
            var element = pending.Pop();
            foreach (var child in element.Children)
            {
                pending.Push(child);
            }

            if (element.Tag is null || !HeadingTagLevels.TryGetValue(element.Tag, out var level))
            {
                continue;
            }

            var text = Normalize(string.Concat(element.Letters.Select(letter => letter.Value)));
            if (text.Length > 0)
            {
                headings[text] = level;
            }
        }

        return headings;
    }

    /// <summary>
    ///     Appends the markdown image links for one page.
    /// </summary>
    /// <param name="markdown">The builder to append to.</param>
    /// <param name="pageNumber">The 1-based page whose images are linked.</param>
    /// <param name="images">All extracted images, filtered here by page.</param>
    /// <remarks>
    ///     Uses the relative paths the sink returned rather than constructing any path, which is what
    ///     keeps every link resolvable and every artifact contained. Side effect: appends to
    ///     <paramref name="markdown"/>.
    /// </remarks>
    private static void AppendPageImages(StringBuilder markdown, int pageNumber, IReadOnlyList<PdfExtractedImage> images)
    {
        foreach (var image in images)
        {
            if (image.PageNumber != pageNumber)
            {
                continue;
            }

            markdown.Append("![").Append(image.Description).Append("](").Append(image.RelativePath).Append(")\n\n");
        }
    }

    /// <summary>
    ///     Appends one paragraph of text, if it has any content.
    /// </summary>
    /// <param name="markdown">The builder to append to.</param>
    /// <param name="text">The paragraph text.</param>
    /// <remarks>Centralizes the blank-line separation markdown paragraphs require. Side effect: appends.</remarks>
    private static void AppendParagraph(StringBuilder markdown, string text)
    {
        var normalized = Normalize(text);
        if (normalized.Length > 0)
        {
            markdown.Append(normalized).Append("\n\n");
        }
    }

    /// <summary>
    ///     Collapses a block's internal whitespace into single spaces and trims it.
    /// </summary>
    /// <param name="text">The text to normalize, which may be <see langword="null"/>.</param>
    /// <returns>The normalized text, which may be empty.</returns>
    /// <remarks>
    ///     A block's line breaks come from the page's physical layout, not from the author's
    ///     paragraphing, so preserving them would encode page geometry into the markdown. Collapsing
    ///     them also makes heading text comparable between the tagged structure and the segmented
    ///     blocks. Pure.
    /// </remarks>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}

/// <summary>
///     The outcome of rendering a document's text.
/// </summary>
/// <param name="Markdown">The rendered markdown, which may contain only page markers.</param>
/// <param name="AnyGlyphs">
///     <see langword="true"/> when at least one selected page contained a glyph; otherwise
///     <see langword="false"/>.
/// </param>
/// <remarks>
///     <paramref name="AnyGlyphs"/> is reported separately from the markdown because the two answer
///     different questions: whether the document has a text layer at all, and what could be made of
///     it. A scanned document has neither, and the distinction is what lets the caller explain that
///     honestly rather than emitting a silently empty document. Immutable and thread-safe.
/// </remarks>
internal sealed record PdfTextResult(string Markdown, bool AnyGlyphs);
