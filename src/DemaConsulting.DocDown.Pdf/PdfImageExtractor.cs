using System.Globalization;
using DocDown.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace DocDown.Pdf;

/// <summary>
///     Extracts the embedded images of a PDF through the sink, choosing an encoding per image and
///     accounting honestly for every image it could not deliver.
/// </summary>
/// <remarks>
///     <para>
///         The central design decision is that this unit never writes bytes under an extension that
///         misrepresents them and never describes bytes as something they are not. What may be written
///         verbatim is decided by an explicit table — <see cref="FilterTable"/> — rather than inferred
///         from control flow, because the distinction it encodes is easy to get wrong and expensive
///         to get wrong.
///     </para>
///     <para>
///         <strong>Standing warning to any future maintainer: the raw stored bytes of a PDF image
///         XObject are, for every filter except <c>DCTDecode</c> and <c>JPXDecode</c>, compressed
///         pixel data and not a file format.</strong> There is no file extension that makes them
///         viewable, because interpreting them requires the width, height, color space, and
///         bits-per-component that live in the image dictionary and not in the stream. Do not
///         "optimize" those rows into a passthrough by dumping <c>RawBytes</c> to a file: the result
///         is a file that opens in nothing, which is the exact failure this unit exists to prevent. The
///         only honest routes for those rows are a genuine decode-and-re-encode to PNG, or a counted
///         gap saying the image could not be delivered.
///     </para>
///     <para>
///         The two exceptions are real image containers. A <c>DCTDecode</c> stream is already a
///         complete JPEG file, so it is written through unchanged as <c>.jpg</c> and reported as a
///         passthrough — a claim that is exactly true and, because the test suite compares the written
///         file against the embedded source bytes, one that is falsifiable rather than decorative. A
///         <c>JPXDecode</c> stream is a JPEG 2000 codestream; this library takes no JPEG 2000 decoder,
///         so the bytes are written unchanged as <c>.jp2</c> and likewise labeled a passthrough, with
///         a gap warning that many viewers and image libraries cannot read that format. A file with a
///         caveat is more useful to a multimodal consumer than no file at all, provided the caveat is
///         never omitted.
///     </para>
///     <para>
///         Encodings that are neither container nor decodable — <c>JBIG2Decode</c> and, where the
///         parser refuses them, <c>CCITTFaxDecode</c> — are counted, grouped by encoding name, and
///         reported as a gap that says how many were lost and why. The same accounting reports
///         size-limit skips separately, so a caller can never mistake a deliberate size skip for a
///         decoding failure, nor either of those for an image that was written with a caveat.
///     </para>
///     <para>
///         Performs no filesystem I/O of its own: every byte goes through the sink. Stateless and
///         thread-safe; the running accounting lives for the duration of a single call.
///     </para>
/// </remarks>
internal static class PdfImageExtractor
{
    /// <summary>
    ///     What the raw stored bytes of an image XObject actually are, per PDF filter.
    /// </summary>
    /// <remarks>
    ///     This is the classification the whole unit turns on, stated as a table rather than left to
    ///     be reconstructed from branches. Only the first two rows may ever be written verbatim; the
    ///     rest are compressed pixel data whose interpretation depends on dictionary fields the stream
    ///     does not carry. The media type is non-null exactly for the rows whose bytes are a file, and
    ///     that is what makes "may this be passed through?" a lookup rather than a judgment call.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, FilterClassification> FilterTable =
        new Dictionary<string, FilterClassification>(StringComparer.Ordinal)
        {
            // A complete JPEG file: write it straight to .jpg, byte-identical
            [DctDecodeFilter] = new(RawBytesMeaning.CompleteImageFile, "image/jpeg"),

            // A JPEG 2000 codestream: a real container we cannot decode, written to .jp2 with a caveat
            [JpxDecodeFilter] = new(RawBytesMeaning.UndecodableImageFile, "image/jp2"),

            // zlib over raw pixel samples: decode and re-encode, never pass through
            ["FlateDecode"] = new(RawBytesMeaning.CompressedSamples, null),

            // Compressed raw samples, as Flate
            ["LZWDecode"] = new(RawBytesMeaning.CompressedSamples, null),
            ["RunLengthDecode"] = new(RawBytesMeaning.CompressedSamples, null),

            // A bare fax bitstream with no container: needs a TIFF wrapper to be viewable at all
            ["CCITTFaxDecode"] = new(RawBytesMeaning.ContainerlessBitstream, null),

            // An embedded JBIG2 segment with no container, as CCITT
            ["JBIG2Decode"] = new(RawBytesMeaning.ContainerlessBitstream, null),

            // No filter declared: uncompressed samples, which still need encoding to be viewable
            [NoFilterName] = new(RawBytesMeaning.UncompressedSamples, null)
        };

