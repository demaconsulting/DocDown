namespace DocDown.Core;

/// <summary>
///     A single diagnostic emitted during extraction, carrying a fixed code, severity, message,
///     and optional location.
/// </summary>
/// <param name="Code">The stable diagnostic code (for example <c>DD0101</c>) for machine branching.</param>
/// <param name="Severity">The severity of the diagnostic.</param>
/// <param name="Message">A human-readable description of the condition.</param>
/// <param name="Location">
///     An optional location the diagnostic refers to (for example a page range), or
///     <see langword="null"/> when it applies to the whole document.
/// </param>
/// <remarks>
///     Diagnostics record the decisions and degradations of a run as an auditable stream; a
///     fixed <paramref name="Code"/> lets automated consumers react without parsing the
///     <paramref name="Message"/> prose. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractionDiagnostic(string Code, DiagnosticSeverity Severity, string Message, string? Location = null);
