namespace DocDown.Core;

/// <summary>
///     A recorded gap: something requested or expected that is absent or only partially present,
///     with the reason and, where possible, a remedy.
/// </summary>
/// <param name="Id">
///     The stable identifier Core assigns (for example <c>GAP-1</c>). A caller-supplied value is
///     overwritten by the sink so identifiers are always dense and ordered.
/// </param>
/// <param name="Kind">The category of content the gap concerns.</param>
/// <param name="Target">The artifact or path the gap applies to (for example <c>images/</c>).</param>
/// <param name="Scope">The extent to which the content was attempted and obtained.</param>
/// <param name="Reason">A mandatory, non-empty explanation of why the content is absent or partial.</param>
/// <param name="Impact">An optional description of the consequence for the consumer.</param>
/// <param name="Remedy">An optional suggested action to obtain the missing content.</param>
/// <param name="AffectedCount">The number of affected items, or <see langword="null"/> when not counted.</param>
/// <param name="AffectedItems">
///     The specific affected items, or <see langword="null"/> when not enumerated.
/// </param>
/// <remarks>
///     Gaps are the mechanism that makes every absence explicit and explained — the library
///     never omits content silently. <see cref="Id"/> is allocated centrally by Core in emission
///     order (any value supplied to the sink is replaced) so identifiers are dense and stable,
///     and <see cref="Reason"/> is required so a gap is never unexplained. Instances are
///     immutable and thread-safe.
/// </remarks>
public sealed record ExtractionGap(string Id, GapKind Kind, string Target, GapScope Scope,
    string Reason, string? Impact = null, string? Remedy = null,
    int? AffectedCount = null, IReadOnlyList<string>? AffectedItems = null);
