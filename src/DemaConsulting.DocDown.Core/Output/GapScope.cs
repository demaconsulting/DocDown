namespace DocDown.Core;

/// <summary>
///     The extent to which the affected content was attempted and obtained, for a gap.
/// </summary>
/// <remarks>
///     The scope distinguishes an absence that was never attempted (for example a suppressed
///     option) from one that was attempted and failed or only partly succeeded. That distinction
///     is essential to an honest completeness story: "not requested" and "requested but failed"
///     are very different signals to a caller.
/// </remarks>
public enum GapScope
{
    /// <summary>The content was not attempted (for example because an option disabled it).</summary>
    NotAttempted,

    /// <summary>The content could not be attempted because the needed capability was unavailable.</summary>
    Unavailable,

    /// <summary>The content was attempted and only partly obtained.</summary>
    PartiallyExtracted,

    /// <summary>The content was attempted and failed entirely.</summary>
    Failed
}
