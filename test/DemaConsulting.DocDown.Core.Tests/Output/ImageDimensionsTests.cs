using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ImageDimensions"/>, proving the header-only pixel-size reader parses
///     the common raster formats and honestly reports unknown for a vector or unrecognized image.
/// </summary>
public class ImageDimensionsTests
{
    /// <summary>
    ///     Proves a PNG's dimensions are read from its <c>IHDR</c> chunk.
    /// </summary>
    [Fact]
    public void ImageDimensions_TryRead_Png_ReadsWidthAndHeight()
    {
        var png = Png(640, 480);

        var ok = ImageDimensions.TryRead(png, out var width, out var height);

        Assert.True(ok);
        Assert.Equal(640, width);
        Assert.Equal(480, height);
    }

    /// <summary>
    ///     Proves a JPEG's dimensions are read from its start-of-frame marker.
    /// </summary>
    [Fact]
    public void ImageDimensions_TryRead_Jpeg_ReadsWidthAndHeight()
    {
        var jpeg = Jpeg(300, 200);

        var ok = ImageDimensions.TryRead(jpeg, out var width, out var height);

        Assert.True(ok);
        Assert.Equal(300, width);
        Assert.Equal(200, height);
    }

    /// <summary>
    ///     Proves a GIF's dimensions are read from its logical screen descriptor.
    /// </summary>
    [Fact]
    public void ImageDimensions_TryRead_Gif_ReadsWidthAndHeight()
    {
        byte[] gif = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 100, 0, 50, 0];

        var ok = ImageDimensions.TryRead(gif, out var width, out var height);

        Assert.True(ok);
        Assert.Equal(100, width);
        Assert.Equal(50, height);
    }

    /// <summary>
    ///     Proves a vector metafile or unrecognized payload is reported as unknown rather than guessed.
    /// </summary>
    [Fact]
    public void ImageDimensions_TryRead_NonRaster_ReturnsFalse()
    {
        // The EMF magic and arbitrary bytes are not a raster the reader parses
        byte[] emf = [0x01, 0x00, 0x00, 0x00, 0x6C, 0x00, 0x00, 0x00, 0xFF, 0xEE];

        var ok = ImageDimensions.TryRead(emf, out var width, out var height);

        Assert.False(ok);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    /// <summary>
    ///     Proves a truncated payload never throws and simply reports unknown.
    /// </summary>
    [Fact]
    public void ImageDimensions_TryRead_Truncated_ReturnsFalse()
    {
        byte[] truncated = [0x89, 0x50, 0x4E, 0x47];

        Assert.False(ImageDimensions.TryRead(truncated, out _, out _));
    }

    /// <summary>
    ///     Builds a minimal PNG header carrying the given pixel dimensions in its <c>IHDR</c> chunk.
    /// </summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The 24-byte PNG header.</returns>
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        // Length (13) and "IHDR" fill offsets 8..15; the reader only needs offsets 16..23
        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);
        return bytes;
    }

    /// <summary>
    ///     Builds a minimal JPEG carrying an <c>SOF0</c> frame header with the given dimensions.
    /// </summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] Jpeg(int width, int height) =>
    [
        0xFF, 0xD8, // SOI
        0xFF, 0xC0, // SOF0
        0x00, 0x11, // segment length
        0x08, // precision
        (byte)(height >> 8), (byte)(height & 0xFF),
        (byte)(width >> 8), (byte)(width & 0xFF),
        0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01
    ];

    /// <summary>
    ///     Writes a big-endian 32-bit integer into a buffer.
    /// </summary>
    /// <param name="bytes">The buffer.</param>
    /// <param name="offset">The offset of the most-significant byte.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
