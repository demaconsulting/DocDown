using System.Globalization;
using System.Text;
using DocDown.Core;
using DocDown.PowerPoint.OpenXml;

namespace DocDown.PowerPoint.Markdown;

/// <summary>
///     Emits a read <see cref="PowerPointDeckModel"/> through the extraction sink: per-slide content
///     in presentation order carrying title, body text, and speaker notes, plus the honest gaps the
///     model and options imply.
/// </summary>
/// <remarks>
///     The guaranteed half of PowerPoint extraction lives here: the text of every slide, its title,
///     the speaker notes, and the deck's embedded images. Nothing is omitted silently: a deck with
///     no notes is stated as a counted zero in the content outline (an inventory fact about the
///     deck, not a shortfall of the extraction); the embedded images are extracted through the sink
///     and counted; an empty deck is a counted gap. Performs no filesystem I/O of its own; every
///     byte goes through the sink. Stateless and thread-safe.
/// </remarks>
internal static class PowerPointContentEmitter
{
    /// <summary>The ledger path the empty-deck gap names.</summary>
    private const string ContentTarget = "content.md";

    /// <summary>The ledger path every image gap names.</summary>
    private const string ImagesTarget = "images/";

    /// <summary>
    ///     Emits a deck model through the sink and returns whether the run degraded.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The deck model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when any gap was reported; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>Side effect: writes content and records reports on the sink.</remarks>
    public static async ValueTask<bool> EmitAsync(
        IExtractionSink sink, ExtractionOptions options, PowerPointDeckModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        var degraded = false;

        // An empty deck has nothing to write; say so as a counted gap rather than an empty file
        if (model.Slides.Count == 0)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                PowerPointDiagnosticCodes.NoSlides, DiagnosticSeverity.Warning,
                "The presentation contains no slides, so no content could be produced."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Text, ContentTarget, GapScope.Failed,
                "The presentation contains no slides.",
                Impact: "No slide content is present in the extracted output."));
            await sink.WriteContentAsync(string.Empty, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // Write the deck's embedded image files first so content can link each one inline at its
        // point of occurrence — Word's convention. The gap and caveat reporting is deferred to its
        // original position below so the diagnostic order is unchanged; only the file writes move up.
        var imageResult = options.IncludeEmbeddedImages
            ? await EmbeddedImageWriter.WriteAsync(sink, options, model.Images, cancellationToken).ConfigureAwait(false)
            : null;
        var imagePaths = imageResult?.PathsBySourceRef ?? EmptyImagePaths;

        var partCount = await WriteContentAsync(sink, options, model, imagePaths, cancellationToken).ConfigureAwait(false);
        sink.ReportDocumentInfo(new DocumentInfo(
            Title: model.Slides[0].Title, Author: null, PageCount: model.Slides.Count, PartCount: partCount));

        // Report the deck's self-reported metadata for metadata.json when the reader captured it. The
        // first-slide title above is a heuristic and deliberately stays out of this authored metadata.
        if (model.Metadata is { } metadata)
        {
            sink.ReportDocumentMetadata(metadata);
        }

        // Speaker notes are the half no render can supply. A notes-less deck is well-formed and no
        // better environment would yield more, so its whole-deck absence is not a shortfall of the
        // extraction at all: it is a fact about the document, stated as a counted "0" in the content
        // outline below rather than as a diagnostic or a gap.
        var notesCount = model.Slides.Count(slide => slide.Notes is not null);

        // Report the honest gaps and caveats for the images written above
        degraded |= ReportImages(sink, options, imageResult);

        // A chart carries its plotted data in a part this backend does not read; say so rather than
        // letting the chart leave no trace at all in a deck claiming a complete extraction
        if (model.ChartsFound > 0)
        {
            ReportChartsNotExtracted(sink, model.ChartsFound);
            degraded = true;
        }

        ReportContentFeatures(sink, model, notesCount);

        return degraded;
    }

    /// <summary>
    ///     Reports the counted gap for charts the deck embeds but this backend does not read.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="count">The number of chart parts found; always greater than zero.</param>
    /// <remarks>
    ///     A chart on a slide is anchored through a graphic frame that carries neither text nor an
    ///     image blip, so it is invisible to both the shape walk and the image walk: without this gap
    ///     the quantities it plots — often the whole point of the slide — would be absent with nothing
    ///     said about them. The remedy points at the workbook route because a chart on a slide is very
    ///     often a view of a spreadsheet that this product's Excel backend extracts in full. Side
    ///     effect: records reports on the sink.
    /// </remarks>
    private static void ReportChartsNotExtracted(IExtractionSink sink, int count)
    {
        var counted = count.ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            PowerPointDiagnosticCodes.ChartsNotExtracted, DiagnosticSeverity.Warning,
            $"The deck embeds {counted} charts whose plotted data this backend does not read."));
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Text, ContentTarget, GapScope.Unavailable,
            $"The deck embeds {counted} charts. Their plotted data is stored in chart parts this backend does not "
            + "yet read, so neither the values nor the chart titles appear in the extracted content.",
            Impact: "The quantities those charts plot are not available as data or as text.",
            Remedy: "Extract the source workbook with the Excel backend, which reads a chart's cached data series "
            + "in full.",
            AffectedCount: count));
    }

    /// <summary>
    ///     Reports an outline of what the rendered <c>content.md</c> contains, counted from the model.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The deck model whose structure is counted.</param>
    /// <param name="notesCount">The number of slides carrying speaker notes, already computed by the caller.</param>
    /// <remarks>
    ///     A character count alone does not tell a paste-in reader that the deck's speaker notes —
    ///     the half no rendered slide image can supply — are present in the text, nor how many slides
    ///     carry a title worth navigating by. The counts come from the model the reader built, not
    ///     from scanning the rendered markdown. The notes count is declared as looked-for, so a deck
    ///     that carries none reads "0 sets of speaker notes" instead of dropping the line: that zero
    ///     is the whole distinction between "we read every notes slide and there are none" and
    ///     "notes are not something this backend counts", and it belongs in the inventory rather than
    ///     in a diagnostic that would pass judgement on the deck's content. Side effect: records
    ///     reports on the sink.
    /// </remarks>
    private static void ReportContentFeatures(IExtractionSink sink, PowerPointDeckModel model, int notesCount)
    {
        sink.ReportContentFeature(new ContentFeature("slides", model.Slides.Count));
        sink.ReportContentFeature(new ContentFeature(
            "slide titles", model.Slides.Count(slide => slide.Title is not null)));
        sink.ReportContentFeature(new ContentFeature(
            "sets of speaker notes", notesCount, "set of speaker notes", LookedFor: true));
        sink.ReportContentFeature(new ContentFeature(
            "inline images", model.Slides.Sum(slide => slide.Images.Count)));
    }

    /// <summary>The empty path map used when images are suppressed, so content rendering emits no links.</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyImagePaths =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    ///     Reports the honest gaps and caveats for the deck's embedded images written earlier.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for suppression and force-PNG.</param>
    /// <param name="result">The image write accounting from the earlier write, or <see langword="null"/> when images were suppressed.</param>
    /// <returns><see langword="true"/> when any image gap was reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     When images are suppressed nothing was written and Core records the suppression itself, so
    ///     this reports nothing. A deck that embeds no images reports nothing here, so a well-formed
    ///     image-free deck stays a clean success. Vector metafiles carry a readability caveat, an image
    ///     beyond a caller limit is a counted size-skip, and a force-PNG request is explained rather
    ///     than honored because this backend ships no imaging stack. Side effect: records reports on the sink.
    /// </remarks>
    private static bool ReportImages(IExtractionSink sink, ExtractionOptions options, EmbeddedImageWriteResult? result)
    {
        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit reports nothing here
        if (result is null)
        {
            return false;
        }

        // A deck that embeds no images has no images ledger to open; stay silent so it is a clean success
        if (result.Found == 0)
        {
            return false;
        }

        // Report the found denominator so the ledger can state "n of m"
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
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file, so they are written and
    ///     counted as extracted, and no better environment or configuration would yield more — this
    ///     backend deliberately ships no metafile rasterizer. The caveat is that many viewers cannot
    ///     render a Windows metafile, so it is stated for the reader without degrading the run. Side
    ///     effect: records a diagnostic on the sink.
    /// </remarks>
    private static void ReportVectorImageDiagnostic(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.VectorWrittenCount, result.Found);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            PowerPointDiagnosticCodes.VectorImageWrittenAsIs, DiagnosticSeverity.Info,
            $"{counted} embedded images are EMF or WMF vector metafiles, which many viewers cannot render. "
            + "Their bytes were written unchanged and counted as extracted."));
    }

    /// <summary>
    ///     Reports the counted gap for images skipped because they exceed a caller-supplied limit.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the size-skip count, items, and found total.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>
    ///     A size skip is a deliberate, reversible choice the caller made, so it is named as such —
    ///     distinct from a loss — and points at the option to relax. Mirrors the PDF backend's
    ///     size-skip gap. Side effect: records reports on the sink.
    /// </remarks>
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
    /// <remarks>
    ///     Core's naming rule already guarantees the file extension follows the bytes actually
    ///     written; this gap supplies the explanation. This backend ships no imaging stack, so it
    ///     cannot decode and re-encode. Mirrors the Word backend's unhonored force-PNG note. Side
    ///     effect: records reports on the sink.
    /// </remarks>
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
    ///     Writes the deck content, either as one flow or as per-slide parts, honoring the split mode.
    /// </summary>
    /// <param name="sink">The sink to write content through.</param>
    /// <param name="options">The effective options carrying the split mode.</param>
    /// <param name="model">The deck model.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of parts written, or <see langword="null"/> for a single flow.</returns>
    /// <remarks>
    ///     A single flow keeps a small deck in one readable file; per-part emits each slide under
    ///     <c>parts/</c> for a large deck. Side effect: writes content on the sink.
    /// </remarks>
    private static async ValueTask<int?> WriteContentAsync(
        IExtractionSink sink, ExtractionOptions options, PowerPointDeckModel model,
        IReadOnlyDictionary<string, string> imagePaths, CancellationToken cancellationToken)
    {
        if (options.ContentSplit == ContentSplitMode.PerPart)
        {
            foreach (var slide in model.Slides)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await sink.AddContentPartAsync(
                    new ContentPart(ContentPartKind.Slide, slide.Ordinal, TitleFor(slide)),
                    RenderSlide(slide, imagePaths), cancellationToken).ConfigureAwait(false);
            }

            return model.Slides.Count;
        }

        var builder = new StringBuilder();
        foreach (var slide in model.Slides)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(RenderSlide(slide, imagePaths));
            builder.Append('\n');
        }

        await sink.WriteContentAsync(builder.ToString().TrimEnd('\n') + "\n", cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    ///     Renders one slide to markdown: a heading with its title, its body lines, the images it
    ///     shows linked inline, and its notes.
    /// </summary>
    /// <param name="slide">The slide to render.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <returns>The slide's markdown.</returns>
    /// <remarks>
    ///     Each image the slide shows is linked at its point of occurrence — after the body text and
    ///     before the speaker notes — exactly when the sink returned a path for it, so a suppressed,
    ///     size-skipped, or deduplicated-away image never leaves a link pointing at nothing. A part
    ///     shown on several slides is linked from each, recording every reference. Pure.
    /// </remarks>
    private static string RenderSlide(PowerPointSlideModel slide, IReadOnlyDictionary<string, string> imagePaths)
    {
        var builder = new StringBuilder();
        builder.Append("## ").Append(TitleFor(slide)).Append('\n').Append('\n');

        foreach (var line in slide.TextLines)
        {
            builder.Append(line).Append('\n').Append('\n');
        }

        foreach (var image in slide.Images)
        {
            if (imagePaths.TryGetValue(image.SourceRef, out var path))
            {
                builder.Append("![").Append(AltText(image.AltText)).Append("](").Append(path).Append(")")
                    .Append('\n').Append('\n');
            }
        }

        if (slide.Notes is { } notes)
        {
            builder.Append("**Speaker notes:**").Append('\n').Append('\n');
            builder.Append(notes).Append('\n').Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Produces the alt text for an inline image link: the descriptive text, or a neutral placeholder.
    /// </summary>
    /// <param name="altText">The image's descriptive alt text, or <see langword="null"/> when none was chosen.</param>
    /// <returns>The escaped alt text, or <c>image</c> when the document gave no descriptive source.</returns>
    /// <remarks>
    ///     A neutral <c>image</c> placeholder is used rather than implying a description the document
    ///     never gave, mirroring Word. The link-structural characters are escaped so a descriptive
    ///     alt cannot break the link syntax. Pure.
    /// </remarks>
    private static string AltText(string? altText) => ImageLinkText.Alt(altText);

    /// <summary>
    ///     Produces the display title for a slide: its own title, or a stable ordinal label.
    /// </summary>
    /// <param name="slide">The slide to label.</param>
    /// <returns>The slide's title, or <c>Slide {n}</c> when it has none.</returns>
    /// <remarks>Pure.</remarks>
    private static string TitleFor(PowerPointSlideModel slide) =>
        string.IsNullOrWhiteSpace(slide.Title)
            ? $"Slide {slide.Ordinal.ToString(CultureInfo.InvariantCulture)}"
            : $"Slide {slide.Ordinal.ToString(CultureInfo.InvariantCulture)}: {slide.Title}";
}
