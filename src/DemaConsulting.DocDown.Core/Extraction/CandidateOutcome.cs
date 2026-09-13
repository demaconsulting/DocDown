namespace DocDown.Core;

/// <summary>
///     The verdict recorded for a single extractor candidate during selection.
/// </summary>
/// <remarks>
///     Every candidate — selected or not — receives an outcome so the selection trace is a
///     complete, auditable explanation of why the chosen backend won and why each other backend
///     did not. This transparency is a core honesty requirement of the library.
/// </remarks>
public enum CandidateOutcome
{
    /// <summary>
    ///     This candidate was selected to perform the extraction.
    /// </summary>
    Selected,

    /// <summary>
    ///     The candidate does not support the detected format.
    /// </summary>
    FormatNotSupported,

    /// <summary>
    ///     The candidate supports the format but is unavailable in this environment.
    /// </summary>
    Unavailable,

    /// <summary>
    ///     The candidate is available but cannot satisfy the required capabilities.
    /// </summary>
    CapabilitiesInsufficient,

    /// <summary>
    ///     The candidate was eligible but a higher-fidelity candidate was selected instead.
    /// </summary>
    OutrankedByHigherFidelity,

    /// <summary>
    ///     The candidate was excluded because the caller forced a different extractor.
    /// </summary>
    ExcludedByOverride
}
