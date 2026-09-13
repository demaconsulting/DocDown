namespace DocDown.Core;

/// <summary>
///     The severity of an <see cref="ExtractionDiagnostic"/>.
/// </summary>
/// <remarks>
///     Severity lets consumers filter the diagnostic stream — informational notes describe
///     expected, benign decisions, while warnings and errors flag reduced fidelity or outright
///     failure that a caller may need to act on.
/// </remarks>
public enum DiagnosticSeverity
{
    /// <summary>
    ///     An informational note about an expected decision; no action is implied.
    /// </summary>
    Info,

    /// <summary>
    ///     A condition that reduced fidelity or completeness but did not stop extraction.
    /// </summary>
    Warning,

    /// <summary>
    ///     A condition that prevented extraction from completing as requested.
    /// </summary>
    Error
}
