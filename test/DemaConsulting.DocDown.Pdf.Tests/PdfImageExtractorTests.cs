using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for the PDF image extractor, proving the encoding decision, the provenance it
///     reports for each decision, the counted gaps for what it could not deliver, suppression, and
///     the size limits.
/// </summary>
/// <remarks>
///     These tests drive the internal extractor directly against a <see cref="RecordingSink"/>, so
///     the <see cref="ImageHint"/> the unit actually supplied can be inspected rather than inferred
///     from the manifest. That matters most for provenance: the system tests prove the label reaches
///     the manifest, and these prove the label originated here, honestly, per image.
/// </remarks>
public class PdfImageExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a stored JPEG is passed through unchanged and labeled as such (JpegPassthrough).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_AddImage_DctImage_SetsPassthroughTransformHint()
    {
        // Arrange: a document whose image is stored as a complete JPEG file
        var sink = new RecordingSink();

        // Act: extract its images
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink);

        // Assert: one image, handed over as the exact source bytes, with the honest media type
        Assert.Equal(1, result.Found);
        Assert.Equal(1, result.Written);
        var recorded = Assert.Single(sink.Images);
        Assert.Equal(PdfFixtures.SourceJpegBytes(), recorded.Content);
        Assert.Equal("image/jpeg", recorded.Hint.MediaType);

        // Assert: and labeled a passthrough, which the byte comparison above makes falsifiable
        Assert.Equal(ImageTransform.Passthrough, recorded.Hint.Transform);
    }

    /// <summary>
    ///     Proves a compressed-sample image is re-encoded and labeled as such (DecodesToPng).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_AddImage_FlateImage_SetsDecodedToPngTransformHint()
    {
        // Arrange: a document whose image is stored as a Flate-compressed sample buffer
        var sink = new RecordingSink();

        // Act: extract its images
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedPng(), sink);

        // Assert: the bytes handed over are a PNG and are labeled as the re-encoding they are
        Assert.Equal(1, result.Written);
        var recorded = Assert.Single(sink.Images);
        Assert.Equal("image/png", recorded.Hint.MediaType);
        Assert.Equal(ImageTransform.DecodedToPng, recorded.Hint.Transform);
        Assert.NotEqual(ImageTransform.Passthrough, recorded.Hint.Transform);
    }

    /// <summary>
    ///     Proves the hint carries the provenance fields the manifest records (ReportsImageHints).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_AddImage_AnyImage_PopulatesHintProvenanceFields()
    {
        // Arrange: a single-page document with one embedded image
        var sink = new RecordingSink();

        // Act: extract its images
        await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink);

        // Assert: the dimensions, source page, and a stable source reference are all supplied
        var hint = Assert.Single(sink.Images).Hint;
        Assert.Equal(8, hint.WidthPx);
        Assert.Equal(8, hint.HeightPx);
        Assert.Equal(1, hint.SourcePage);
        Assert.Equal("page 1 image 1", hint.SourceRef);
    }

    /// <summary>
    ///     Proves a JPEG 2000 image is written verbatim as .jp2 with a readability caveat (WritesJpeg2000WithCaveat).
    /// </summary>
    /// <remarks>
    ///     The caveat is the whole point of writing these bytes at all. A <c>.jp2</c> file some tooling
    ///     can open is more useful to a multimodal consumer than no file, but only while the consumer
    ///     is told plainly that many viewers and image libraries cannot read it. Asserting the bytes
    ///     against the fixture's retained codestream keeps the passthrough label falsifiable, exactly
    ///     as the JPEG scenario does.
    /// </remarks>
    [Fact]
    public async Task PdfImageExtractor_AddImage_JpxImage_WritesJp2PassthroughWithCaveat()
    {
        // Arrange: a document with one decodable JPEG and one JPEG 2000 image
        var sink = new RecordingSink();

        // Act: extract its images
        var result = await ExtractAsync(PdfFixtures.WithJpeg2000Image(), sink);

        // Assert: both images reached the output, so neither is a loss
        Assert.Equal(2, result.Found);
        Assert.Equal(2, result.Written);
        Assert.Equal(2, sink.Images.Count);

        // Assert: the JPEG 2000 image is the exact embedded codestream, typed and labeled honestly
        var recorded = sink.Images[1];
        Assert.Equal(PdfFixtures.SourceJpeg2000Bytes(), recorded.Content);
        Assert.Equal("image/jp2", recorded.Hint.MediaType);
        Assert.Equal(ImageTransform.Passthrough, recorded.Hint.Transform);

        // Assert: and the run warns that the format is not widely readable, without calling it a loss
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0004");
        Assert.DoesNotContain(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0001");
        var gap = Assert.Single(sink.Gaps);
        Assert.Equal("images/", gap.Target);
        Assert.Equal(GapScope.PartiallyExtracted, gap.Scope);
        Assert.Equal(1, gap.AffectedCount);
        Assert.Equal(["page 1 image 2"], gap.AffectedItems);
        Assert.Contains("Many image viewers and image libraries cannot read JPEG 2000 files.",
            Normalize(gap.Reason), StringComparison.Ordinal);
        Assert.Contains(".jp2", gap.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an undecodable image is counted and named rather than dropped (ReportsUndecodableEncodings).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_UndecodableEncoding_ReportsCountedGapAndWritesNothingForIt()
    {
        // Arrange: a document with one decodable JPEG and one JBIG2 image
        var sink = new RecordingSink();

        // Act: extract its images
        var result = await ExtractAsync(PdfFixtures.WithUndecodableImage(), sink);

        // Assert: the decodable image was delivered and the other was not silently omitted
        Assert.Equal(2, result.Found);
        Assert.Equal(1, result.Written);
        Assert.Single(sink.Images);
        Assert.Equal(2, Assert.Single(sink.FoundCounts, found => found.Kind == GapKind.Images).FoundCount);

        // Assert: the gap names the encoding, the count, and the consequence
        var gap = Assert.Single(sink.Gaps);
        Assert.Equal("images/", gap.Target);
        Assert.Equal(GapKind.Images, gap.Kind);
        Assert.Equal(GapScope.PartiallyExtracted, gap.Scope);
        Assert.Contains("JBIG2Decode: 1", gap.Reason, StringComparison.Ordinal);
        Assert.Contains("1 of 2", gap.Reason, StringComparison.Ordinal);
        Assert.Equal(1, gap.AffectedCount);
        Assert.Equal(["page 1 image 2"], gap.AffectedItems);
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0001");
    }

    /// <summary>
    ///     Proves a document where nothing could be written reports an unavailable rather than partial scope.
    /// </summary>
    /// <remarks>
    ///     The distinction is not cosmetic. Calling an empty folder "partially extracted" would both
    ///     overstate the result and contradict the contract verifier's rule that a partial folder
    ///     holds at least one file, so the scope has to follow what was actually written.
    /// </remarks>
    [Fact]
    public async Task PdfImageExtractor_Extract_NothingWritten_ReportsUnavailableScope()
    {
        // Arrange: a document whose only deliverable image is excluded by a byte budget, leaving none
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageBytes = 4 };

        // Act: extract with the limit in force
        var result = await ExtractAsync(PdfFixtures.WithUndecodableImage(), sink, options);

        // Assert: nothing was written, so the scope says so rather than claiming partial success
        Assert.Equal(0, result.Written);
        Assert.All(sink.Gaps, gap => Assert.Equal(GapScope.Unavailable, gap.Scope));
    }

    /// <summary>
    ///     Proves a size-limit skip is a distinct gap from a decode failure (HonorsSizeLimits).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImageOverByteLimit_ReportsSizeGapDistinctFromDecodeGap()
    {
        // Arrange: a JPEG-bearing document with an impossibly small byte budget
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageBytes = 8 };

        // Act: extract with the limit in force
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: nothing written, and the gap names a limit rather than an undecodable encoding
        Assert.Equal(0, result.Written);
        var gap = Assert.Single(sink.Gaps);
        Assert.Contains("size or dimension limit", gap.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot decode", gap.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0001");
        Assert.NotNull(gap.Remedy);
    }

    /// <summary>
    ///     Proves a dimension limit skips the image the same way a byte limit does (HonorsSizeLimits).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImageOverDimensionLimit_SkipsWithCountedGap()
    {
        // Arrange: an 8x8 image excluded by a four-pixel dimension limit
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageDimensionPx = 4 };

        // Act: extract with the limit in force
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: the image is skipped and counted rather than written or silently ignored
        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Written);
        Assert.Equal(1, Assert.Single(sink.Gaps).AffectedCount);
    }

    /// <summary>
    ///     Proves a PNG request that cannot be honored is explained (ExplainsUnhonoredForcePng).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ForcePngWithJpeg_ReportsUnhonoredModeGap()
    {
        // Arrange: a JPEG-bearing document extracted with PNG output demanded
        var sink = new RecordingSink();
        var options = new ExtractionOptions { ImageOutput = ImageOutputMode.ForcePng };

        // Act: extract with an output mode that cannot be honored
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: the image is still delivered, truthfully, as the JPEG it is
        Assert.Equal(1, result.Written);
        Assert.Equal("image/jpeg", Assert.Single(sink.Images).Hint.MediaType);

        // Assert: and the run explains which images defeated the requested mode and why
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0002");
        var gap = Assert.Single(sink.Gaps);
        Assert.Contains("PNG output was requested", gap.Reason, StringComparison.Ordinal);
        Assert.Contains("DCTDecode", gap.Reason, StringComparison.Ordinal);
        Assert.Equal(1, gap.AffectedCount);
    }

    /// <summary>
    ///     Proves a PNG request that can be honored produces no explanation gap.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ForcePngWithFlateImage_ReportsNoUnhonoredGap()
    {
        // Arrange: a document whose image genuinely becomes PNG, extracted with PNG demanded
        var sink = new RecordingSink();
        var options = new ExtractionOptions { ImageOutput = ImageOutputMode.ForcePng };

        // Act: extract with an output mode that can be honored
        await ExtractAsync(PdfFixtures.WithEmbeddedPng(), sink, options);

        // Assert: the mode was honored, so nothing needed explaining
        Assert.Empty(sink.Gaps);
        Assert.DoesNotContain(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0002");
    }

    /// <summary>
    ///     Proves a JPEG 2000 passthrough defeats a PNG request exactly as a JPEG passthrough does.
    /// </summary>
    /// <remarks>
    ///     A passthrough is a passthrough whatever the container: an image written as <c>.jp2</c> under
    ///     a PNG request is as unhonored as one written as <c>.jpg</c>, and the explanation must name
    ///     the encoding that actually defeated the request rather than assume it was JPEG.
    /// </remarks>
    [Fact]
    public async Task PdfImageExtractor_Extract_ForcePngWithJpxImage_ReportsUnhonoredModeGapNamingJpx()
    {
        // Arrange: a document holding a JPEG and a JPEG 2000 image, extracted with PNG demanded
        var sink = new RecordingSink();
        var options = new ExtractionOptions { ImageOutput = ImageOutputMode.ForcePng };

        // Act: extract with an output mode neither image can honor
        var result = await ExtractAsync(PdfFixtures.WithJpeg2000Image(), sink, options);

        // Assert: both images are still delivered in their true encodings
        Assert.Equal(2, result.Written);
        Assert.Equal("image/jp2", sink.Images[1].Hint.MediaType);

        // Assert: the unhonored-mode gap counts both and names JPXDecode alongside DCTDecode
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0002");
        var gap = Assert.Single(sink.Gaps, candidate =>
            candidate.Reason.Contains("PNG output was requested", StringComparison.Ordinal));
        Assert.Contains("DCTDecode: 1", gap.Reason, StringComparison.Ordinal);
        Assert.Contains("JPXDecode: 1", gap.Reason, StringComparison.Ordinal);
        Assert.Equal(2, gap.AffectedCount);
    }

    /// <summary>
    ///     Proves disabling embedded images attempts nothing at all (HonorsSuppression).
    /// </summary>
    /// <remarks>
    ///     Nothing is decoded, counted, or explained here, because Core owns the record of a
    ///     deliberate suppression; a second account of the same decision would be redundant at best
    ///     and contradictory at worst.
    /// </remarks>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImagesDisabled_AttemptsNothing()
    {
        // Arrange: a JPEG-bearing document extracted with embedded images turned off
        var sink = new RecordingSink();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false };

        // Act: extract with images suppressed
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: no image, no link, no count, and no gap of this unit's own
        Assert.Equal(0, result.Found);
        Assert.Empty(result.Images);
        Assert.Empty(sink.Images);
        Assert.Empty(sink.Gaps);
        Assert.Empty(sink.FoundCounts);
    }

    /// <summary>
    ///     Proves a document with no images reports a zero found count rather than staying silent.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_NoImages_ReportsZeroFoundCount()
    {
        // Arrange: a text-only document
        var sink = new RecordingSink();

        // Act: extract its (absent) images
        var result = await ExtractAsync(PdfFixtures.SimpleText(), sink);

        // Assert: the denominator is still reported, so the ledger can state zero of zero honestly
        Assert.Equal(0, result.Found);
        Assert.Equal(0, Assert.Single(sink.FoundCounts).FoundCount);
        Assert.Empty(sink.Gaps);
    }

    /// <summary>
    ///     Proves a null sink is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_NullSink_ThrowsArgumentNullException()
    {
        // Arrange: a valid document but no sink
        using var document = PdfFixtures.Open(PdfFixtures.SimpleText());
        var pages = document.GetPages().ToList();

        // Act + Assert: the sink is the unit's only output channel and is mandatory
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await global::DocDown.Pdf.PdfImageExtractor.ExtractAsync(
                pages, null!, new ExtractionOptions(), Ct));
    }

    /// <summary>
    ///     Collapses all whitespace in a text into single spaces.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text with every whitespace run reduced to one space.</returns>
    /// <remarks>
    ///     Gap prose is wrapped when it reaches <c>summary.txt</c>, so a phrase may be split across two
    ///     lines. Normalizing before matching asserts on what is said rather than on where the wrap
    ///     happened to fall, and keeps this assertion identical in shape to the system-level one. Pure.
    /// </remarks>
    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    ///     Runs the image extractor over a generated document.
    /// </summary>
    /// <param name="bytes">The generated document bytes.</param>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="options">The options to extract with, or <see langword="null"/> for the defaults.</param>
    /// <returns>The extraction result.</returns>
    /// <remarks>Opens the fixture and hands its pages straight to the unit, keeping the test unit-scoped.</remarks>
    private static async ValueTask<global::DocDown.Pdf.PdfImageResult> ExtractAsync(
        byte[] bytes, RecordingSink sink, ExtractionOptions? options = null)
    {
        using var document = PdfFixtures.Open(bytes);
        var pages = document.GetPages().ToList();
        return await global::DocDown.Pdf.PdfImageExtractor.ExtractAsync(
            pages, sink, options ?? new ExtractionOptions(), Ct);
    }
}
