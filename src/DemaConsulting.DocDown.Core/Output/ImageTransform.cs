namespace DocDown.Core;

/// <summary>
///     How an extractor produced the image bytes it hands to the sink.
/// </summary>
/// <remarks>
///     <para>
///         Image provenance is a claim the manifest makes about every extracted image, so the set
///         of legal claims must be closed rather than free-form: a manifest that labels a
///         decode-and-re-encode as a byte-for-byte passthrough is a false provenance claim, and
///         downstream trust in every other manifest field rests on no field being decorative.
///         Modeling the vocabulary as an enumeration makes the closure structural — an extractor
///         cannot name a transform that does not exist, and <c>ManifestWriter</c>'s projection
///         throws on any member it has not been taught to serialize.
///     </para>
///     <para>
///         The set is exactly two members because there are exactly two ways bytes reach
///         <c>images/</c>: either the source document's stored bytes are already a complete image
///         file and are written unchanged, or the source samples must be decoded and re-encoded as
///         PNG to be usable. A third member would itself be a documented value that nothing can produce,
///         which is the defect this enumeration exists to prevent.
///     </para>
/// </remarks>
public enum ImageTransform
{
    /// <summary>The bytes were written exactly as the source document stored them.</summary>
    /// <remarks>
    ///     The default when an extractor reports no transform, because Core writes whatever bytes
    ///     it is given verbatim and therefore changes nothing on its own.
    /// </remarks>
    Passthrough,

    /// <summary>The source samples were decoded and re-encoded as PNG.</summary>
    /// <remarks>
    ///     Reported by an extractor whose source encoding is not itself a usable image file — for
    ///     example a compressed sample buffer — so the written bytes are a new encoding rather
    ///     than the stored ones.
    /// </remarks>
    DecodedToPng
}