    /// <summary>The classification applied to a filter this table does not name.</summary>
    /// <remarks>
    ///     An unrecognized filter is assumed to hold samples rather than a file, because that is both
    ///     the overwhelmingly common case and the safe direction to be wrong in: the worst outcome is
    ///     an explained gap, whereas guessing "file" would write bytes under an extension that lies.
    /// </remarks>
    private static readonly FilterClassification UnlistedFilter = new(RawBytesMeaning.CompressedSamples, null);

    /// <summary>The PDF filter name whose stored stream is already a complete JPEG file.</summary>
    /// <remarks>Named once so the passthrough decision and the <c>ForcePng</c> explanation agree.</remarks>
    private const string DctDecodeFilter = "DCTDecode";

    /// <summary>The PDF filter name whose stored stream is a JPEG 2000 codestream.</summary>
    /// <remarks>Named once so the passthrough decision and the readability caveat agree.</remarks>
    private const string JpxDecodeFilter = "JPXDecode";

    /// <summary>The name used for an image whose dictionary declares no filter at all.</summary>
    /// <remarks>
    ///     Such an image holds uncompressed samples. The name doubles as the grouping key in gap text,
    ///     so it is deliberately a word a reader can make sense of rather than an empty string.
    /// </remarks>
    private const string NoFilterName = "unknown";

    /// <summary>The maximum number of affected item references recorded on any one gap.</summary>
    /// <remarks>
    ///     Bounds the gap text for a pathological document while still naming enough items to be
    ///     actionable; the count is always exact even when the item list is truncated.
    /// </remarks>
    private const int MaxAffectedItems = 20;

    /// <summary>
    ///     Extracts every embedded image on the selected pages, returning the links and the accounting.
    /// </summary>
    /// <param name="pages">The pages to extract from, already restricted to any requested range. Must not be null.</param>
    /// <param name="sink">The sink every image is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options governing suppression and limits. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     The written images with the relative paths to link them by, together with the number of
    ///     images found and written. Returns an empty, zero-count result when embedded-image
    ///     extraction is disabled.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Reports the found count, and any decode-failure, size-skip, or unhonored-<c>ForcePng</c>
    ///     gaps, before returning, so the sink's ledger denominators and explanations are complete by
    ///     the time the engine reconciles. Side effect: writes images and records reports on the sink.
    /// </remarks>
    internal static async ValueTask<PdfImageResult> ExtractAsync(
        IReadOnlyList<Page> pages, IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);

        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit must not decode, count, or explain anything here
        if (!options.IncludeEmbeddedImages)
        {
            return PdfImageResult.Empty;
        }

        var accounting = new ImageAccounting();
        var written = new List<PdfExtractedImage>();

