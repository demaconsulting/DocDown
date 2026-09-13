using System.Globalization;
using System.Text;
using DocDown.Core;
using DocDown.Visio.OpenXml;

namespace DocDown.Visio.Markdown;

/// <summary>
///     Emits a read <see cref="VisioDocumentModel"/> through the extraction sink: per-page content in
///     document order carrying the page name, its shape text, and — the headline capability — its
///     directed connector topology, plus the honest gaps the model and options imply.
/// </summary>
/// <remarks>
///     A list of disconnected strings is not a schematic: the topology is the engineering content, so
///     each page renders its connections as a readable directed graph (<c>Inlet Tank → Transfer Pump</c>).
///     Nothing is omitted silently: an empty drawing is a counted gap, an empty page is noted
///     informationally, and any embedded images are extracted through the sink and counted. The
///     package thumbnail is deliberately not treated as embedded content. Performs no filesystem I/O
///     of its own; every byte goes through the sink. Stateless and thread-safe.
/// </remarks>
internal static class VisioContentEmitter
{
    /// <summary>The ledger path the empty-drawing gap names.</summary>
    private const string ContentTarget = "content.md";

    /// <summary>The ledger path every image gap names.</summary>
    private const string ImagesTarget = "images/";

    /// <summary>The directed-edge arrow rendered between two connected shapes.</summary>
    private const string Arrow = " \u2192 ";

    /// <summary>The two spaces that keep a continuation line inside its markdown list item.</summary>
    /// <remarks>Matches the list marker's width, which is what a renderer requires to continue the item.</remarks>
    private const string ContinuationIndent = "  ";

    /// <summary>The two trailing spaces that make a markdown hard line break.</summary>
    /// <remarks>Preserves the authored line structure of a fittings list or valve table within one list item.</remarks>
    private const string HardBreak = "  ";

