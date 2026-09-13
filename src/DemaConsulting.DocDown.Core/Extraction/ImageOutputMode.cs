namespace DocDown.Core;

/// <summary>
///     Controls the on-disk encoding of extracted images.
/// </summary>
/// <remarks>
///     Preserving the original encoding avoids a lossy or lossless re-encode when the source
///     bytes are already usable, while forcing PNG guarantees a single predictable format for
///     consumers that cannot handle the variety of encodings a document may embed.
/// </remarks>
public enum ImageOutputMode
{
    /// <summary>
    ///     Write embedded images in their original encoding whenever possible.
    /// </summary>
    Preserve,

    /// <summary>
    ///     Convert every extracted image to PNG for a uniform output format.
    /// </summary>
    ForcePng
}