        // Walk the selected pages in order so image ordinals follow document order
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var image in page.GetImages())
            {
                accounting.Found++;
                var extracted = await WriteImageAsync(page, image, accounting, sink, options, cancellationToken)
                    .ConfigureAwait(false);
                if (extracted is not null)
                {
                    written.Add(extracted);
                }
            }
        }

        // Report the denominator unconditionally, including zero, so the ledger can state "n of m"
        sink.ReportFound(GapKind.Images, accounting.Found);
        ReportAccountingGaps(sink, options, accounting, written.Count);
        return new PdfImageResult(written, accounting.Found, written.Count);
    }

    /// <summary>
    ///     Writes one image through the sink, or records why it could not be written.
    /// </summary>
    /// <param name="page">The page the image was found on, used for provenance.</param>
    /// <param name="image">The image to write.</param>
    /// <param name="accounting">The running accounting to record a skip or failure into.</param>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="options">The effective options governing the size limits and output mode.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The written image, or <see langword="null"/> when nothing was written.</returns>
    /// <remarks>
    ///     Applies the size limits first — a skip for size is a different fact from a decode failure
    ///     and must not be conflated with one — then classifies the filter and chooses the encoding.
    ///     A written image whose bytes are a container this extractor cannot decode is recorded as a
    ///     readability caveat rather than as a loss, because the file is there. Side effect: writes
    ///     through the sink and mutates <paramref name="accounting"/>.
    /// </remarks>
    private static async ValueTask<PdfExtractedImage?> WriteImageAsync(
        Page page, IPdfImage image, ImageAccounting accounting,
        IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        var reference = DescribeImage(page.Number, accounting.Found);

        // A dimension beyond the caller's limit is skipped deliberately, not failed
        if (options.MaxImageDimensionPx is { } maxDimension
            && (image.WidthInSamples > maxDimension || image.HeightInSamples > maxDimension))
        {
            accounting.RecordSizeSkip(reference);
            return null;
        }

        // Classify the filter first: what the stored bytes are decides everything that follows
        var filter = FilterNameOf(image);
        var classification = Classify(filter);
        var encoded = Encode(image, classification);
        if (encoded is null)
        {
            accounting.RecordUndecodable(filter, reference);
            return null;
        }

        // A payload beyond the caller's byte budget is likewise a deliberate skip
        if (options.MaxImageBytes is { } maxBytes && encoded.Value.Bytes.LongLength > maxBytes)
        {
            accounting.RecordSizeSkip(reference);
            return null;
        }

        // Hand the bytes to Core, which allocates the name, deduplicates, and records the provenance
        var hint = new ImageHint(
            PreferredName: null,
            MediaType: encoded.Value.MediaType,
            WidthPx: image.WidthInSamples,
            HeightPx: image.HeightInSamples,
            SourcePage: page.Number,
            SourceRef: reference,
            Transform: encoded.Value.Transform);
        using var stream = new MemoryStream(encoded.Value.Bytes, writable: false);
        var path = await sink.AddImageAsync(stream, hint, cancellationToken).ConfigureAwait(false);

        // An empty path means the sink declined to store the image; emit no link for it
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // A written file whose format many consumers cannot open is delivered, but not silently:
        // the caveat is the only thing standing between a useful artifact and a misleading one
        if (classification.Meaning == RawBytesMeaning.UndecodableImageFile)
        {
            accounting.RecordLimitedReadability(filter, reference);
        }

        // Record an image written in its source encoding under a PNG request so the mode can be explained
        if (options.ImageOutput == ImageOutputMode.ForcePng && encoded.Value.Transform != ImageTransform.DecodedToPng)
        {
            accounting.RecordUnhonoredForcePng(filter, reference);
        }

        return new PdfExtractedImage(page.Number, path, reference);
    }

    /// <summary>
    ///     Looks up what the raw stored bytes of an image with the given filter actually are.
    /// </summary>
    /// <param name="filterName">The effective PDF filter name, or <see cref="NoFilterName"/>.</param>
    /// <returns>The classification for that filter, or the conservative default for an unlisted one.</returns>
    /// <remarks>
    ///     A lookup rather than a chain of comparisons, so the classification can be read as the table
    ///     it is and a new filter is added in one place. Pure.
    /// </remarks>
    private static FilterClassification Classify(string filterName) =>
        FilterTable.TryGetValue(filterName, out var classification) ? classification : UnlistedFilter;

    /// <summary>
    ///     Chooses the bytes, media type, and honest transform label for one image.
    /// </summary>
    /// <param name="image">The image to encode.</param>
    /// <param name="classification">The classification of what the image's raw bytes are.</param>
    /// <returns>The encoded payload, or <see langword="null"/> when this extractor cannot deliver it.</returns>
    /// <remarks>
    ///     <para>
    ///         The two rows whose raw bytes are a complete image file — a JPEG stream and a JPEG 2000
    ///         codestream — are returned untouched and labeled
    ///         <see cref="ImageTransform.Passthrough"/>, because that is exactly what happened to them.
    ///         Core derives the extension from the media type, so the bytes on disk and the name they
    ///         are under always agree.
    ///     </para>
    ///     <para>
    ///         <strong>Every other row must go through <c>TryGetPng</c>, which decodes the samples and
    ///         re-encodes them. Their raw bytes are compressed pixel data, not a file format: no
    ///         extension makes them viewable, because interpreting them requires the width, height,
    ///         color space, and bits-per-component held in the image dictionary rather than in the
    ///         stream.</strong> A refusal from <c>TryGetPng</c> therefore yields <see langword="null"/>
    ///         rather than raw bytes, because a file no consumer can open is worse than an explained
    ///         absence. Pure apart from reading the image.
    ///     </para>
    /// </remarks>
    private static EncodedImage? Encode(IPdfImage image, FilterClassification classification)
    {
        // A non-null media type marks the rows whose stored bytes are genuinely a file; those, and
        // only those, may be written verbatim without misrepresenting what is on disk
        if (classification.MediaType is { } mediaType)
        {
            return new EncodedImage(image.RawBytes.ToArray(), mediaType, ImageTransform.Passthrough);
        }

        // Everything else stores compressed or raw samples rather than an image file, so producing a
        // usable artifact necessarily means decoding and re-encoding as PNG
        return image.TryGetPng(out var png) && png is { Length: > 0 }
            ? new EncodedImage(png, "image/png", ImageTransform.DecodedToPng)
            : null;
    }

    /// <summary>
    ///     Reads the effective PDF filter name of an image.
    /// </summary>
    /// <param name="image">The image whose dictionary is inspected.</param>
    /// <returns>The last filter in the chain, or <see cref="NoFilterName"/> when none is declared.</returns>
    /// <remarks>
    ///     The last entry of a filter array is the image encoding; earlier entries are transport
    ///     compressions PdfPig has already undone. Both the full <c>Filter</c> key and its inline
    ///     <c>F</c> abbreviation are consulted so inline images are named as precisely as XObjects.
    ///     Pure.
    /// </remarks>
    private static string FilterNameOf(IPdfImage image)
    {
        var dictionary = image.ImageDictionary;
        if (dictionary is null)
        {
            return NoFilterName;
        }

        // Prefer the full key; inline images abbreviate it, so fall back to the short form
        if (!dictionary.TryGet<IToken>(NameToken.Filter, out var filter) || filter is null)
        {
            dictionary.TryGet<IToken>(NameToken.F, out filter);
        }

        return filter switch
        {
            NameToken name => name.Data,
            ArrayToken { Length: > 0 } array when array.Data[^1] is NameToken last => last.Data,
            _ => NoFilterName
        };
    }

    /// <summary>
    ///     Builds the stable reference that identifies an image within the document.
    /// </summary>
    /// <param name="pageNumber">The 1-based page the image was found on.</param>
    /// <param name="ordinal">The 1-based discovery ordinal across the whole extraction.</param>
    /// <returns>A reference such as <c>page 2 image 3</c>.</returns>
    /// <remarks>
    ///     PdfPig does not surface the XObject's resource name, so the page and discovery ordinal are
    ///     used instead: they are stable for a given document and page range, which keeps gap text
    ///     and manifest provenance byte-deterministic across runs. Pure.
    /// </remarks>
    private static string DescribeImage(int pageNumber, int ordinal) =>
        string.Format(CultureInfo.InvariantCulture, "page {0} image {1}", pageNumber, ordinal);

    /// <summary>
    ///     Reports the decode-failure, readability-caveat, size-skip, and unhonored-<c>ForcePng</c> gaps.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for the requested output mode.</param>
    /// <param name="accounting">The completed accounting for the run.</param>
    /// <param name="written">The number of images actually written.</param>
    /// <remarks>
    ///     Each condition gets its own gap so a reader can tell a decode failure from an image written
    ///     with a caveat from a size skip from an unhonored option; collapsing them into one would be
    ///     less informative than the counts deserve, and would blur the line between an image that is
    ///     absent and one that is present but awkward to open. Side effect: records diagnostics and
    ///     gaps on the sink.
    /// </remarks>
    private static void ReportAccountingGaps(
        IExtractionSink sink, ExtractionOptions options, ImageAccounting accounting, int written)
    {
        if (accounting.UndecodableCount > 0)
        {
            ReportUndecodableGap(sink, accounting, written);
        }

        if (accounting.LimitedReadabilityCount > 0)
        {
            ReportLimitedReadabilityGap(sink, accounting);
        }

        if (accounting.SizeSkippedCount > 0)
        {
            ReportSizeSkipGap(sink, accounting, written);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && accounting.UnhonoredForcePngCount > 0)
        {
            ReportUnhonoredForcePngGap(sink, accounting);
        }
    }

    /// <summary>
    ///     Reports the counted gap for images whose encoding this extractor cannot decode.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="accounting">The completed accounting supplying the counts and encoding names.</param>
    /// <param name="written">The number of images actually written.</param>
    /// <remarks>
    ///     The scope distinguishes a partial extraction from a total one: when nothing was written the
    ///     folder is genuinely empty, and calling that "partially extracted" would both overstate the
    ///     result and contradict the contract verifier's rule that a partial folder holds at least one
    ///     file. Side effect: records on the sink.
    /// </remarks>
    private static void ReportUndecodableGap(IExtractionSink sink, ImageAccounting accounting, int written)
    {
        var encodings = accounting.DescribeUndecodableEncodings();
        var counted = Counted(accounting.UndecodableCount, accounting.Found);

        sink.ReportDiagnostic(new ExtractionDiagnostic(
            PdfDiagnosticCodes.UndecodableImageEncoding, DiagnosticSeverity.Warning,
            $"{counted} embedded images use an encoding this extractor cannot decode."));

        sink.ReportGap(new ExtractionGap(
            Id: string.Empty,
            Kind: GapKind.Images,
            Target: ImagesTarget,
            Scope: written > 0 ? GapScope.PartiallyExtracted : GapScope.Unavailable,
            Reason: $"{counted} embedded images use encodings this extractor cannot decode "
                  + $"({encodings}); they were not written.",
            Impact: "The information in those images is not available in the extracted output.",
            Remedy: null,
            AffectedCount: accounting.UndecodableCount,
            AffectedItems: accounting.UndecodableItems));
    }

    /// <summary>
    ///     Reports the counted caveat for images written in a format many consumers cannot read.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="accounting">The completed accounting supplying the counts and encoding names.</param>
    /// <remarks>
    ///     These images are <em>written</em>, so this is a caveat and not a loss: it is deliberately
    ///     kept out of the undecodable count, whose denominator has to keep meaning "images that never
    ///     reached the output". The scope is always partial because the gap is only reported when at
    ///     least one such file exists on disk, which is what the contract verifier's rule requires.
    ///     The prose names JPEG 2000 because that is the sole row of <see cref="FilterTable"/>
    ///     classified <see cref="RawBytesMeaning.UndecodableImageFile"/>; adding another such row means
    ///     revisiting this wording. Side effect: records on the sink.
    /// </remarks>
    private static void ReportLimitedReadabilityGap(IExtractionSink sink, ImageAccounting accounting)
    {
        var encodings = accounting.DescribeLimitedReadabilityEncodings();
        var counted = Counted(accounting.LimitedReadabilityCount, accounting.Found);

        sink.ReportDiagnostic(new ExtractionDiagnostic(
            PdfDiagnosticCodes.Jpeg2000WrittenAsIs, DiagnosticSeverity.Warning,
            $"{counted} embedded images were written as JPEG 2000 files, which many viewers cannot read."));

        sink.ReportGap(new ExtractionGap(
            Id: string.Empty,
            Kind: GapKind.Images,
            Target: ImagesTarget,
            Scope: GapScope.PartiallyExtracted,
            Reason: $"{counted} embedded images use the JPEG 2000 encoding ({encodings}); this extractor "
                  + "does not decode it, so their codestream was written unchanged with a .jp2 extension. "
                  + "Many image viewers and image libraries cannot read JPEG 2000 files.",
            Impact: "Those images are present in the extracted output, but a consumer without JPEG 2000 "
                  + "support may be unable to open them.",
            Remedy: "Convert the .jp2 files with a JPEG 2000-capable tool if your consumer cannot read them.",
            AffectedCount: accounting.LimitedReadabilityCount,
            AffectedItems: accounting.LimitedReadabilityItems));
    }

    /// <summary>
    ///     Reports the counted gap for images skipped because they exceed a caller-supplied limit.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="accounting">The completed accounting supplying the counts and references.</param>
    /// <param name="written">The number of images actually written.</param>
    /// <remarks>
    ///     Kept separate from the decode-failure gap because the two have different remedies: a size
    ///     skip is undone by relaxing an option the caller chose, whereas a decode failure is a
    ///     property of the document. The remedy names the options rather than instructing an install.
    ///     Side effect: records on the sink.
    /// </remarks>
    private static void ReportSizeSkipGap(IExtractionSink sink, ImageAccounting accounting, int written)
    {
        var counted = Counted(accounting.SizeSkippedCount, accounting.Found);
        sink.ReportGap(new ExtractionGap(
            Id: string.Empty,
            Kind: GapKind.Images,
            Target: ImagesTarget,
            Scope: written > 0 ? GapScope.PartiallyExtracted : GapScope.Unavailable,
            Reason: $"{counted} embedded images exceed the caller's image size or dimension limit; "
                  + "they were not written.",
            Impact: "Those images are not available in the extracted output.",
            Remedy: "Raise or clear MaxImageBytes and MaxImageDimensionPx to extract images of any size.",
            AffectedCount: accounting.SizeSkippedCount,
            AffectedItems: accounting.SizeSkippedItems));
    }

    /// <summary>
    ///     Reports the counted gap explaining why PNG output could not be honored for some images.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="accounting">The completed accounting supplying the count and references.</param>
    /// <remarks>
    ///     Core's naming rule already guarantees the file on disk is not mislabeled: the extension
    ///     follows the bytes actually written, never the requested format. What is missing without
    ///     this gap is the <em>explanation</em> — a caller who asked for PNG and received JPEG would
    ///     otherwise have to infer why. The encodings are named from what was actually written rather
    ///     than assumed to be JPEG, because a JPEG 2000 passthrough defeats the request in exactly the
    ///     same way and naming the wrong encoding would be its own small dishonesty. Side effect:
    ///     records on the sink.
    /// </remarks>
    private static void ReportUnhonoredForcePngGap(IExtractionSink sink, ImageAccounting accounting)
    {
        var counted = Counted(accounting.UnhonoredForcePngCount, accounting.Found);
        var encodings = accounting.DescribeUnhonoredForcePngEncodings();

        sink.ReportDiagnostic(new ExtractionDiagnostic(
            PdfDiagnosticCodes.ForcePngNotHonored, DiagnosticSeverity.Warning,
            $"PNG output was requested but {accounting.UnhonoredForcePngCount.ToString(CultureInfo.InvariantCulture)} "
            + "images were written in their source encoding."));

        sink.ReportGap(new ExtractionGap(
            Id: string.Empty,
            Kind: GapKind.Images,
            Target: ImagesTarget,
            Scope: GapScope.PartiallyExtracted,
            Reason: $"PNG output was requested, but {counted} embedded images use an encoding this "
                  + $"extractor does not decode ({encodings}); they were written in their source "
                  + "encoding with a matching file extension rather than converted.",
            Impact: "Those images are not in the requested PNG format; their file extensions and the "
                  + "manifest media types describe what was actually written.",
            Remedy: null,
            AffectedCount: accounting.UnhonoredForcePngCount,
            AffectedItems: accounting.UnhonoredForcePngItems));
    }

    /// <summary>The ledger path every image gap must name for the contract verifier to accept it.</summary>
    /// <remarks>
    ///     Held as a constant because a gap whose target does not match the ledger entry exactly is
    ///     reported by the verifier as an unexplained absence, so the spelling is load-bearing.
    /// </remarks>
    private const string ImagesTarget = "images/";

    /// <summary>
    ///     Formats an "n of m" phrase with invariant digits.
    /// </summary>
    /// <param name="count">The affected count.</param>
    /// <param name="found">The total number of images found.</param>
    /// <returns>A phrase such as <c>2 of 8</c>.</returns>
    /// <remarks>Invariant formatting keeps gap text byte-identical across locales. Pure.</remarks>
    private static string Counted(int count, int found) => string.Format(
        CultureInfo.InvariantCulture, "{0} of {1}", count, found);

    /// <summary>
    ///     The running tally of what an image extraction found, skipped, and could not decode.
    /// </summary>
    /// <remarks>
    ///     Separated from the extraction walk so the gap-reporting rules can be read and tested as one
    ///     coherent accounting policy rather than as side effects scattered through a loop. Encoding
    ///     names are held in a sorted map and item lists are appended in document order, which is what
    ///     makes the resulting gap text byte-deterministic across runs. Not thread-safe.
    /// </remarks>
    private sealed class ImageAccounting
    {
        /// <summary>Counts of undecodable images by PDF filter name, sorted for deterministic text.</summary>
        private readonly SortedDictionary<string, int> _undecodableByEncoding = new(StringComparer.Ordinal);

        /// <summary>The references of undecodable images, in document order and bounded in length.</summary>
        private readonly List<string> _undecodableItems = [];

        /// <summary>The references of images skipped for size, in document order and bounded in length.</summary>
        private readonly List<string> _sizeSkippedItems = [];

        /// <summary>Counts of images written with a readability caveat, by PDF filter name.</summary>
        private readonly SortedDictionary<string, int> _limitedReadabilityByEncoding = new(StringComparer.Ordinal);

        /// <summary>The references of images written with a readability caveat, in document order.</summary>
        private readonly List<string> _limitedReadabilityItems = [];

        /// <summary>Counts of images that defeated a PNG request, by PDF filter name.</summary>
        private readonly SortedDictionary<string, int> _unhonoredForcePngByEncoding = new(StringComparer.Ordinal);

        /// <summary>The references of images written in a non-PNG encoding under a PNG request.</summary>
        private readonly List<string> _unhonoredForcePngItems = [];

        /// <summary>Gets or sets the number of images found, counting every image on every selected page.</summary>
        /// <remarks>The ledger denominator; incremented before any decision is taken about an image.</remarks>
        public int Found { get; set; }

        /// <summary>Gets the number of images that could not be decoded and were not written.</summary>
        /// <remarks>
        ///     Counts losses only. An image written in a format many viewers cannot read is not counted
        ///     here, because it is on disk and this number is what the "not written" gap reports.
        /// </remarks>
        public int UndecodableCount { get; private set; }

        /// <summary>Gets the number of images skipped because of a caller size limit.</summary>
        public int SizeSkippedCount { get; private set; }

        /// <summary>Gets the number of images written in a format many consumers cannot read.</summary>
        public int LimitedReadabilityCount { get; private set; }

        /// <summary>Gets the number of images written in their source encoding despite a PNG request.</summary>
        public int UnhonoredForcePngCount { get; private set; }

        /// <summary>Gets the bounded, ordered references of the undecodable images.</summary>
        public IReadOnlyList<string> UndecodableItems => _undecodableItems;

        /// <summary>Gets the bounded, ordered references of the size-skipped images.</summary>
        public IReadOnlyList<string> SizeSkippedItems => _sizeSkippedItems;

        /// <summary>Gets the bounded, ordered references of the images written with a readability caveat.</summary>
        public IReadOnlyList<string> LimitedReadabilityItems => _limitedReadabilityItems;

        /// <summary>Gets the bounded, ordered references of the images that defeated the PNG request.</summary>
        public IReadOnlyList<string> UnhonoredForcePngItems => _unhonoredForcePngItems;

        /// <summary>
        ///     Records an image this extractor cannot decode, grouped by its encoding.
        /// </summary>
        /// <param name="encoding">The PDF filter name that could not be decoded.</param>
        /// <param name="reference">The image's stable reference.</param>
        /// <remarks>Grouping by encoding is what lets the gap name the reason rather than just a count.</remarks>
        public void RecordUndecodable(string encoding, string reference)
        {
            UndecodableCount++;
            _undecodableByEncoding[encoding] = _undecodableByEncoding.GetValueOrDefault(encoding) + 1;
            Append(_undecodableItems, reference);
        }

        /// <summary>
        ///     Records an image skipped because it exceeds a caller-supplied limit.
        /// </summary>
        /// <param name="reference">The image's stable reference.</param>
        /// <remarks>Tracked apart from decode failures so the two are never conflated in a gap.</remarks>
        public void RecordSizeSkip(string reference)
        {
            SizeSkippedCount++;
            Append(_sizeSkippedItems, reference);
        }

        /// <summary>
        ///     Records an image that was written but in a format many consumers cannot read.
        /// </summary>
        /// <param name="encoding">The PDF filter name the bytes were written in.</param>
        /// <param name="reference">The image's stable reference.</param>
        /// <remarks>
        ///     Deliberately separate from <see cref="RecordUndecodable"/>: this image is in the output,
        ///     so counting it as a loss would understate what was extracted, and omitting it entirely
        ///     would leave a file no one warned the consumer about.
        /// </remarks>
        public void RecordLimitedReadability(string encoding, string reference)
        {
            LimitedReadabilityCount++;
            _limitedReadabilityByEncoding[encoding] = _limitedReadabilityByEncoding.GetValueOrDefault(encoding) + 1;
            Append(_limitedReadabilityItems, reference);
        }

        /// <summary>
        ///     Records an image written in its source encoding despite a PNG request.
        /// </summary>
        /// <param name="encoding">The PDF filter name the image was written in.</param>
        /// <param name="reference">The image's stable reference.</param>
        /// <remarks>
        ///     Drives the explanation that keeps <c>ForcePng</c> from failing silently; the encoding is
        ///     carried so the explanation names what actually defeated the request.
        /// </remarks>
        public void RecordUnhonoredForcePng(string encoding, string reference)
        {
            UnhonoredForcePngCount++;
            _unhonoredForcePngByEncoding[encoding] = _unhonoredForcePngByEncoding.GetValueOrDefault(encoding) + 1;
            Append(_unhonoredForcePngItems, reference);
        }

        /// <summary>
        ///     Renders the undecodable encodings and their counts as deterministic text.
        /// </summary>
        /// <returns>A phrase such as <c>JBIG2Decode: 1; JPXDecode: 2</c>.</returns>
        /// <remarks>
        ///     Sorted by encoding name so two runs over the same document produce byte-identical gap
        ///     text regardless of the order the images happened to be visited. Pure.
        /// </remarks>
        public string DescribeUndecodableEncodings() => Describe(_undecodableByEncoding);

        /// <summary>
        ///     Renders the caveated encodings and their counts as deterministic text.
        /// </summary>
        /// <returns>A phrase such as <c>JPXDecode: 1</c>.</returns>
        /// <remarks>Sorted for the same byte-determinism reason as the undecodable list. Pure.</remarks>
        public string DescribeLimitedReadabilityEncodings() => Describe(_limitedReadabilityByEncoding);

        /// <summary>
        ///     Renders the encodings that defeated a PNG request and their counts as deterministic text.
        /// </summary>
        /// <returns>A phrase such as <c>DCTDecode: 1</c>.</returns>
        /// <remarks>Sorted for the same byte-determinism reason as the undecodable list. Pure.</remarks>
        public string DescribeUnhonoredForcePngEncodings() => Describe(_unhonoredForcePngByEncoding);

        /// <summary>
        ///     Renders an encoding tally as deterministic text.
        /// </summary>
        /// <param name="tally">The encoding counts to render, already sorted by name.</param>
        /// <returns>A phrase such as <c>DCTDecode: 1; JPXDecode: 2</c>.</returns>
        /// <remarks>Shared so every gap names its encodings in one recognizable shape. Pure.</remarks>
        private static string Describe(SortedDictionary<string, int> tally) => string.Join("; ", tally.Select(
            entry => string.Format(CultureInfo.InvariantCulture, "{0}: {1}", entry.Key, entry.Value)));

        /// <summary>
        ///     Appends a reference to a bounded list.
        /// </summary>
        /// <param name="items">The list to append to.</param>
        /// <param name="reference">The reference to append.</param>
        /// <remarks>Bounded so a pathological document cannot produce an unreadable gap. Pure apart from the append.</remarks>
        private static void Append(List<string> items, string reference)
        {
            if (items.Count < MaxAffectedItems)
            {
                items.Add(reference);
            }
        }
    }

    /// <summary>
    ///     What the raw stored bytes of an image XObject are, independent of how they are handled.
    /// </summary>
    /// <remarks>
    ///     Named rather than implied so a reader can see the distinction the unit turns on: only the
    ///     first two members are image <em>files</em>. The remaining members are pixel data whose
    ///     interpretation needs the width, height, color space, and bits-per-component that live in
    ///     the image dictionary, so no file extension can make them viewable. They are kept distinct
    ///     from one another even though they share a route, because the reason each is not a file
    ///     differs and a maintainer deciding what to do about one should not have to guess.
    /// </remarks>
    private enum RawBytesMeaning
    {
        /// <summary>A complete, self-describing image file that consumers can widely open.</summary>
        CompleteImageFile,

        /// <summary>A complete image file in a format this extractor cannot decode and many tools cannot read.</summary>
        UndecodableImageFile,

        /// <summary>Compressed pixel samples: not a file, and not viewable under any extension.</summary>
        CompressedSamples,

        /// <summary>A bare bitstream with no container: needs a wrapper, such as TIFF, to be viewable.</summary>
        ContainerlessBitstream,

        /// <summary>Uncompressed pixel samples: not a file, and not viewable under any extension.</summary>
        UncompressedSamples
    }

    /// <summary>
    ///     One row of the filter table: what a filter's raw bytes are and, if they are a file, its type.
    /// </summary>
    /// <param name="Meaning">What the raw stored bytes actually are.</param>
    /// <param name="MediaType">
    ///     The media type of the raw bytes when they constitute a file, or <see langword="null"/> when
    ///     they do not and must therefore never be written verbatim.
    /// </param>
    /// <remarks>
    ///     The null media type is load-bearing rather than incidental: it is the single fact that makes
    ///     "may these bytes be passed through?" answerable without re-deriving the classification.
    ///     Immutable and thread-safe.
    /// </remarks>
    private sealed record FilterClassification(RawBytesMeaning Meaning, string? MediaType);

    /// <summary>
    ///     The bytes, media type, and honest provenance label chosen for one image.
    /// </summary>
    /// <param name="Bytes">The bytes to write.</param>
    /// <param name="MediaType">The media type those bytes actually are.</param>
    /// <param name="Transform">How the bytes were produced, as reported to the sink.</param>
    /// <remarks>
    ///     The three travel together because they must agree: a mismatch between them is precisely
    ///     the dishonesty this unit exists to avoid. Immutable and thread-safe.
    /// </remarks>
    private readonly record struct EncodedImage(byte[] Bytes, string MediaType, ImageTransform Transform);
}

