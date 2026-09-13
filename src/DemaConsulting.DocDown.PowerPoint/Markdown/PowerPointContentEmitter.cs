using System.Globalization;
using System.Text;
using DocDown.Core;
using DocDown.PowerPoint.OpenXml;

namespace DocDown.PowerPoint.Markdown;

/// <summary>
///     Emits a read <see cref="PowerPointDeckModel"/> through the extraction sink: per-slide content
///     in presentation order carrying title, body text, and speaker notes, plus the inventory and
///     plain-language notes the model and options imply.
/// </summary>
/// <remarks>
///     The guaranteed half of PowerPoint extraction lives here: the text of every slide, its title,
///     the speaker notes, and the deck's embedded images. Nothing is omitted silently: a deck with
///     no notes is stated as a counted zero in the content outline (an inventory fact about the
///     deck, not a shortfall of the extraction); the embedded images that are written are counted;
///     and an empty deck is described by an empty content file plus zero-count inventory. When an
///     attempted image step cannot be completed, this unit records a plain-language note rather than
///     a code-bearing judgment. Performs no filesystem I/O of its own; every byte goes through the
///     sink. Stateless and thread-safe.
/// </remarks>
internal static class PowerPointContentEmitter
{
    /// <summary>
    ///     Emits a deck model through the sink.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The deck model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>Side effect: writes content and records reports on the sink.</remarks>
    public static async ValueTask EmitAsync(
        IExtractionSink sink, ExtractionOptions options, PowerPointDeckModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        // An empty deck is a fact about the document, not a failure: write the empty content and
        // let the inventory carry the zero counts a reader may care about
        if (model.Slides.Count == 0)
        {
            await sink.WriteContentAsync(string.Empty, cancellationToken).ConfigureAwait(false);
            sink.ReportDocumentInfo(new DocumentInfo(Title: null, Author: null, PageCount: 0, PartCount: null));
            if (model.Metadata is { } emptyMetadata)
            {
                sink.ReportDocumentMetadata(emptyMetadata);
            }

            ReportContentFeatures(sink, model, notesCount: 0, EmptyImagePaths);
            return;
        }

        // Write the deck's embedded image files first so content can link each one inline at its
        // point of occurrence — Word's convention. Any note about an attempted image step is
        // deferred to its original position below so the reporting order is unchanged; only the file
        // writes move up.
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
        // outline below rather than as a note.
        var notesCount = model.Slides.Count(slide => slide.Notes is not null);

        // Report the plain-language notes for the images written above
        ReportImages(sink, options, imageResult);

        ReportContentFeatures(sink, model, notesCount, imagePaths);
    }

    /// <summary>
    ///     Reports an outline of what the rendered <c>content.md</c> contains, counted from the model.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The deck model whose structure is counted.</param>
    /// <param name="notesCount">The number of slides carrying speaker notes, already computed by the caller.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <remarks>
    ///     A character count alone does not tell a paste-in reader that the deck's speaker notes —
    ///     the half no rendered slide image can supply — are present in the text, nor how many slides
    ///     carry a title worth navigating by. The counts come from the model the reader built and
    ///     from the image paths the sink actually allocated, not from scanning the rendered markdown.
    ///     The notes count is declared as looked-for, so a deck that carries none reads
    ///     "0 sets of speaker notes" instead of dropping the line: that zero is the whole
    ///     distinction between "we read every notes slide and there are none" and
    ///     "notes are not something this backend counts", and it belongs in the inventory rather than
    ///     in a note about the extraction. Side effect: records reports on the sink.
    /// </remarks>
    private static void ReportContentFeatures(
        IExtractionSink sink, PowerPointDeckModel model, int notesCount,
        IReadOnlyDictionary<string, string> imagePaths)
    {
        sink.ReportContentFeature(new ContentFeature("slides", model.Slides.Count, "slide", LookedFor: true));
        sink.ReportContentFeature(new ContentFeature(
            "slide titles", model.Slides.Count(slide => slide.Title is not null)));
        sink.ReportContentFeature(new ContentFeature(
            "sets of speaker notes", notesCount, "set of speaker notes", LookedFor: true));
        sink.ReportContentFeature(new ContentFeature(
            "inline images",
            model.Slides.Sum(slide => slide.Images.Count(image => imagePaths.ContainsKey(image.SourceRef)))));
    }

    /// <summary>The empty path map used when images are suppressed, so content rendering emits no links.</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyImagePaths =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    ///     Reports the plain-language notes for the deck's embedded images written earlier.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for suppression and force-PNG.</param>
    /// <param name="result">The image write accounting from the earlier write, or <see langword="null"/> when images were suppressed.</param>
    /// <remarks>
    ///     When images are suppressed nothing was written and Core records the suppression itself, so
    ///     this reports nothing. A deck that embeds no images reports nothing here, so a well-formed
    ///     image-free deck stays a clean success. An image beyond a caller limit and a force-PNG
    ///     request this backend cannot honor are both attempts that could not complete and are stated
    ///     as plain-language notes. Vector metafiles written unchanged are not called out: the bytes
    ///     were produced exactly as stored. Side effect: records reports on the sink.
    /// </remarks>
    private static void ReportImages(IExtractionSink sink, ExtractionOptions options, EmbeddedImageWriteResult? result)
    {
        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit reports nothing here
        if (result is null)
        {
            return;
        }

        // A deck that embeds no images has no image step to report; stay silent so it is a clean success
        if (result.Found == 0)
        {
            return;
        }

        if (result.SizeSkippedCount > 0)
        {
            ReportSizeSkipNote(sink, result);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && result.ForcePngUnhonoredCount > 0)
        {
            ReportForcePngNote(sink, result);
        }
    }

    /// <summary>
    ///     Reports that some embedded images exceeded a caller-supplied size limit and were not written.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the size-skip count.</param>
    /// <remarks>
    ///     A size limit is an extraction-time decision, not a statement about the document, so the
    ///     skipped write is reported as a plain note about what DocDown attempted and did not
    ///     complete. Side effect: records a note on the sink.
    /// </remarks>
    private static void ReportSizeSkipNote(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var countText = result.SizeSkippedCount.ToString(CultureInfo.InvariantCulture);
        var noun = result.SizeSkippedCount == 1 ? "embedded image" : "embedded images";
        var verb = result.SizeSkippedCount == 1 ? "was" : "were";
        sink.ReportNote(new ExtractionNote(
            $"{countText} {noun} exceeded the caller-supplied size limit and {verb} not written."));
    }

    /// <summary>
    ///     Reports that a force-PNG request could not be honored and the source-encoded bytes were written instead.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the unhonored force-PNG count.</param>
    /// <remarks>
    ///     The attempted conversion is an extraction fact rather than a judgment, so this backend
    ///     records a plain note and keeps the successfully written source-encoded image files.
    ///     Side effect: records a note on the sink.
    /// </remarks>
    private static void ReportForcePngNote(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var countText = result.ForcePngUnhonoredCount.ToString(CultureInfo.InvariantCulture);
        var noun = result.ForcePngUnhonoredCount == 1 ? "embedded image" : "embedded images";
        var verb = result.ForcePngUnhonoredCount == 1 ? "was" : "were";
        sink.ReportNote(new ExtractionNote(
            $"PNG output was requested, but this backend could not convert {countText} {noun}; "
            + $"source-encoded files {verb} written instead."));
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
