namespace DocDown.Core;

/// <summary>
///     Describes a logical content part an extractor is adding, such as a sheet or slide.
/// </summary>
/// <param name="Kind">The kind of part (page, sheet, slide, section, or attachment).</param>
/// <param name="Ordinal">
///     The extractor's advisory ordinal for the part. This is a hint only — Core assigns the
///     real, gap-free ordinal in call order.
/// </param>
/// <param name="Title">A human-readable title for the part, or <see langword="null"/> when none.</param>
/// <remarks>
///     Core overrides <see cref="Ordinal"/> with a stable call-order value so part numbering is
///     dense and independent of any numbering an extractor invents; the extractor-supplied value
///     is retained here only to convey the extractor's intent. Instances are immutable and
///     thread-safe.
/// </remarks>
public sealed record ContentPart(ContentPartKind Kind, int Ordinal, string? Title);
