namespace DocDown.Core;

/// <summary>
///     The availability of a document extractor in the current environment, together with the
///     capabilities that are actually usable here.
/// </summary>
/// <param name="IsAvailable">
///     <see langword="true"/> when the extractor can run in this environment; otherwise
///     <see langword="false"/>.
/// </param>
/// <param name="UnavailableReason">
///     A human-readable reason the extractor is unavailable, or <see langword="null"/> when it
///     is available.
/// </param>
/// <param name="EffectiveCapabilities">
///     The capabilities that are actually usable in this environment, which may be a subset of
///     the extractor's declared capabilities (for example when an optional native renderer is
///     absent).
/// </param>
/// <remarks>
///     Separating <em>declared</em> from <em>effective</em> capabilities is central to the
///     library's honesty guarantee: a backend may advertise page rendering yet be unable to
///     perform it on a given platform, and selection must reason about what is truly possible
///     here. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractorAvailability(
    bool IsAvailable, string? UnavailableReason, ExtractorCapabilities EffectiveCapabilities)
{
    /// <summary>
    ///     Creates an availability result for an extractor that can run here.
    /// </summary>
    /// <param name="effective">The capabilities usable in this environment.</param>
    /// <returns>An available <see cref="ExtractorAvailability"/> with no unavailable reason.</returns>
    /// <remarks>
    ///     A named factory makes available results read clearly at call sites and guarantees the
    ///     reason is <see langword="null"/> for the available case. Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Available(ExtractorCapabilities effective) =>
        new(true, null, effective);

    /// <summary>
    ///     Creates an availability result for an extractor that cannot run here.
    /// </summary>
    /// <param name="reason">The human-readable reason the extractor is unavailable. Must not be null or empty.</param>
    /// <returns>An unavailable <see cref="ExtractorAvailability"/> with no effective capabilities.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
    /// <remarks>
    ///     Requires a non-empty reason so an unavailable backend is never reported without an
    ///     explanation the caller can surface. Effective capabilities are forced to
    ///     <see cref="ExtractorCapabilities.None"/> because nothing is usable when unavailable.
    ///     Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Unavailable(string reason)
    {
        // Refuse an empty reason so an unavailable status always carries a displayable cause
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new ExtractorAvailability(false, reason, ExtractorCapabilities.None);
    }
}
