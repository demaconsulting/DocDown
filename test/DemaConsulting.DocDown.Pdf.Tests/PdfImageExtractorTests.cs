using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for the PDF image extractor, proving the encoding decision, the provenance it
///     reports for each decision, the plain-language notes for what it could not deliver, suppression,
///     and the size limits.
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
    ///     Proves a stored JPEG is passed through unchanged and labeled as such.
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
    ///     Proves a compressed-sample image is re-encoded and labeled as such.
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
    ///     Proves the hint carries the provenance fields the manifest records.
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
    ///     Proves a JPEG 2000 image is written verbatim as <c>.jp2</c> and labeled as a passthrough.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_AddImage_JpxImage_WritesJp2PassthroughWithoutExtraNote()
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

        // Assert: nothing was left incomplete, because the image was written successfully
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an undecodable image is counted and named rather than dropped.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_UndecodableEncoding_ReportsPlainNoteAndWritesNothingForIt()
    {
        // Arrange: a document with one decodable JPEG and one JBIG2 image
        var sink = new RecordingSink();

        // Act: extract its images
        var result = await ExtractAsync(PdfFixtures.WithUndecodableImage(), sink);

        // Assert: the decodable image was delivered and the other was not silently omitted
        Assert.Equal(2, result.Found);
        Assert.Equal(1, result.Written);
        Assert.Single(sink.Images);

        // Assert: the note names the encoding, the count, and the consequence
        var note = SingleNoteMessage(sink);
        Assert.Contains("JBIG2Decode: 1", note, StringComparison.Ordinal);
        Assert.Contains("1 of 2", note, StringComparison.Ordinal);
        Assert.Contains("were not written", note, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a size-limit skip is a distinct note from a decode failure.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImageOverByteLimit_ReportsSizeNoteDistinctFromDecodeFailure()
    {
        // Arrange: a JPEG-bearing document with an impossibly small byte budget
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageBytes = 8 };

        // Act: extract with the limit in force
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: nothing written, and the note names a limit rather than an undecodable encoding
        Assert.Equal(0, result.Written);
        var note = SingleNoteMessage(sink);
        Assert.Contains("size or dimension limit", note, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot decode", note, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a dimension limit skips the image the same way a byte limit does.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImageOverDimensionLimit_ReportsCountedSizeNote()
    {
        // Arrange: an 8x8 image excluded by a four-pixel dimension limit
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageDimensionPx = 4 };

        // Act: extract with the limit in force
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: the image is skipped and counted rather than written or silently ignored
        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Written);
        var note = SingleNoteMessage(sink);
        Assert.Contains("1 of 1", note, StringComparison.Ordinal);
        Assert.Contains("size or dimension limit", note, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a JPEG stored in a PDF is written in its own encoding, with nothing to explain.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_JpegImage_WritesSourceEncodingWithoutNote()
    {
        // Arrange: a JPEG-bearing document
        var sink = new RecordingSink();

        // Act: extract with the default options
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, new ExtractionOptions());

        // Assert: the image is delivered, truthfully, as the JPEG it is, and nothing is reported
        Assert.Equal(1, result.Written);
        Assert.Equal("image/jpeg", Assert.Single(sink.Images).Hint.MediaType);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a JPEG 2000 codestream is passed through in its own encoding like a JPEG.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_Jpeg2000Image_WritesSourceEncodingWithoutNote()
    {
        // Arrange: a document holding a JPEG and a JPEG 2000 image
        var sink = new RecordingSink();

        // Act: extract with the default options
        var result = await ExtractAsync(PdfFixtures.WithJpeg2000Image(), sink, new ExtractionOptions());

        // Assert: both images are delivered in their true encodings with nothing to explain
        Assert.Equal(2, result.Written);
        Assert.Equal("image/jp2", sink.Images[1].Hint.MediaType);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves disabling embedded images attempts nothing at all.
    /// </summary>
    /// <remarks>
    ///     Nothing is decoded or explained here, because Core owns the record of a deliberate
    ///     suppression; a second account of the same decision would be redundant at best and
    ///     contradictory at worst.
    /// </remarks>
    [Fact]
    public async Task PdfImageExtractor_Extract_ImagesDisabled_AttemptsNothing()
    {
        // Arrange: a JPEG-bearing document extracted with embedded images turned off
        var sink = new RecordingSink();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false };

        // Act: extract with images suppressed
        var result = await ExtractAsync(PdfFixtures.WithEmbeddedJpeg(), sink, options);

        // Assert: no image, no link, no note of this unit's own
        Assert.Equal(0, result.Found);
        Assert.Empty(result.Images);
        Assert.Empty(sink.Images);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a document with no images reports zero counts without a note.
    /// </summary>
    [Fact]
    public async Task PdfImageExtractor_Extract_NoImages_ReturnsZeroCountsWithoutNote()
    {
        // Arrange: a text-only document
        var sink = new RecordingSink();

        // Act: extract its absent images
        var result = await ExtractAsync(PdfFixtures.SimpleText(), sink);

        // Assert: the denominator is still reported by the result counters, and there is nothing to explain
        Assert.Equal(0, result.Found);
        Assert.Equal(0, result.Written);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a null sink is rejected as a caller error.
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
    ///     Returns the single note message recorded by the sink.
    /// </summary>
    /// <param name="sink">The sink whose notes are inspected.</param>
    /// <returns>The single recorded note message.</returns>
    /// <remarks>Used where one scenario is expected to explain itself with exactly one note.</remarks>
    private static string SingleNoteMessage(RecordingSink sink) => Assert.Single(sink.Notes).Message;

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
