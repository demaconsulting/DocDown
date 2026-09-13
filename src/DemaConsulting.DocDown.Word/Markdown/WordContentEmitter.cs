using System.Globalization;
using DocDown.Core;

namespace DocDown.Word.Markdown;

/// <summary>
///     Emits a rendered <see cref="WordDocumentModel"/> through the extraction sink, reporting the
///     produced inventory and any extraction notes in one place.
/// </summary>
/// <remarks>
///     <para>
///         The reader populates the model and hands it here, so every decision about what reaches
///         the output is made once, in a unit that can be exercised from a hand-built model with no
///         document behind it. Only how the model and its image bytes are obtained lives upstream;
///         everything downstream of the model is this one unit.
///     </para>
///     <para>
///         The emitted inventory states what reached the output, including deliberate zero counts for
///         categories a reader genuinely looked for, and a note is reserved for the narrower case
///         where DocDown attempted a step and could not complete it. Performs no filesystem I/O of
///         its own: every byte goes through the sink. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordContentEmitter
{
    /// <summary>
    ///     Emits a model through the sink: images, content, metadata, inventory counts, and any
    ///     extraction notes the model implies.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The document model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the model has been emitted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Side effect: writes content and images and records inventory counts and notes on the
    ///     sink.
    /// </remarks>
    public static async ValueTask EmitAsync(
        IExtractionSink sink, ExtractionOptions options, WordDocumentModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        // Images first: the content renderer places the links the sink allocates for them
        var imagePaths = await WriteImagesAsync(sink, options, model, cancellationToken)
            .ConfigureAwait(false);

        var partCount = await WriteContentAsync(sink, options, model, imagePaths, cancellationToken)
            .ConfigureAwait(false);

        sink.ReportDocumentInfo(new DocumentInfo(model.Title, model.Author, model.ProducerPageCount, partCount));

        ReportContentFeatures(sink, model);

        // Report the document's self-reported metadata for metadata.json when the reader captured it
        if (model.Metadata is { } metadata)
        {
            sink.ReportDocumentMetadata(metadata);
        }

        ReportExtractionNotes(sink, model);
    }

    /// <summary>
    ///     Reports an outline of what the rendered <c>content.md</c> contains, counted from the model.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The document model whose structure is counted.</param>
    /// <remarks>
    ///     <para>
    ///         The summary otherwise states only a character count, which leaves the single most
    ///         valuable thing a Word draft can carry — the author-attributed reviewer comments —
    ///         invisible. An agent asked to find reviewer commentary in a draft holding 53 such
    ///         comments went to a different document and answered by inference, because nothing told
    ///         it the <c>## Comments</c> section existed. Naming the comment count, and how many
    ///         distinct people wrote them, makes that content discoverable for a handful of tokens.
    ///     </para>
    ///     <para>
    ///         The counts come from the model the reader built by walking the document, not from
    ///         scanning the rendered markdown: the model already knows, and a regex over markdown would
    ///         describe a rendering rather than the document. Features whose absence matters to a reader
    ///         — text blocks, comments, distinct comment authors, and footnotes — are marked as looked
    ///         for so a zero count still states plainly that Word extraction inspected them. Side effect:
    ///         records reports on the sink.
    ///     </para>
    /// </remarks>
    private static void ReportContentFeatures(IExtractionSink sink, WordDocumentModel model)
    {
        // Count the body and the surviving document-control subsections together: both are rendered
        // into content.md, so both are things a reader will actually find there
        var blocks = model.Body.Concat(model.DocumentControl.SelectMany(section => section.Blocks)).ToList();
        var textualBlocks = CountTextualBlocks(blocks);

        sink.ReportContentFeature(new ContentFeature(
            "text blocks", textualBlocks, "text block", LookedFor: true));

        sink.ReportContentFeature(new ContentFeature(
            "headings", blocks.Count(block => block.Kind == WordBlockKind.Heading)));
        sink.ReportContentFeature(new ContentFeature(
            "tables", blocks.Count(block => block.Kind == WordBlockKind.Table)));
        sink.ReportContentFeature(new ContentFeature(
            "list items", blocks.Count(block => block.Kind == WordBlockKind.ListItem)));
        sink.ReportContentFeature(new ContentFeature(
            "inline images", blocks.Count(block => block.Kind == WordBlockKind.Image)));
        sink.ReportContentFeature(new ContentFeature(
            "comments", model.Comments.Count, LookedFor: true));

        // Distinct comment authors answer "is this one person's markup or a review?"; an unattributed
        // comment is not counted as a comment author because the document names nobody for it
        sink.ReportContentFeature(new ContentFeature(
            "distinct comment authors",
            model.Comments.Select(comment => comment.Author)
                .Where(author => author is not null)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            "distinct comment author",
            LookedFor: true));

        sink.ReportContentFeature(new ContentFeature(
            "footnotes", model.Footnotes.Count, "footnote", LookedFor: true));
    }

    /// <summary>
    ///     Builds an image hint for a model image reference, always claiming a passthrough with
    ///     unknown pixel dimensions and carrying the chosen description and its source.
    /// </summary>
    /// <param name="image">The image reference to describe.</param>
    /// <returns>The hint to pass to the sink.</returns>
    /// <remarks>
    ///     An Open XML image part stores a complete image file byte-for-byte, so the transform is
    ///     always <see cref="ImageTransform.Passthrough"/>. The width and height stay
    ///     <see langword="null"/> because <c>wp:extent</c> is an EMU display size, not a pixel count;
    ///     reporting it as pixels would be a false provenance claim. The description and its source
    ///     are carried through so Core records them as image metadata; both are absent when only the
    ///     media name was available, so no description is ever invented. Pure.
    /// </remarks>
    internal static ImageHint HintFor(WordImageRef image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new ImageHint(
            PreferredName: image.PreferredName,
            MediaType: image.MediaType,
            WidthPx: null,
            HeightPx: null,
            SourcePage: null,
            SourceRef: image.SourceRef,
            Transform: ImageTransform.Passthrough,
            Description: image.Description,
            DescriptionSource: image.DescriptionSource);
    }

    /// <summary>
    ///     Writes every embedded image the model carries and builds the source-reference-to-path map.
    /// </summary>
    /// <param name="sink">The sink to write images through.</param>
    /// <param name="options">The effective options, consulted for suppression and force-PNG.</param>
    /// <param name="model">The model whose image blocks are written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The map from an image source reference to its written path.</returns>
    /// <remarks>
    ///     When images are suppressed nothing is written and Core records the suppression itself. A
    ///     force-PNG request is recorded as a note when files were written in their source encoding,
    ///     because this package ships no imaging stack and therefore cannot re-encode them. Side
    ///     effect: writes images and records reports on the sink.
    /// </remarks>
    private static async ValueTask<Dictionary<string, string>> WriteImagesAsync(
        IExtractionSink sink, ExtractionOptions options, WordDocumentModel model, CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit must not read, write, or count anything here
        if (!options.IncludeEmbeddedImages)
        {
            return paths;
        }

        var images = CollectImages(model);
        if (images.Count == 0)
        {
            return paths;
        }

        var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new MemoryStream(image.Bytes, writable: false);
            var path = await sink.AddImageAsync(stream, HintFor(image), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (image.SourceRef is { } sourceRef)
            {
                paths[sourceRef] = path;
            }

            writtenPaths.Add(path);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && writtenPaths.Count > 0)
        {
            ReportForcePngNote(sink, writtenPaths.Count);
        }

        return paths;
    }

    /// <summary>
    ///     Writes the document content, honoring the per-part split mode.
    /// </summary>
    /// <param name="sink">The sink to write content through.</param>
    /// <param name="options">The effective options, consulted for the split mode.</param>
    /// <param name="model">The model to render.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of parts written, or <see langword="null"/> for a single flow.</returns>
    /// <remarks>
    ///     <see cref="ContentSplitMode.Auto"/> and <see cref="ContentSplitMode.Single"/> produce a
    ///     single flow because a Word document is one continuous flow; <see cref="ContentSplitMode.PerPart"/>
    ///     splits at every <c>Heading 1</c>. Core does not apply the mode for the extractor, so it is
    ///     inspected here. Side effect: writes content on the sink.
    /// </remarks>
    private static async ValueTask<int?> WriteContentAsync(
        IExtractionSink sink, ExtractionOptions options, WordDocumentModel model,
        IReadOnlyDictionary<string, string> imagePaths, CancellationToken cancellationToken)
    {
        var parts = options.ContentSplit == ContentSplitMode.PerPart
            ? BuildParts(model, imagePaths)
            : null;

        // Fall back to a single flow when a per-part request has no Heading 1 boundary to split on,
        // so the whole document is never lost to an empty parts list
        if (parts is not { Count: > 0 })
        {
            var flow = WordMarkdownWriter.Write(model, imagePaths);
            await sink.WriteContentAsync(flow, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var ordinal = 1;
        foreach (var (title, markdown) in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Section, ordinal, title), markdown, cancellationToken)
                .ConfigureAwait(false);
            ordinal++;
        }

        return parts.Count;
    }

    /// <summary>
    ///     Splits a model's body into per-part sections at every <c>Heading 1</c> boundary.
    /// </summary>
    /// <param name="model">The model to split.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <returns>The parts as title-and-markdown pairs, empty when the body has no <c>Heading 1</c>.</returns>
    /// <remarks>
    ///     Leading matter before the first <c>Heading 1</c> becomes part one, titled from the
    ///     document title or "Front matter", and carries the document-control section. Comments and
    ///     footnotes are appended to the final part so they are never lost to the split. Pure.
    /// </remarks>
    private static List<(string Title, string Markdown)> BuildParts(
        WordDocumentModel model, IReadOnlyDictionary<string, string> imagePaths)
    {
        var body = model.Body;
        var boundaries = new List<int>();
        for (var index = 0; index < body.Count; index++)
        {
            if (body[index].Kind == WordBlockKind.Heading && body[index].HeadingLevel <= 1)
            {
                boundaries.Add(index);
            }
        }

        if (boundaries.Count == 0)
        {
            return [];
        }

        var parts = new List<(string Title, string Markdown)>();

        // Leading matter (and the document-control section) become the first part when present
        var frontEnd = boundaries[0];
        if (frontEnd > 0 || model.DocumentControl.Count > 0)
        {
            var front = new System.Text.StringBuilder();
            front.Append(WordMarkdownWriter.WriteBlocks(model.DocumentControlAsBody(), imagePaths));
            front.Append(WordMarkdownWriter.WriteBlocks(body.Take(frontEnd).ToList(), imagePaths));
            parts.Add((model.Title ?? "Front matter", front.ToString()));
        }

        for (var boundary = 0; boundary < boundaries.Count; boundary++)
        {
            var start = boundaries[boundary];
            var end = boundary + 1 < boundaries.Count ? boundaries[boundary + 1] : body.Count;
            var segment = body.Skip(start).Take(end - start).ToList();
            var title = MarkdownToText(segment[0].Inlines) is { Length: > 0 } heading ? heading : "Section";
            parts.Add((title, WordMarkdownWriter.WriteBlocks(segment, imagePaths)));
        }

        AppendTrailingSections(model, parts);
        return parts;
    }

    /// <summary>
    ///     Appends the comments and footnotes to the final part so a split never loses them.
    /// </summary>
    /// <param name="model">The model whose comments and footnotes are appended.</param>
    /// <param name="parts">The parts built so far.</param>
    /// <remarks>Side effect: rewrites the last part's markdown. Pure otherwise.</remarks>
    private static void AppendTrailingSections(
        WordDocumentModel model, List<(string Title, string Markdown)> parts)
    {
        if (model.Comments.Count == 0 && model.Footnotes.Count == 0)
        {
            return;
        }

        var trailing = WordMarkdownWriter.WriteBlocks([], new Dictionary<string, string>());
        var commentsAndFootnotes = WordMarkdownWriter.Write(
            model with { Body = [], DocumentControl = [] }, new Dictionary<string, string>());
        if (commentsAndFootnotes.Length == 0)
        {
            return;
        }

        var last = parts[^1];
        parts[^1] = (last.Title, last.Markdown + trailing + commentsAndFootnotes);
    }

    /// <summary>
    ///     Collects the model's image references in document order, body first then document control.
    /// </summary>
    /// <param name="model">The model to walk.</param>
    /// <returns>The image references, deduplication left to the sink.</returns>
    /// <remarks>Pure.</remarks>
    private static List<WordImageRef> CollectImages(WordDocumentModel model)
    {
        var images = new List<WordImageRef>();
        foreach (var block in model.Body)
        {
            if (block is { Kind: WordBlockKind.Image, Image: { } image })
            {
                images.Add(image);
            }
        }

        foreach (var section in model.DocumentControl)
        {
            foreach (var block in section.Blocks)
            {
                if (block is { Kind: WordBlockKind.Image, Image: { } image })
                {
                    images.Add(image);
                }
            }
        }

        return images;
    }

    /// <summary>
    ///     Reports the extraction notes the model implies beyond the images written earlier.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The model to inspect.</param>
    /// <remarks>Side effect: records reports on the sink.</remarks>
    private static void ReportExtractionNotes(IExtractionSink sink, WordDocumentModel model)
    {
        // A chart carries its plotted data in a part this backend does not read; say so rather than
        // letting the chart leave no trace at all in a document claiming a complete extraction
        if (model.ChartsFound > 0)
        {
            ReportChartsNote(sink, model.ChartsFound);
        }

        ReportFlattenedTableStructureNote(sink, model);
    }

    /// <summary>
    ///     Reports that the document embeds charts whose chart parts this backend does not read.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="count">The number of chart parts found; always greater than zero.</param>
    /// <remarks>
    ///     A Word chart is anchored through a graphic frame carrying no image blip, so neither the
    ///     text walk nor the image walk sees it. The note therefore records the incomplete step
    ///     plainly, without treating the document's content as a defect. Side effect: records on the
    ///     sink.
    /// </remarks>
    private static void ReportChartsNote(IExtractionSink sink, int count)
    {
        var counted = count.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote(
            $"The document embeds {counted} charts, but this extractor does not read chart parts, so their titles and plotted values do not appear in the extracted content."));
    }

    /// <summary>
    ///     Reports when markdown table rendering could not preserve merged or nested table structure.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The model whose tables are inspected.</param>
    /// <remarks>
    ///     GitHub-flavored markdown has no merge or nested-table construct, so this is one of the few
    ///     places where the emitter attempted to preserve structure and could only approximate it.
    ///     Side effect: records on the sink.
    /// </remarks>
    private static void ReportFlattenedTableStructureNote(IExtractionSink sink, WordDocumentModel model)
    {
        var flattened = 0;
        foreach (var block in EnumerateTables(model))
        {
            flattened += block.MergedCellCount + block.NestedTableCount;
        }

        if (flattened == 0)
        {
            return;
        }

        var flattenedText = flattened.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote(
            $"{flattenedText} merged or nested table cells were flattened because GitHub-flavored markdown cannot represent that table structure."));
    }

    /// <summary>
    ///     Reports that PNG output could not be honored, explaining that source bytes were written instead.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="written">The number of distinct embedded image files written in their source encoding.</param>
    /// <remarks>
    ///     Core's naming rule already guarantees the file extension follows the bytes actually
    ///     written. This note therefore records only the incomplete re-encoding step itself. Side
    ///     effect: records on the sink.
    /// </remarks>
    private static void ReportForcePngNote(IExtractionSink sink, int written)
    {
        var writtenText = written.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote(
            $"PNG output was requested, but {writtenText} embedded image files were written in their source encoding because this package does not re-encode images."));
    }

    /// <summary>
    ///     Counts the textual blocks the emitted markdown carries.
    /// </summary>
    /// <param name="blocks">The rendered block sequence to count.</param>
    /// <returns>The number of heading, paragraph, list-item, and table blocks.</returns>
    /// <remarks>
    ///     A present-but-empty document is conveyed by this count at zero, marked looked for in the
    ///     content inventory, rather than by a separate note. Pure.
    /// </remarks>
    private static int CountTextualBlocks(IEnumerable<WordBlock> blocks) =>
        blocks.Count(block => block.Kind is WordBlockKind.Heading
            or WordBlockKind.Paragraph
            or WordBlockKind.ListItem
            or WordBlockKind.Table);

    /// <summary>
    ///     Enumerates every table block in the body and the document-control subsections.
    /// </summary>
    /// <param name="model">The model to walk.</param>
    /// <returns>The table models in document order.</returns>
    /// <remarks>Pure.</remarks>
    private static IEnumerable<WordTableModel> EnumerateTables(WordDocumentModel model)
    {
        foreach (var block in model.Body)
        {
            if (block is { Kind: WordBlockKind.Table, Table: { } table })
            {
                yield return table;
            }
        }

        foreach (var section in model.DocumentControl)
        {
            foreach (var block in section.Blocks)
            {
                if (block is { Kind: WordBlockKind.Table, Table: { } table })
                {
                    yield return table;
                }
            }
        }
    }

    /// <summary>
    ///     Extracts the plain text of a heading's inline runs for a part title.
    /// </summary>
    /// <param name="inlines">The heading inlines, or <see langword="null"/>.</param>
    /// <returns>The concatenated, trimmed text.</returns>
    /// <remarks>Pure.</remarks>
    private static string MarkdownToText(IReadOnlyList<WordInline>? inlines)
    {
        if (inlines is null)
        {
            return string.Empty;
        }

        return string.Concat(inlines.Select(inline => inline.Text)).Trim();
    }
}

/// <summary>
///     Convenience projections over a <see cref="WordDocumentModel"/> used when splitting content.
/// </summary>
/// <remarks>Pure and thread-safe.</remarks>
internal static class WordDocumentModelExtensions
{
    /// <summary>
    ///     Renders the document-control subsections as a flat block sequence for the front-matter part.
    /// </summary>
    /// <param name="model">The model whose document control is projected.</param>
    /// <returns>A block sequence carrying each subsection as a level-three heading and its blocks.</returns>
    /// <remarks>
    ///     Used only by the per-part splitter, which renders the front-matter part from a plain block
    ///     list rather than the whole-model writer. Pure.
    /// </remarks>
    public static IReadOnlyList<WordBlock> DocumentControlAsBody(this WordDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.DocumentControl.Count == 0)
        {
            return [];
        }

        var blocks = new List<WordBlock>
        {
            new(WordBlockKind.Heading, [new WordInline("Document Control")], HeadingLevel: 2)
        };
        foreach (var section in model.DocumentControl)
        {
            blocks.Add(new WordBlock(WordBlockKind.Heading, [new WordInline(section.Label)], HeadingLevel: 3));
            blocks.AddRange(section.Blocks);
        }

        return blocks;
    }
}
