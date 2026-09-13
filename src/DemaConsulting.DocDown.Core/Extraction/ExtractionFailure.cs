namespace DocDown.Core;

/// <summary>
///     A plain-language description of why an extraction could not produce output.
/// </summary>
/// <param name="Summary">A one-line headline describing the failure.</param>
/// <param name="Explanation">
///     A multi-line explanation ready for direct display, including the detected format where
///     relevant.
/// </param>
/// <remarks>
///     Failures are returned as data rather than thrown so a caller always receives the full
///     layout (where one could be written) alongside the prose; this is why the engine catches
///     backend exceptions and converts them into this record. It carries prose only — no code,
///     no category, and no remedy — because DocDown states what happened without grading the
///     document or advising a fix. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractionFailure(string Summary, string Explanation);
