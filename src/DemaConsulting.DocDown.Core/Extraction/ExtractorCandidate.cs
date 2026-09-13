namespace DocDown.Core;

/// <summary>
///     Pairs an extractor's static <see cref="ExtractorDescriptor"/> with its current
///     <see cref="ExtractorAvailability"/> for use as a selection input.
/// </summary>
/// <param name="Descriptor">The extractor's identity and supported formats.</param>
/// <param name="Availability">Whether the extractor can run here and whether it renders pages.</param>
/// <remarks>
///     Selection operates purely over candidates so it can be a deterministic function of its
///     inputs with no I/O: the registry probes availability once and hands the frozen result to
///     the selector. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractorCandidate(ExtractorDescriptor Descriptor, ExtractorAvailability Availability);
