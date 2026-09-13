namespace DocDown.Core;

/// <summary>
///     The result of extractor selection: the chosen extractor (if any), the reasoning, and the
///     capability negotiation outcome.
/// </summary>
/// <param name="Selected">
///     The selected extractor descriptor, or <see langword="null"/> when no extractor could be
///     selected (in which case <see cref="Failure"/> explains why).
/// </param>
/// <param name="Mode">Whether selection was automatic or forced by a caller override.</param>
/// <param name="RequiredCapabilities">The capabilities selection required for this extraction.</param>
/// <param name="SatisfiedCapabilities">
///     The required capabilities the selected extractor can actually satisfy here; a strict
///     subset indicates a degraded extraction.
/// </param>
/// <param name="Trace">
///     The full candidate trace, ordered with the selected candidate first and the remainder
///     sorted by identifier so the trace is deterministic regardless of registration order.
/// </param>
/// <param name="Failure">
///     The structured failure when no extractor could be selected, or <see langword="null"/> on
///     success.
/// </param>
/// <remarks>
///     Selection returns a complete decision record rather than just a winner so the engine can
///     report the negotiation, synthesize capability gaps, and — on failure — surface an
///     auditable explanation without re-deriving it. Instances are immutable and thread-safe.
/// </remarks>
public sealed record SelectionResult(
    ExtractorDescriptor? Selected, SelectionMode Mode,
    ExtractorCapabilities RequiredCapabilities, ExtractorCapabilities SatisfiedCapabilities,
    IReadOnlyList<CandidateVerdict> Trace, ExtractionFailure? Failure);
