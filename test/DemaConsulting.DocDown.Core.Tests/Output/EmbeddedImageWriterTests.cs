using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="EmbeddedImageWriter"/>, the shared sink-write pipeline the Open XML
///     backends use, proving content deduplication, the caller size limits, and the vector and
///     force-PNG accounting a backend turns into its own gaps.
/// </summary>
public class EmbeddedImageWriterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves two byte-identical images collapse to one written file and one found, and every
    ///     source reference resolves to the shared path.
    /// </summary>
    [Fact]
    public async Task EmbeddedImageWriter_Write_DuplicateContent_CountsOnce()
    {
        var sink = new RecordingSink();
        var images = new List<EmbeddedImage>
        {
            new([1, 2, 3], "image/png", SourceRef: "/a.png"),
            new([1, 2, 3], "image/png", SourceRef: "/b.png")
        };

        var result = await EmbeddedImageWriter.WriteAsync(sink, new ExtractionOptions(), images, Ct);

        Assert.Equal(1, result.Found);
        Assert.Equal(1, result.WrittenCount);
        Assert.Single(sink.Images);
        Assert.Equal(result.PathsBySourceRef["/a.png"], result.PathsBySourceRef["/b.png"]);
    }

    /// <summary>
    ///     Proves two byte-identical parts referenced from different pages merge their referrer sets
    ///     into the single written image rather than discarding the second — every reference recorded.
    /// </summary>
    [Fact]
    public async Task EmbeddedImageWriter_Write_DuplicateContent_MergesReferrerSets()
    {
        var sink = new RecordingSink();
        var images = new List<EmbeddedImage>
        {
            new([1, 2, 3], "image/png", SourceRef: "/a.png", SourcePages: [3]),
            new([1, 2, 3], "image/png", SourceRef: "/b.png", SourcePages: [1], ReferencedByTemplate: true)
        };

        var result = await EmbeddedImageWriter.WriteAsync(sink, new ExtractionOptions(), images, Ct);

        Assert.Equal(1, result.Found);
        var written = Assert.Single(sink.Images);
        Assert.Equal([1, 3], written.Hint.SourcePages);
        Assert.Equal(1, written.Hint.SourcePage);
        Assert.True(written.Hint.ReferencedByTemplate);
    }

    /// <summary>
    ///     Proves an image beyond the byte budget is skipped, counted, and not written.
    /// </summary>
    [Fact]
    public async Task EmbeddedImageWriter_Write_ExceedsByteLimit_SkipsForSize()
    {
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageBytes = 3 };
        var images = new List<EmbeddedImage> { new([1, 2, 3, 4, 5], "image/png", SourceRef: "/big.png") };

        var result = await EmbeddedImageWriter.WriteAsync(sink, options, images, Ct);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.WrittenCount);
        Assert.Equal(1, result.SizeSkippedCount);
        Assert.Empty(sink.Images);
    }

    /// <summary>
    ///     Proves a raster image whose pixel dimension exceeds the caller limit is skipped for size.
    /// </summary>
    [Fact]
    public async Task EmbeddedImageWriter_Write_ExceedsDimensionLimit_SkipsForSize()
    {
        var sink = new RecordingSink();
        var options = new ExtractionOptions { MaxImageDimensionPx = 100 };
        var images = new List<EmbeddedImage> { new(Png(1000, 200), "image/png", SourceRef: "/wide.png") };

        var result = await EmbeddedImageWriter.WriteAsync(sink, options, images, Ct);

        Assert.Equal(1, result.SizeSkippedCount);
        Assert.Equal(0, result.WrittenCount);
    }

    /// <summary>
    ///     Proves an EMF vector metafile is written and counted in the vector tally so the backend can
    ///     emit its readability caveat.
    /// </summary>
    [Fact]
    public async Task EmbeddedImageWriter_Write_VectorMetafile_CountedAsVector()
    {
        var sink = new RecordingSink();
        var images = new List<EmbeddedImage> { new([1, 2, 3], "image/x-emf", SourceRef: "/d.emf") };

        var result = await EmbeddedImageWriter.WriteAsync(sink, new ExtractionOptions(), images, Ct);

        Assert.Equal(1, result.WrittenCount);
        Assert.Equal(1, result.VectorWrittenCount);
    }

    /// <summary>
    ///     Builds a minimal PNG header carrying the given pixel dimensions.
    /// </summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The 24-byte PNG header.</returns>
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        bytes[16] = (byte)(width >> 24);
        bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8);
        bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24);
        bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8);
        bytes[23] = (byte)height;
        return bytes;
    }
}
