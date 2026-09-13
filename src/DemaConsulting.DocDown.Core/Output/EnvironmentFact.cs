namespace DocDown.Core;

/// <summary>
///     Where an environment fact came from, so the summary can tell what the run actually used
///     from context about what it did not.
/// </summary>
/// <remarks>
///     A summary that lists the availability of every registered backend when exactly one ran
///     spends a reader's token budget on context. The distinction is carried in the data rather
///     than inferred from a key prefix, so trimming the summary can never accidentally hide a fact
///     a backend deliberately reported.
/// </remarks>
public enum EnvironmentFactOrigin
{
    /// <summary>
    ///     The fact was contributed by the backend that ran, describing the environment it actually
    ///     used. Always shown in the summary.
    /// </summary>
    Backend,

    /// <summary>
    ///     The fact was derived by Core from a registered candidate that did not run, recording
    ///     whether it would have been available. Shown in the summary only when it reports an
    ///     unavailability; otherwise summarized as a count with a pointer to <c>manifest.json</c>,
    ///     which always carries every fact.
    /// </summary>
    CandidateAvailability
}

/// <summary>
///     A single environment observation contributed to the extraction record.
/// </summary>
/// <param name="Source">
///     The contributing component that reported the fact (for example <c>DocDown.Pdf</c> or
///     <c>DocDown.Pdf.Rendering</c>). Required so the summary can group facts by their origin,
///     which keeps a component's honest statement (such as a capability it does not offer) from
///     reading as a whole-run failure in a multi-backend environment.
/// </param>
/// <param name="Key">A short, stable key identifying the fact (for example <c>pdf.pageRenderer</c>).</param>
/// <param name="Value">A human-readable value or description for the fact.</param>
/// <param name="Available">
///     An optional tri-state availability flag: <see langword="true"/> present,
///     <see langword="false"/> absent, or <see langword="null"/> when availability is not a
///     meaningful dimension for this fact.
/// </param>
/// <param name="Origin">
///     Whether the fact came from the backend that ran or from a registered candidate that did not.
///     Defaults to <see cref="EnvironmentFactOrigin.Backend"/> so a backend reporting a fact never
///     has to think about it and is never trimmed from the summary.
/// </param>
/// <remarks>
///     Facts let backends and unavailable candidates record why a capability could or could not
///     be provided in this environment (for example a missing native binary), which turns an
///     opaque degradation into an explained one. Instances are immutable and thread-safe.
/// </remarks>
public sealed record EnvironmentFact(
    string Source, string Key, string Value, bool? Available = null,
    EnvironmentFactOrigin Origin = EnvironmentFactOrigin.Backend);
