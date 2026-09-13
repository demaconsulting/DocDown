namespace DocDown.Core;

/// <summary>
///     A structured, human-readable description of why an extraction could not proceed.
/// </summary>
/// <param name="Kind">The category of failure.</param>
/// <param name="Code">The fixed diagnostic code associated with the failure kind (for example <c>DD0401</c>).</param>
/// <param name="Summary">A one-line headline describing the failure.</param>
/// <param name="Explanation">
///     A multi-line explanation ready for direct display, including the detected format and a
///     per-candidate breakdown when relevant.
/// </param>
/// <param name="Candidates">
///     The selection verdicts that led to the failure, empty when the failure occurred before
///     candidate selection.
/// </param>
/// <param name="Remedy">A suggested remedy the caller can act on, or <see langword="null"/> when none applies.</param>
/// <remarks>
///     Failures are returned as data rather than thrown so a caller always receives the full
///     layout and a machine-branchable code alongside the prose; this is why the engine catches
///     backend exceptions and converts them into this record. Instances are immutable and
///     thread-safe.
/// </remarks>
public sealed record ExtractionFailure(
    ExtractionFailureKind Kind, string Code, string Summary, string Explanation,
    IReadOnlyList<CandidateVerdict> Candidates, string? Remedy);
