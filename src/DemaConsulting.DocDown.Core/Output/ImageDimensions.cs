namespace DocDown.Core;

/// <summary>
///     Reads the pixel width and height of a raster image directly from its header bytes, with no
///     imaging stack and no decoding of the pixel data.
/// </summary>
/// <remarks>
///     <para>
///         An Open XML backend ships no imaging library, yet it must know an image's pixel size to
///         honor a caller's <see cref="ExtractionOptions.MaxImageDimensionPx"/> limit and to record
///         honest <c>WidthPx</c>/<c>HeightPx</c> provenance. The dimensions of the common raster
///         formats live in a fixed header field a few bytes in, so they can be read without a codec:
///         PNG in its <c>IHDR</c> chunk, JPEG in its start-of-frame marker, GIF and BMP in their
///         file headers. This unit reads exactly those fields and nothing else.
///     </para>
///     <para>
///         A vector metafile (EMF, WMF, SVG) has no pixel dimension at all, and a format this unit
///         does not recognize is reported as unknown rather than guessed, so a caller learns the size
///         only when it is a real, recorded fact. Pure, stateless, and thread-safe.
///     </para>
/// </remarks>
public static class ImageDimensions
{
    /// <summary>
    ///     Attempts to read the pixel dimensions of a raster image from its header.
    /// </summary>
    /// <param name="bytes">The complete image file bytes.</param>
    /// <param name="width">The pixel width when the read succeeds; otherwise zero.</param>
    /// <param name="height">The pixel height when the read succeeds; otherwise zero.</param>
    /// <returns><see langword="true"/> when the dimensions were read; <see langword="false"/> for an unrecognized format or a vector image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <remarks>Reads only the header field for the detected format; never decodes pixel data. Pure.</remarks>
    public static bool TryRead(byte[] bytes, out int width, out int height)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        width = 0;
        height = 0;
        var span = bytes.AsSpan();

        if (TryReadPng(span, out width, out height))
        {
            return true;
        }

        if (TryReadJpeg(span, out width, out height))
        {
            return true;
        }

        if (TryReadGif(span, out width, out height))
        {
            return true;
        }

        return TryReadBmp(span, out width, out height);
    }

    /// <summary>
    ///     Reads a PNG's dimensions from its mandatory <c>IHDR</c> chunk.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="width">The pixel width when the read succeeds.</param>
    /// <param name="height">The pixel height when the read succeeds.</param>
    /// <returns><see langword="true"/> when the bytes are a PNG whose header could be read.</returns>
    /// <remarks>The 8-byte signature is followed by the <c>IHDR</c> chunk whose data begins with a 4-byte width and height, big-endian. Pure.</remarks>
    private static bool TryReadPng(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(signature))
        {
            return false;
        }

        width = BigEndianInt32(bytes, 16);
        height = BigEndianInt32(bytes, 20);
        return width > 0 && height > 0;
    }

    /// <summary>
    ///     Reads a JPEG's dimensions from its start-of-frame marker.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="width">The pixel width when the read succeeds.</param>
    /// <param name="height">The pixel height when the read succeeds.</param>
    /// <returns><see langword="true"/> when the bytes are a JPEG whose frame header could be read.</returns>
    /// <remarks>
    ///     Walks the marker segments from the <c>SOI</c> until a start-of-frame marker (any of
    ///     <c>SOF0</c>..<c>SOF15</c> except the huffman, arithmetic, and difference markers), whose
    ///     payload carries the height then the width as big-endian 16-bit fields. Pure.
    /// </remarks>
    private static bool TryReadJpeg(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }

        var pos = 2;
        while (pos + 9 < bytes.Length)
        {
            // Markers begin with 0xFF; a byte that is not 0xFF is padding and is skipped
            if (bytes[pos] != 0xFF)
            {
                pos++;
                continue;
            }

            var marker = bytes[pos + 1];

            // Standalone markers (SOI, EOI, RSTn, TEM) carry no length payload
            if (marker is 0xD8 or 0xD9 or 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                pos += 2;
                continue;
            }

            var segmentLength = (bytes[pos + 2] << 8) | bytes[pos + 3];

            // A start-of-frame marker names the frame dimensions; exclude DHT (C4), JPG (C8), and DAC (CC)
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                height = (bytes[pos + 5] << 8) | bytes[pos + 6];
                width = (bytes[pos + 7] << 8) | bytes[pos + 8];
                return width > 0 && height > 0;
            }

            if (segmentLength < 2)
            {
                return false;
            }

            pos += 2 + segmentLength;
        }

        return false;
    }

    /// <summary>
    ///     Reads a GIF's dimensions from its logical screen descriptor.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="width">The pixel width when the read succeeds.</param>
    /// <param name="height">The pixel height when the read succeeds.</param>
    /// <returns><see langword="true"/> when the bytes are a GIF whose header could be read.</returns>
    /// <remarks>The <c>GIF87a</c>/<c>GIF89a</c> signature is followed by a little-endian 16-bit width and height. Pure.</remarks>
    private static bool TryReadGif(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 10 || bytes[0] != (byte)'G' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F')
        {
            return false;
        }

        width = bytes[6] | (bytes[7] << 8);
        height = bytes[8] | (bytes[9] << 8);
        return width > 0 && height > 0;
    }

    /// <summary>
    ///     Reads a Windows BMP's dimensions from its info header.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="width">The pixel width when the read succeeds.</param>
    /// <param name="height">The pixel height when the read succeeds.</param>
    /// <returns><see langword="true"/> when the bytes are a BMP whose header could be read.</returns>
    /// <remarks>The <c>BM</c> signature precedes a header whose width and height are little-endian 32-bit fields at offsets 18 and 22; the height may be negative for a top-down bitmap. Pure.</remarks>
    private static bool TryReadBmp(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 26 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
        {
            return false;
        }

        width = LittleEndianInt32(bytes, 18);
        height = Math.Abs(LittleEndianInt32(bytes, 22));
        return width > 0 && height > 0;
    }

    /// <summary>
    ///     Reads a big-endian 32-bit signed integer at an offset.
    /// </summary>
    /// <param name="bytes">The bytes to read from.</param>
    /// <param name="offset">The offset of the most-significant byte.</param>
    /// <returns>The integer value.</returns>
    /// <remarks>Pure.</remarks>
    private static int BigEndianInt32(ReadOnlySpan<byte> bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    /// <summary>
    ///     Reads a little-endian 32-bit signed integer at an offset.
    /// </summary>
    /// <param name="bytes">The bytes to read from.</param>
    /// <param name="offset">The offset of the least-significant byte.</param>
    /// <returns>The integer value.</returns>
    /// <remarks>Pure.</remarks>
    private static int LittleEndianInt32(ReadOnlySpan<byte> bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
}
