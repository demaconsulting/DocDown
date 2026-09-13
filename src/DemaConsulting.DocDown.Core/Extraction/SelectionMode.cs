namespace DocDown.Core;

/// <summary>
///     Indicates how an extractor was chosen for an extraction.
/// </summary>
/// <remarks>
///     Recorded so consumers can distinguish an automatically ranked selection from one the
///     caller forced with <see cref="ExtractionOptions.PreferredExtractorId"/>; a forced
///     selection never silently falls back to another backend.
/// </remarks>
public enum SelectionMode
{
    /// <summary>
    ///     Core ranked the available candidates and selected the best fit automatically.
    /// </summary>
    Automatic,

    /// <summary>
    ///     The caller named a specific extractor, overriding automatic ranking.
    /// </summary>
    CallerOverride
}