    /// <summary>
    ///     Emits a drawing model through the sink and returns whether the run degraded.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The drawing model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when any gap was reported; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>Side effect: writes content and records reports on the sink.</remarks>
    public static async ValueTask<bool> EmitAsync(
        IExtractionSink sink, ExtractionOptions options, VisioDocumentModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        var degraded = false;

        sink.ReportFound(GapKind.Structure, model.Pages.Count);

        // An empty drawing has nothing to write; say so as a counted gap rather than an empty file
        if (model.Pages.Count == 0)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                VisioDiagnosticCodes.NoPages, DiagnosticSeverity.Warning,
                "The drawing contains no pages, so no content could be produced."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Structure, ContentTarget, GapScope.Failed,
                "The drawing contains no pages.",
                Impact: "No page content is present in the extracted output."));
            await sink.WriteContentAsync(string.Empty, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // Write the drawing's embedded image files first so each page can link its images inline at
        // their point of occurrence — Word's convention. Gap reporting is deferred below so the
        // diagnostic order is unchanged; only the file writes move up.
        var imageResult = options.IncludeEmbeddedImages
            ? await EmbeddedImageWriter.WriteAsync(sink, options, model.Images, cancellationToken).ConfigureAwait(false)
            : null;
        var imagePaths = imageResult?.PathsBySourceRef ?? EmptyImagePaths;

        var partCount = await WriteContentAsync(sink, options, model, imagePaths, cancellationToken).ConfigureAwait(false);
        sink.ReportDocumentInfo(new DocumentInfo(
            Title: null, Author: null, PageCount: model.Pages.Count, PartCount: partCount));

        // Report the drawing's self-reported metadata for metadata.json when the reader captured it
        if (model.Metadata is { } metadata)
        {
            sink.ReportDocumentMetadata(metadata);
        }

        ReportTopologyLabeling(sink, model);
        ReportContentFeatures(sink, model);

        foreach (var page in model.Pages)
        {
            if (page.Shapes.All(shape => shape.Text is null) && page.Connections.Count == 0)
            {
                sink.ReportDiagnostic(new ExtractionDiagnostic(
                    VisioDiagnosticCodes.EmptyPage, DiagnosticSeverity.Info,
                    $"Page '{page.Name}' carried no shapes with recoverable text and no connections."));
            }
        }

        // Report the honest gaps and caveats for the images written above. A drawing whose only
        // metafile is the package thumbnail (excluded by the reader) embeds no content images, so it
        // reports none and stays a clean success.
        degraded |= ReportImages(sink, options, imageResult);

        return degraded;
    }

    /// <summary>The empty path map used when images are suppressed, so content rendering emits no links.</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyImagePaths =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    ///     Reports an outline of what the rendered content contains, counted from the model.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The drawing model whose structure is counted.</param>
    /// <remarks>
    ///     A character count cannot tell a reader whether a drawing is a handful of labeled boxes or
    ///     a schematic with a hundred connected components, which is the first thing anyone deciding
    ///     whether to read it wants to know. Only shapes whose text survives the
    ///     <see cref="Informative"/> test and edges that actually reach the output are counted, so
    ///     the outline describes what a reader will find rather than what the drawing contains.
    ///     Side effect: records reports on the sink.
    /// </remarks>
    private static void ReportContentFeatures(IExtractionSink sink, VisioDocumentModel model)
    {
        sink.ReportContentFeature(new ContentFeature("pages", model.Pages.Count));
        sink.ReportContentFeature(new ContentFeature(
            "labeled shapes", model.Pages.Sum(page => page.Shapes.Count(shape => Informative(shape.Text)))));

        // Count the edges the reader will actually see, not the ones that were suppressed for naming
        // nothing; the suppressed counts are reported in the content itself, page by page
        var connections = 0;
        foreach (var page in model.Pages)
        {
            var shapes = IndexShapes(page);
            connections += page.Connections.Count(connection =>
                VisioShapeLabeler.Label(connection.FromId, shapes).Source != VisioLabelSource.Unresolved
                || VisioShapeLabeler.Label(connection.ToId, shapes).Source != VisioLabelSource.Unresolved);
        }

        sink.ReportContentFeature(new ContentFeature("connections", connections));
    }

    /// <summary>
    ///     Reports the honest gaps and caveats for the drawing's embedded images written earlier.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for suppression and force-PNG.</param>
    /// <param name="result">The image write accounting from the earlier write, or <see langword="null"/> when images were suppressed.</param>
    /// <returns><see langword="true"/> when any image gap was reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     When images are suppressed nothing was written and Core records the suppression itself, so
    ///     this reports nothing. A drawing that embeds no content images reports nothing here, so it
    ///     stays a clean success. Vector metafiles carry a readability caveat, an image beyond a caller
    ///     limit is a counted size-skip, and a force-PNG request is explained rather than honored
    ///     because this backend ships no imaging stack. Side effect: records reports on the sink.
    /// </remarks>
    private static bool ReportImages(IExtractionSink sink, ExtractionOptions options, EmbeddedImageWriteResult? result)
    {
        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit reports nothing here
        if (result is null)
        {
            return false;
        }

        // A drawing that embeds no content images has no images ledger to open; stay silent
        if (result.Found == 0)
        {
            return false;
        }

        sink.ReportFound(GapKind.Images, result.Found);

        var degraded = false;
        if (result.VectorWrittenCount > 0)
        {
            ReportVectorImageDiagnostic(sink, result);
        }

        if (result.SizeSkippedCount > 0)
        {
            degraded |= ReportSizeSkipGap(sink, result);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && result.ForcePngUnhonoredCount > 0)
        {
            degraded |= ReportForcePngGap(sink, result);
        }

        return degraded;
    }

    /// <summary>
    ///     Reports the informational readability caveat for EMF or WMF vector metafiles written unchanged.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the vector and found counts.</param>
    /// <remarks>Informational, never a gap: the bytes are a complete image file, written and counted as extracted, and no better environment would yield more; the caveat is that many viewers cannot render a Windows metafile. Side effect: records a diagnostic.</remarks>
    private static void ReportVectorImageDiagnostic(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.VectorWrittenCount, result.Found);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            VisioDiagnosticCodes.VectorImageWrittenAsIs, DiagnosticSeverity.Info,
            $"{counted} embedded images are EMF or WMF vector metafiles, which many viewers cannot render. "
            + "Their bytes were written unchanged and counted as extracted."));
    }

    /// <summary>
    ///     Reports the counted gap for images skipped because they exceed a caller-supplied limit.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the size-skip count, items, and found total.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>A size skip is a deliberate, reversible choice, named as such and pointing at the option to relax. Side effect: records reports.</remarks>
    private static bool ReportSizeSkipGap(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.SizeSkippedCount, result.Found);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Images, ImagesTarget, GapScope.PartiallyExtracted,
            $"{counted} embedded images exceeded a caller-supplied size limit and were skipped rather than written.",
            Impact: "Those images are not present in the extracted output.",
            Remedy: "Raise or clear MaxImageBytes and MaxImageDimensionPx to extract images of any size.",
            AffectedCount: result.SizeSkippedCount,
            AffectedItems: result.SizeSkippedItems));
        return true;
    }

    /// <summary>
    ///     Reports that PNG output could not be honored, explaining that source bytes were written instead.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the unhonored-force-PNG count and found total.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>The file extension follows the bytes actually written; this backend ships no imaging stack, so it cannot re-encode. Side effect: records reports.</remarks>
    private static bool ReportForcePngGap(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.ForcePngUnhonoredCount, result.Found);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Images, ImagesTarget, GapScope.PartiallyExtracted,
            $"PNG output was requested, but this backend ships no imaging stack and cannot re-encode; "
            + $"{counted} embedded images were written in their source encoding with a matching file "
            + "extension rather than converted.",
            Impact: "Those images are not in the requested PNG format; their file extensions and the "
            + "manifest media types describe what was actually written.",
            AffectedCount: result.ForcePngUnhonoredCount));
        return true;
    }

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
    ///     Writes the drawing content, either as one flow or as per-page parts, honoring the split mode.
    /// </summary>
    /// <param name="sink">The sink to write content through.</param>
    /// <param name="options">The effective options carrying the split mode.</param>
    /// <param name="model">The drawing model.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of parts written, or <see langword="null"/> for a single flow.</returns>
    /// <remarks>Side effect: writes content on the sink.</remarks>
    private static async ValueTask<int?> WriteContentAsync(
        IExtractionSink sink, ExtractionOptions options, VisioDocumentModel model,
        IReadOnlyDictionary<string, string> imagePaths, CancellationToken cancellationToken)
    {
        if (options.ContentSplit == ContentSplitMode.PerPart)
        {
            var ordinal = 1;
            foreach (var page in model.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await sink.AddContentPartAsync(
                    new ContentPart(ContentPartKind.Page, ordinal, page.Name), RenderPage(page, imagePaths), cancellationToken)
                    .ConfigureAwait(false);
                ordinal++;
            }

            return model.Pages.Count;
        }

        var builder = new StringBuilder();
        foreach (var page in model.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(RenderPage(page, imagePaths));
            builder.Append('\n');
        }

        await sink.WriteContentAsync(builder.ToString().TrimEnd('\n') + "\n", cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    ///     Renders one page to markdown: its name heading, its shape-text list, its directed
    ///     topology as a readable edge list, and the images it shows linked inline.
    /// </summary>
    /// <param name="page">The page to render.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <returns>The page's markdown.</returns>
    /// <remarks>
    ///     Each image the page shows is linked at its point of occurrence — after the shapes and
    ///     topology — exactly when the sink returned a path for it, so a suppressed, size-skipped, or
    ///     deduplicated-away image never leaves a link pointing at nothing, mirroring Word. Pure.
    /// </remarks>
    private static string RenderPage(VisioPageModel page, IReadOnlyDictionary<string, string> imagePaths)
    {
        var builder = new StringBuilder();
        builder.Append("# ").Append(page.Name).Append('\n').Append('\n');

        var labeled = page.Shapes.Where(shape => Informative(shape.Text)).ToList();
        var suppressedShapes = page.Shapes.Count(shape => shape.Text is not null) - labeled.Count;
        if (labeled.Count > 0)
        {
            builder.Append("## Shapes").Append('\n').Append('\n');
            foreach (var shape in labeled)
            {
                builder.Append("- ").Append(AsListItem(shape.Text!)).Append('\n');
            }

            builder.Append('\n');
        }

        // Account for what was dropped so the omission is visible rather than silent
        if (suppressedShapes > 0)
        {
            builder.Append("> ").Append(suppressedShapes.ToString(CultureInfo.InvariantCulture))
                .Append(suppressedShapes == 1
                    ? " shape whose text is a bare callout number was omitted."
                    : " shapes whose text is a bare callout number were omitted.")
                .Append('\n').Append('\n');
        }

        if (page.Connections.Count > 0)
        {
            var shapes = IndexShapes(page);
            var labels = page.Connections
                .Select(connection => (
                    From: VisioShapeLabeler.Label(connection.FromId, shapes),
                    To: VisioShapeLabeler.Label(connection.ToId, shapes)))
                .ToList();

            // An edge whose endpoints are both unidentified names nothing a reader can use; keep
            // every edge with at least one meaningful endpoint and count the rest
            var meaningful = labels
                .Where(pair => pair.From.Source != VisioLabelSource.Unresolved
                    || pair.To.Source != VisioLabelSource.Unresolved)
                .ToList();
            var suppressedEdges = labels.Count - meaningful.Count;

            if (meaningful.Count > 0)
            {
                builder.Append("## Connections").Append('\n').Append('\n');

                // State the convention in the content itself whenever a label is not the shape's own
                // text, so a human reading content.md alone cannot mistake a type for a name
                if (meaningful.Any(pair => pair.From.Source != VisioLabelSource.Text || pair.To.Source != VisioLabelSource.Text))
                {
                    builder.Append("> ").Append(VisioShapeLabeler.Convention).Append('\n').Append('\n');
                }

                foreach (var (from, to) in meaningful)
                {
                    builder.Append("- ").Append(from.Display).Append(Arrow).Append(to.Display).Append('\n');
                }

                builder.Append('\n');
            }

            if (suppressedEdges > 0)
            {
                builder.Append("> ").Append(suppressedEdges.ToString(CultureInfo.InvariantCulture))
                    .Append(suppressedEdges == 1
                        ? " connection between unnamed shapes was omitted."
                        : " connections between unnamed shapes were omitted.")
                    .Append('\n').Append('\n');
            }
        }

        foreach (var image in page.Images)
        {
            if (imagePaths.TryGetValue(image.SourceRef, out var path))
            {
                builder.Append("![").Append(ImageLinkText.Alt(image.AltText)).Append("](").Append(path).Append(")")
                    .Append('\n').Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Decides whether a shape's text carries information worth a line of a reader's budget.
    /// </summary>
    /// <param name="text">The shape's text, or <see langword="null"/> when it carries none.</param>
    /// <returns><see langword="true"/> when the text contains at least one letter.</returns>
    /// <remarks>
    ///     Engineering drawings label callouts with bare numbers — <c>1</c>, <c>2</c>, <c>3</c> — that
    ///     key into a legend the drawing renders graphically. Listed as components they read as
    ///     equipment named "1", which is both noise and actively misleading. Requiring a letter keeps
    ///     every part number, designation, and name (all of which carry letters) while dropping the
    ///     bare callouts; the count of what was dropped is reported so the omission stays visible.
    ///     Pure.
    /// </remarks>
    private static bool Informative(string? text) => text is not null && text.Any(char.IsLetter);

    /// <summary>
    ///     Renders a shape's text as the body of a markdown list item, keeping multi-line text inside
    ///     the item it belongs to.
    /// </summary>
    /// <param name="text">The shape's text, which may span several lines.</param>
    /// <returns>The list-item body with continuation lines indented and hard-broken.</returns>
    /// <remarks>
    ///     Emitted flat, a shape's continuation lines start at column zero and the list structure
    ///     collapses: a renderer merges "<c>Transfer Pump</c>" with the two part numbers beneath it
    ///     unpredictably, and those part numbers are exactly what makes Visio text valuable. Indenting
    ///     each continuation line by two spaces keeps it inside its item, and a two-space hard break
    ///     on the preceding line preserves the authored line structure — which matters because these
    ///     blocks are fittings lists and valve-positioning tables, not prose. Nothing is truncated.
    ///     Blank lines are emitted bare so they start a new paragraph within the same item. Pure.
    /// </remarks>
    private static string AsListItem(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            // A blank line separates paragraphs within the item and must carry no indent of its own
            if (lines[index].Length == 0)
            {
                continue;
            }

            if (index > 0)
            {
                builder.Append(ContinuationIndent);
            }

            builder.Append(lines[index]);

            // Hard-break before another content line so the authored line structure survives rendering
            if (index + 1 < lines.Count && lines[index + 1].Length > 0)
            {
                builder.Append(HardBreak);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Indexes a page's shapes by id for endpoint resolution.
    /// </summary>
    /// <param name="page">The page whose shapes are indexed.</param>
    /// <returns>The shape-id-to-shape map, keeping the first shape declared under any repeated id.</returns>
    /// <remarks>
    ///     Grouping rather than a direct dictionary build is deliberate: a page-local shape id is
    ///     expected to be unique, but a malformed drawing that repeats one must degrade to reporting
    ///     the first declaration, not abort an otherwise complete extraction. Pure.
    /// </remarks>
    private static Dictionary<string, VisioShapeModel> IndexShapes(VisioPageModel page) =>
        page.Shapes
            .GroupBy(shape => shape.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>
    ///     Reports the topology labeling convention and the honest endpoint-resolution coverage.
    /// </summary>
    /// <param name="sink">The sink to report diagnostics through.</param>
    /// <param name="model">The drawing model whose endpoints are counted.</param>
    /// <remarks>
    ///     <para>
    ///         The convention must survive machine reading, not only a human eye on
    ///         <c>content.md</c>, so it is published verbatim into the manifest's diagnostic stream
    ///         alongside the counts a consumer needs to judge how much of the topology is actually
    ///         identified: how many endpoint occurrences carry the shape's own text, how many are
    ///         named only by type, and how many remain a bare shape id.
    ///     </para>
    ///     <para>
    ///         The counts are of endpoint occurrences, not distinct shapes, because a reader's
    ///         question is "how readable is this edge list", and an endpoint that appears on five
    ///         edges is read five times. An unresolved endpoint is not reported as a gap: nothing was
    ///         lost in extraction — the drawing genuinely does not name that shape — and a gap would
    ///         claim a failure that is the document's, not the extractor's.
    ///     </para>
    ///     <para>Side effect: reports diagnostics on the sink. Reports nothing when the drawing has no connections.</para>
    /// </remarks>
    private static void ReportTopologyLabeling(IExtractionSink sink, VisioDocumentModel model)
    {
        var byText = 0;
        var byType = 0;
        var unresolved = 0;

        foreach (var page in model.Pages)
        {
            var shapes = IndexShapes(page);
            foreach (var connection in page.Connections)
            {
                CountEndpoint(VisioShapeLabeler.Label(connection.FromId, shapes).Source);
                CountEndpoint(VisioShapeLabeler.Label(connection.ToId, shapes).Source);
            }
        }

        void CountEndpoint(VisioLabelSource source)
        {
            switch (source)
            {
                case VisioLabelSource.Text:
                    byText++;
                    break;
                case VisioLabelSource.Type:
                    byType++;
                    break;
                default:
                    unresolved++;
                    break;
            }
        }

        if (byText + byType + unresolved == 0)
        {
            return;
        }

        sink.ReportDiagnostic(new ExtractionDiagnostic(
            VisioDiagnosticCodes.TopologyLabelConvention, DiagnosticSeverity.Info, VisioShapeLabeler.Convention));

        var total = (byText + byType + unresolved).ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            VisioDiagnosticCodes.TopologyEndpointCoverage, DiagnosticSeverity.Info,
            $"Of {total} connector endpoint occurrence(s), "
            + $"{byText.ToString(CultureInfo.InvariantCulture)} carried the shape's own text, "
            + $"{byType.ToString(CultureInfo.InvariantCulture)} were named by the shape's master type, and "
            + $"{unresolved.ToString(CultureInfo.InvariantCulture)} are identified only by shape id."));
    }
}
