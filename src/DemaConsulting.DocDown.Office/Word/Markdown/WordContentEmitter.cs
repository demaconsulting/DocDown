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

        var partCount = await WriteContentAsync(sink, model, imagePaths, cancellationToken)
            .ConfigureAwait(false);

        // Reviewer remarks are not the document's content, so they leave content.md untouched and
        // travel to Core as review comments for the dedicated review-comments.md artifact
        ReportReviewComments(sink, model);

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
    ///     Reports the document's comments through the sink as review comments.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The document model whose comments are reported.</param>
    /// <remarks>
    ///     <para>
    ///         A reviewer's remark is commentary <em>about</em> the document rather than part of it,
    ///         and interleaving it into <c>content.md</c> left a consumer unable to tell the author's
    ///         words from a reviewer's. Reporting each comment to Core instead collects them in one
    ///         <c>review-comments.md</c> artifact, in the order the reader found them.
    ///     </para>
    ///     <para>
    ///         A comment whose anchor markers the document does not carry has no location to state,
    ///         so <see cref="UnknownLocation"/> is used rather than inventing a position.
    ///     </para>
    ///     <para>
    ///         The body is reported as the document records it — literal text, not markdown. Core
    ///         carries it verbatim into <c>manifest.json</c>, whose documented contract is exactly
    ///         "the comment text as the document records it", and
    ///         <see cref="ReviewCommentsWriter"/> escapes it where markdown is written. Reporting an
    ///         escaped body here would put rendering artifacts into the machine-readable manifest
    ///         and would make Word the only backend that does so.
    ///     </para>
    ///     <para>
    ///         A comment whose text is blank is skipped rather than reported. The reader already
    ///         drops those, so this is the second of two layers: it is the only unit that calls
    ///         <see cref="IExtractionSink.ReportReviewComment"/> for Word, and that method rejects a
    ///         blank body by contract. Honoring the invariant here keeps a malformed comment from
    ///         turning a perfectly readable document into a failed extraction. Side effect: records
    ///         review comments on the sink.
    ///     </para>
    /// </remarks>
    private static void ReportReviewComments(IExtractionSink sink, WordDocumentModel model)
    {
        foreach (var comment in model.Comments)
        {
            var body = WordMarkdownWriter.RenderPlainText(comment.Content);
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            sink.ReportReviewComment(new DocumentComment(
                comment.Author,
                body,
                comment.Location ?? UnknownLocation));
        }
    }

    /// <summary>The location stated for a comment the document anchors nowhere this reader could resolve.</summary>
    /// <remarks>
    ///     Stating the absence plainly keeps every review comment the same shape without claiming a
    ///     position the document never gave.
    /// </remarks>
    private const string UnknownLocation = "(location unknown)";

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
    ///         it the comments had been extracted at all. Naming the comment count, and how many
    ///         distinct people wrote them, makes that content discoverable for a handful of tokens.
    ///     </para>
    ///     <para>
    ///         The counts come from the model the reader built by walking the document, not from
    ///         scanning the rendered markdown: the model already knows, and a regex over markdown would
    ///         describe a rendering rather than the document. Counting comments here remains correct
    ///         even though their text is now reported as review comments rather than rendered into
    ///         <c>content.md</c> — the inventory answers "what does this document carry", which is
    ///         independent of which artifact carries it. Features whose absence matters to a reader
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

        return paths;
    }

    /// <summary>
    ///     Writes the document content as one continuous flow.
    /// </summary>
    /// <param name="sink">The sink to write content through.</param>
    /// <param name="model">The model to render.</param>
    /// <param name="imagePaths">The image path map.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>Always <see langword="null"/>, meaning a single flow rather than a part count.</returns>
    /// <remarks>
    ///     A Word document is one continuous flow, so it is written as one <c>content.md</c> rather
    ///     than split into <c>parts/</c>: splitting at headings would invent a structure the
    ///     document does not assert, and a reader following the flow would meet arbitrary breaks.
    ///     Side effect: writes content on the sink.
    /// </remarks>
    private static async ValueTask<int?> WriteContentAsync(
        IExtractionSink sink, WordDocumentModel model,
        IReadOnlyDictionary<string, string> imagePaths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var flow = WordMarkdownWriter.Write(model, imagePaths);
        await sink.WriteContentAsync(flow, cancellationToken).ConfigureAwait(false);
        return null;
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

        // A comment whose only content is a picture or ink carried a remark this backend cannot
        // render; say so rather than letting the reviewer's words leave no trace
        if (model.CommentsWithUnreadableContent > 0)
        {
            ReportImageOnlyCommentsNote(sink, model.CommentsWithUnreadableContent);
        }
    }

    /// <summary>
    ///     Reports that some comments carried only a picture or ink and therefore no readable text.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="count">The number of such comments; always greater than zero.</param>
    /// <remarks>
    ///     This is the narrow case a note exists for: the reviewer did leave a remark, the extractor
    ///     attempted to read it, and what it found cannot be rendered as comment text. A comment that
    ///     was simply never typed into is not reported, because nothing was lost there. Side effect:
    ///     records on the sink.
    /// </remarks>
    private static void ReportImageOnlyCommentsNote(IExtractionSink sink, int count)
    {
        var counted = count.ToString(CultureInfo.InvariantCulture);
        var noun = count == 1 ? "comment carries" : "comments carry";
        var possessive = count == 1 ? "its content does" : "their content does";
        sink.ReportNote(new ExtractionNote(
            $"{counted} {noun} only a picture or ink and no text, so {possessive} not appear in review-comments.md."));
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
}

