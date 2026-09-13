namespace DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;

/// <summary>
///     A minimal PNG reader the tests use to assert that a rendered page is a real image rather than
///     merely a file that exists.
/// </summary>
/// <remarks>
///     Reads only the eight-byte signature and the width and height from the IHDR chunk, which is
///     enough to prove the renderer produced a decodable PNG of plausible size without pulling in an
///     image-decoding dependency. All members are static and pure.
/// </remarks>
public static class Png
{
    /// <summary>The eight-byte PNG file signature.</summary>
    /// <remarks>Every PNG begins with exactly these bytes; a file that does not is not a PNG.</remarks>
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Tests whether a byte buffer begins with the PNG signature.
    /// </summary>
    /// <param name="bytes">The bytes to inspect.</param>
    /// <returns><see langword="true"/> when the buffer starts with the PNG signature; otherwise <see langword="false"/>.</returns>
    /// <remarks>A necessary condition for the buffer to be a PNG, checked before reading dimensions. Pure.</remarks>
    public static bool HasSignature(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < Signature.Length)
        {
            return false;
        }

        for (var index = 0; index < Signature.Length; index++)
        {
            if (bytes[index] != Signature[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Reads the pixel dimensions from a PNG's IHDR chunk.
    /// </summary>
    /// <param name="bytes">The PNG bytes to read.</param>
    /// <returns>The width and height in pixels.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> is not a PNG with a readable IHDR.</exception>
    /// <remarks>
    ///     The IHDR chunk immediately follows the signature, and its width and height are the first
    ///     two big-endian 32-bit integers of the chunk data (at byte offsets 16 and 20). Pure.
    /// </remarks>
    public static (int Width, int Height) ReadDimensions(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (!HasSignature(bytes) || bytes.Length < 24)
        {
            throw new ArgumentException("The buffer is not a PNG with a readable IHDR chunk.", nameof(bytes));
        }

        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        return (width, height);
    }

    /// <summary>
    ///     Reads a big-endian 32-bit integer at an offset in a buffer.
    /// </summary>
    /// <param name="bytes">The buffer to read from.</param>
    /// <param name="offset">The offset of the first (most significant) byte.</param>
    /// <returns>The decoded integer.</returns>
    /// <remarks>PNG stores integers most-significant-byte first, unlike the host's little-endian order. Pure.</remarks>
    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
}
