namespace DocDown.Core;

/// <summary>
///     The recorded verdict for one extractor candidate considered during selection.
/// </summary>
/// <param name="ExtractorId">The candidate extractor's stable identifier.</param>
/// <param name="DisplayName">The candidate extractor's human-readable name.</param>
/// <param name="Priority">The candidate's ranking priority at the time of selection.</param>
/// <param name="Outcome">Why the candidate was or was not selected.</param>
/// <param name="Detail">A human-readable explanation of the outcome for display and audit.</param>
/// <remarks>
///     Verdicts form the selection trace that accompanies every extraction, making the decision
///     fully auditable: a reviewer can see each candidate's fate and reason. Instances are
///     immutable and thread-safe.
/// </remarks>
public sealed record CandidateVerdict(
    string ExtractorId, string DisplayName, int Priority, CandidateOutcome Outcome, string Detail);
