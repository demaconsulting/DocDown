using System.Globalization;
using DocDown.Core;

namespace DocDown.Word.Markdown;

/// <summary>
///     Emits a rendered <see cref="WordDocumentModel"/> through the extraction sink, applying the
///     honest gap-and-diagnostic policy in one place.
/// </summary>
/// <remarks>
///     <para>
///         The reader populates the model and hands it here, so every decision about what reaches
///         the output is made once, in a unit that can be exercised from a hand-built model with no
///         document behind it. Only how the model and its image bytes are obtained lives upstream;
///         everything downstream of the model is this one unit.
///     </para>
///     <para>
///         Every shortfall the model records — an assumed table header, a flattened merge, an
///         omitted page-furniture header, a document with no text — becomes a diagnostic or a
///         counted, reasoned gap here. Nothing is omitted silently. Performs no filesystem I/O of
///         its own: every byte goes through the sink. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordContentEmitter
{
    /// <summary>The ledger path every image gap must name for the contract verifier to accept it.</summary>
    private const string ImagesTarget = "images/";

    /// <summary>The ledger path the content and structural gaps name.</summary>
    private const string ContentTarget = "content.md";

    /// <summary>The pages ledger path a rendering gap names.</summary>
    private const string PagesTarget = "pages/";

    /// <summary>
    ///     Emits a model through the sink: images, content, metadata, and every diagnostic or gap
    ///     the model implies.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The document model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the run degraded (any gap was reported); otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>Side effect: writes content and images and records reports on the sink.</remarks>
    public static async ValueTask<bool> EmitAsync(
        IExtractionSink sink, ExtractionOptions options, WordDocumentModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        // Images first: the content renderer places the links the sink allocates for them
        var (imagePaths, imagesDegraded) = await WriteImagesAsync(sink, options, model, cancellationToken)
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

        var modelDegraded = ReportModelDiagnostics(sink, options, model);
        return imagesDegraded || modelDegraded;
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
    ///         scanning the rendered markdown: the model already knows, and a regex over markdown
    ///         would describe a rendering rather than the document. Core drops any zero count, so a
    ///         document without tables or comments simply omits those words. Side effect: records
    ///         reports on the sink.
    ///     </para>
    /// </remarks>
    private static void ReportContentFeatures(IExtractionSink sink, WordDocumentModel model)
    {
        // Count the body and the surviving document-control subsections together: both are rendered
        // into content.md, so both are things a reader will actually find there
        var blocks = model.Body.Concat(model.DocumentControl.SelectMany(section => section.Blocks)).ToList();

        sink.ReportContentFeature(new ContentFeature(
            "headings", blocks.Count(block => block.Kind == WordBlockKind.Heading)));
        sink.ReportContentFeature(new ContentFeature(
            "tables", blocks.Count(block => block.Kind == WordBlockKind.Table)));
        sink.ReportContentFeature(new ContentFeature(
            "list items", blocks.Count(block => block.Kind == WordBlockKind.ListItem)));
        sink.ReportContentFeature(new ContentFeature(
            "inline images", blocks.Count(block => block.Kind == WordBlockKind.Image)));
        sink.ReportContentFeature(new ContentFeature("comments", model.Comments.Count));

        // Distinct comment authors answer "is this one person's markup or a review?"; an unattributed
        // comment is not counted as a comment author because the document names nobody for it
        sink.ReportContentFeature(new ContentFeature(
            "distinct comment authors",
            model.Comments.Select(comment => comment.Author)
                .Where(author => author is not null)
                .Distinct(StringComparer.Ordinal)
                .Count()));

        sink.ReportContentFeature(new ContentFeature("footnotes", model.Footnotes.Count));
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
    /// <returns>The map from an image source reference to its written path, and whether the run degraded.</returns>
    /// <remarks>
    ///     When images are suppressed nothing is written and Core records the suppression itself. A
    ///     vector metafile is written unchanged with a readability caveat, and a force-PNG request is
    ///     explained rather than honored because this package ships no imaging stack. Side effect:
    ///     writes images and records reports on the sink.
    /// </remarks>
    private static async ValueTask<(Dictionary<string, string> Paths, bool Degraded)> WriteImagesAsync(
        IExtractionSink sink, ExtractionOptions options, WordDocumentModel model, CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit must not read, write, or count anything here
        if (!options.IncludeEmbeddedImages)
        {
            return (paths, false);
        }

        var images = CollectImages(model);
        if (images.Count == 0)
        {
            return (paths, false);
        }

        var writtenPaths = new HashSet<string>(StringComparer.Ordinal);
        var vectorCount = 0;

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
            if (IsVectorMetafile(image.MediaType))
            {
                vectorCount++;
            }
        }

        // Report the found count as the number of distinct files written: a logo referenced many
        // times is one image, so found equals obtained and the ledger stays clean rather than
        // inventing a false shortfall from deduplication
        sink.ReportFound(GapKind.Images, writtenPaths.Count);

        var degraded = false;
        if (vectorCount > 0)
        {
            ReportVectorImageDiagnostic(sink, vectorCount, writtenPaths.Count);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && writtenPaths.Count > 0)
        {
            degraded |= ReportForcePngGap(sink, writtenPaths.Count);
        }

        return (paths, degraded);
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
    ///     Reports every diagnostic and gap the model implies beyond the images.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options.</param>
    /// <param name="model">The model to inspect.</param>
    /// <returns><see langword="true"/> when any gap was reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>Side effect: records reports on the sink.</remarks>
    private static bool ReportModelDiagnostics(IExtractionSink sink, ExtractionOptions options, WordDocumentModel model)
    {
        var degraded = false;

        if (!HasText(model))
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.NoTextContent, DiagnosticSeverity.Warning,
                "The document carries no extractable text."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Text, ContentTarget, GapScope.Unavailable,
                "The document carries no headings, paragraphs, list items, or tables, so there is no "
                + "textual content to extract.",
                Impact: "No textual content could be recovered from this document."));
            degraded = true;
        }
        else
        {
            degraded |= ReportTableDiagnostics(sink, model);
        }

        if (model.TrackedChangeCount > 0)
        {
            var count = model.TrackedChangeCount.ToString(CultureInfo.InvariantCulture);
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.TrackedChangesAccepted, DiagnosticSeverity.Info,
                $"The document contains {count} tracked-change revisions; they were rendered in the "
                + "accepted-revisions view."));
        }

        if (model.HeaderFooterPartsEmpty > 0 || model.HeaderFooterPartsPageFurniture > 0)
        {
            ReportHeaderFooterOmissions(sink, model);
        }

        if (options.RenderPages)
        {
            ReportRenderingUnavailableGap(sink);
            degraded = true;
        }

        return degraded;
    }

    /// <summary>
    ///     Reports the table-related diagnostics and the counted structural gap for flattened cells.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The model whose tables are inspected.</param>
    /// <returns><see langword="true"/> when a flattening gap was reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>Side effect: records reports on the sink.</remarks>
    private static bool ReportTableDiagnostics(IExtractionSink sink, WordDocumentModel model)
    {
        var flattened = 0;
        var assumedHeader = false;
        foreach (var block in EnumerateTables(model))
        {
            flattened += block.MergedCellCount + block.NestedTableCount;
            assumedHeader |= !block.FirstRowIsHeader;
        }

        if (assumedHeader)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.TableHeaderAssumed, DiagnosticSeverity.Info,
                "A table's first row was used as the header row though the document did not mark it one."));
        }

        if (model.EmptyTablesSkipped > 0)
        {
            var count = model.EmptyTablesSkipped.ToString(CultureInfo.InvariantCulture);
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.EmptyTableSkipped, DiagnosticSeverity.Info,
                $"{count} tables with no cell content were skipped."));
        }

        if (flattened == 0)
        {
            return false;
        }

        var flattenedText = flattened.ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            WordDiagnosticCodes.MergedCellsFlattened, DiagnosticSeverity.Warning,
            $"{flattenedText} merged or nested table cells were flattened."));
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Structure, ContentTarget, GapScope.PartiallyExtracted,
            $"{flattenedText} merged (gridSpan/vMerge) or nested table cells were flattened because "
            + "GitHub-flavored markdown cannot express a merge or a nested table; their content is "
            + "preserved but the cell structure is not.",
            Impact: "The affected tables read as flat grids rather than reproducing the original merges.",
            AffectedCount: flattened));
        return true;
    }

    /// <summary>
    ///     Records the omitted header and footer parts as informational diagnostics, never gaps.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The model carrying the two omission counts.</param>
    /// <remarks>
    ///     An empty header or footer part and a part carrying only page-numbering furniture are both
    ///     expected authoring artifacts that carry no document content, so omitting them drops nothing.
    ///     Each is therefore an <see cref="DiagnosticSeverity.Info"/> note — "an expected decision; no
    ///     action is implied" — and neither raises a gap nor degrades the run. The two cases are
    ///     reported separately so each carries its own matching wording; a mixed batch is never
    ///     described by one case's text. Side effect: records diagnostics on the sink.
    /// </remarks>
    private static void ReportHeaderFooterOmissions(IExtractionSink sink, WordDocumentModel model)
    {
        if (model.HeaderFooterPartsEmpty > 0)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.HeaderFooterPageNumberingOnly, DiagnosticSeverity.Info,
                EmptyPartsMessage(model.HeaderFooterPartsEmpty)));
        }

        if (model.HeaderFooterPartsPageFurniture > 0)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                WordDiagnosticCodes.HeaderFooterPageNumberingOnly, DiagnosticSeverity.Info,
                FurniturePartsMessage(model.HeaderFooterPartsPageFurniture)));
        }
    }

    /// <summary>
    ///     Composes the grammatically-agreeing note for empty header or footer parts.
    /// </summary>
    /// <param name="count">The number of empty parts omitted; always positive at the call site.</param>
    /// <returns>The note text, singular or plural to match the count.</returns>
    /// <remarks>Pure.</remarks>
    private static string EmptyPartsMessage(int count) =>
        count == 1
            ? "1 empty header or footer part was omitted from document control because it carried no "
              + "content to add to the narrative."
            : $"{count.ToString(CultureInfo.InvariantCulture)} empty header or footer parts were omitted "
              + "from document control because they carried no content to add to the narrative.";

    /// <summary>
    ///     Composes the grammatically-agreeing note for page-furniture-only header or footer parts.
    /// </summary>
    /// <param name="count">The number of furniture-only parts omitted; always positive at the call site.</param>
    /// <returns>The note text, singular or plural to match the count.</returns>
    /// <remarks>Pure.</remarks>
    private static string FurniturePartsMessage(int count) =>
        count == 1
            ? "1 header or footer part containing only page-numbering fields was omitted from document "
              + "control because that furniture is structural repetition rather than document content."
            : $"{count.ToString(CultureInfo.InvariantCulture)} header or footer parts containing only "
              + "page-numbering fields were omitted from document control because that furniture is "
              + "structural repetition rather than document content.";

    /// <summary>
    ///     Reports the informational readability caveat for vector metafiles written unchanged.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="vectorCount">The number of vector images written as-is.</param>
    /// <param name="found">The total number of images found.</param>
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file, so they are written and
    ///     counted as extracted rather than lost, and no better environment would yield more — this
    ///     package deliberately ships no metafile rasterizer. The caveat is that many viewers cannot
    ///     render EMF or WMF metafiles, so it is stated for the reader without degrading the run. Side
    ///     effect: records a diagnostic on the sink.
    /// </remarks>
    private static void ReportVectorImageDiagnostic(IExtractionSink sink, int vectorCount, int found)
    {
        var counted = Counted(vectorCount, found);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            WordDiagnosticCodes.VectorImageWrittenAsIs, DiagnosticSeverity.Info,
            $"{counted} embedded images are EMF or WMF vector metafiles, which many viewers cannot render. "
            + "Their bytes were written unchanged and counted as extracted."));
    }

    /// <summary>
    ///     Reports that PNG output could not be honored, explaining that source bytes were written instead.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="written">The number of images written in their source encoding.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>
    ///     Core's naming rule already guarantees the file extension follows the bytes actually
    ///     written; this gap supplies the explanation. This package ships no imaging stack, so it
    ///     cannot decode and re-encode. Mirrors the PDF backend's unhonored force-PNG note. Side
    ///     effect: records reports on the sink.
    /// </remarks>
    private static bool ReportForcePngGap(IExtractionSink sink, int written)
    {
        var writtenText = written.ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            WordDiagnosticCodes.ForcePngNotHonored, DiagnosticSeverity.Warning,
            $"PNG output was requested but {writtenText} images were written in their source encoding."));
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Images, ImagesTarget, GapScope.PartiallyExtracted,
            $"PNG output was requested, but this package ships no imaging stack and cannot re-encode; "
            + $"{writtenText} embedded images were written in their source encoding with a matching file "
            + "extension rather than converted.",
            Impact: "Those images are not in the requested PNG format; their file extensions and the "
            + "manifest media types describe what was actually written.",
            AffectedCount: written));
        return true;
    }

    /// <summary>
    ///     Reports that this backend does not render pages, naming where that capability would live.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <remarks>
    ///     Core also emits its own engine-level gap for the unmet render request; this one adds the
    ///     backend-specific reason. The wording states a fact about where the capability would come
    ///     from rather than issuing an instruction, so it tells the reader nothing they could act on
    ///     and fail at today and remains true on the day such a package exists. Side effect: records
    ///     on the sink.
    /// </remarks>
    private static void ReportRenderingUnavailableGap(IExtractionSink sink)
    {
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Pages, PagesTarget, GapScope.Unavailable,
            "This extractor reads Word text, tables, embedded images, and document metadata; it does "
            + "not render pages. Rendered page images would come from a separate Word page-rendering "
            + "extractor package that a host registers with the engine alongside this one.",
            Impact: "Rendered page images are not available."));
    }

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
    ///     Determines whether the model carries any textual content.
    /// </summary>
    /// <param name="model">The model to inspect.</param>
    /// <returns><see langword="true"/> when text is present; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Text is present when the body has a heading, paragraph, list item, or table, when the
    ///     metadata names a title, or when a document-control subsection survived. Pure.
    /// </remarks>
    private static bool HasText(WordDocumentModel model)
    {
        if (model.DocumentControl.Count > 0 || model.Comments.Count > 0 || model.Footnotes.Count > 0)
        {
            return true;
        }

        return model.Body.Any(block => block.Kind
            is WordBlockKind.Heading or WordBlockKind.Paragraph or WordBlockKind.ListItem or WordBlockKind.Table);
    }

    /// <summary>
    ///     Reports whether an image media type names an EMF or WMF vector metafile.
    /// </summary>
    /// <param name="mediaType">The image media type.</param>
    /// <returns><see langword="true"/> for a vector metafile; otherwise <see langword="false"/>.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsVectorMetafile(string mediaType) =>
        mediaType.Contains("emf", StringComparison.OrdinalIgnoreCase)
        || mediaType.Contains("wmf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Renders a "{count} of {found}" or bare-count phrase for gap prose.
    /// </summary>
    /// <param name="count">The affected count.</param>
    /// <param name="found">The total found.</param>
    /// <returns>The phrase.</returns>
    /// <remarks>Reads naturally whether or not the affected set is the whole set. Pure.</remarks>
    private static string Counted(int count, int found)
    {
        var countText = count.ToString(CultureInfo.InvariantCulture);
        return count == found
            ? countText
            : $"{countText} of {found.ToString(CultureInfo.InvariantCulture)}";
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
