namespace DocDown.Core;

/// <summary>
///     Describes the overall result of an extraction attempt at a glance.
/// </summary>
/// <remarks>
///     Reported so callers can branch on a single value without parsing diagnostics: a
///     successful run may still be <see cref="Degraded"/> when some requested content could
///     not be produced, which is a common and expected outcome rather than an error.
/// </remarks>
public enum ExtractionOutcome
{
    /// <summary>
    ///     Everything requested was produced; the extraction is complete.
    /// </summary>
    Succeeded,

    /// <summary>
    ///     Content was produced but some requested aspect is missing; see the reported gaps.
    /// </summary>
    Degraded,

    /// <summary>
    ///     No content could be produced; the extraction failed. See the structured failure.
    /// </summary>
    Failed
}
