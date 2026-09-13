namespace DocDown.Core;

/// <summary>
///     The availability of a document extractor in the current environment, and whether it can
///     render document pages to images here.
/// </summary>
/// <param name="IsAvailable">
///     <see langword="true"/> when the extractor can run in this environment; otherwise
///     <see langword="false"/>.
/// </param>
/// <param name="UnavailableReason">
///     A human-readable reason the extractor is unavailable, or <see langword="null"/> when it
///     is available.
/// </param>
/// <param name="ProvidesRenderedPages">
///     <see langword="true"/> when the extractor can render document pages to raster images in this
///     environment. This is the one environment-dependent fact selection needs: a managed backend
///     that only extracts text reports <see langword="false"/>, while a renderer whose native stack
///     loaded reports <see langword="true"/>.
/// </param>
/// <remarks>
///     Page rendering is separated out because it is the only capability that varies with the
///     environment and the only one selection reasons about: a backend may be able to render pages
///     in principle yet be unable to on a given platform, and selection must know what is truly
///     possible here. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractorAvailability(
    bool IsAvailable, string? UnavailableReason, bool ProvidesRenderedPages)
{
    /// <summary>
    ///     Creates an availability result for an extractor that can run here.
    /// </summary>
    /// <param name="providesRenderedPages">
    ///     <see langword="true"/> when the extractor can render document pages to images in this
    ///     environment; <see langword="false"/> (the default) when it cannot.
    /// </param>
    /// <returns>An available <see cref="ExtractorAvailability"/> with no unavailable reason.</returns>
    /// <remarks>
    ///     A named factory makes available results read clearly at call sites and guarantees the
    ///     reason is <see langword="null"/> for the available case. Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Available(bool providesRenderedPages = false) =>
        new(true, null, providesRenderedPages);

    /// <summary>
    ///     Creates an availability result for an extractor that cannot run here.
    /// </summary>
    /// <param name="reason">The human-readable reason the extractor is unavailable. Must not be null or empty.</param>
    /// <returns>An unavailable <see cref="ExtractorAvailability"/> that renders no pages.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
    /// <remarks>
    ///     Requires a non-empty reason so an unavailable backend is never reported without an
    ///     explanation the caller can surface. Page rendering is forced to <see langword="false"/>
    ///     because nothing is usable when unavailable. Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Unavailable(string reason)
    {
        // Refuse an empty reason so an unavailable status always carries a displayable cause
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new ExtractorAvailability(false, reason, false);
    }
}