/// <summary>
///     One embedded image that was successfully written, with the path to link it by.
/// </summary>
/// <param name="PageNumber">The 1-based page the image was found on.</param>
/// <param name="RelativePath">The relative path the sink allocated, used in the markdown link.</param>
/// <param name="Description">The stable reference identifying the image, used as link text.</param>
/// <remarks>
///     Returned rather than linked in place so the text extractor decides where in the markdown flow
///     each image appears. Immutable and thread-safe.
/// </remarks>
internal sealed record PdfExtractedImage(int PageNumber, string RelativePath, string Description);

/// <summary>
///     The outcome of extracting a document's embedded images.
/// </summary>
/// <param name="Images">The images written, in document order.</param>
/// <param name="Found">The number of images found across the selected pages.</param>
/// <param name="Written">The number of images actually written.</param>
/// <remarks>
///     Carries the found and written counts alongside the links so the caller can describe the
///     extraction without recounting. Immutable and thread-safe.
/// </remarks>
internal sealed record PdfImageResult(IReadOnlyList<PdfExtractedImage> Images, int Found, int Written)
{
    /// <summary>The result of an extraction that attempted nothing.</summary>
    /// <remarks>Used when embedded-image extraction is disabled, where Core owns the explanation.</remarks>
    public static PdfImageResult Empty { get; } = new([], 0, 0);
}
