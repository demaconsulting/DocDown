using System.Globalization;
using System.Text;

namespace DocDown.Core;

/// <summary>
///     Chooses the best extractor for a detected format from a set of candidates, producing a
///     complete, auditable decision record.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Select"/> is a deliberately <strong>pure function</strong> of its three
///         arguments: it performs no I/O, is not asynchronous, reads no clock, and holds no mutable
///         static state, so repeated calls with equal inputs always yield equal results. That purity
///         is exactly why selection is a first-class unit rather than logic buried in
///         <see cref="DocDownEngine"/> — it can be exhaustively unit-tested with hand-built
///         candidates and options.
///     </para>
///     <para>
///         The algorithm follows the fixed seven-step ranking (format filter, caller override,
///         availability filter, required-capability computation, full-versus-partial partition,
///         total-order ranking, and winner-plus-losers verdicts). Ranking never depends on
///         registration order; the final tie-break is the extractor identifier so the outcome is
///         deterministic regardless of how the caller registered its backends. Every candidate —
///         selected or not — receives a verdict with a human-useful detail so the trace fully
///         explains the decision, and a selection failure carries a ready-to-display explanation.
///     </para>
///     <para>
///         Instances are stateless and therefore thread-safe; a single selector may be shared by
///         concurrent extractions.
///     </para>
/// </remarks>
public sealed class ExtractorSelector
{
    /// <summary>
    ///     Creates a selector.
    /// </summary>
    /// <remarks>
    ///     The selector holds no configuration and no mutable state — <see cref="Select"/> is a pure
    ///     function of its arguments — so an instance is stateless, thread-safe, and may be shared
    ///     by concurrent extractions. <see cref="DocDownEngine"/> creates its own; construct one
    ///     directly only to exercise selection outside an engine.
    /// </remarks>
    public ExtractorSelector()
    {
    }

    /// <summary>
    ///     Maps a well-known format identifier to the DocDown package that provides its extractor.
    /// </summary>
    /// <remarks>
    ///     This is a naming table only: it creates no project reference, no package reference, and
    ///     no dependency, so Core keeps its zero-runtime-dependency property. Its sole purpose is
    ///     to turn the unhelpful "no extractor is registered" message into one that names the
    ///     package which provides the missing extractor, so the reader knows which component owns
    ///     the capability. The remedy states that fact rather than instructing an installation,
    ///     because Core cannot know how the consuming application acquires code - a NuGet reference,
    ///     a vendored build, an internal feed, or a single-file image whose backend set was fixed at
    ///     publish time - so an "install this package" instruction is not universally actionable.
    ///     Naming where the capability lives is true in every one of those cases. Formats absent
    ///     from the table fall back to generic wording
    ///     rather than inventing a package name. The legacy binary formats are deliberately absent:
    ///     no DocDown package extracts them, so naming one would be the same false promise.
    /// </remarks>
    private static readonly Dictionary<string, string> WellKnownPackages = new(StringComparer.Ordinal)
    {
        ["pdf"] = "DemaConsulting.DocDown.Pdf",
        ["docx"] = "DemaConsulting.DocDown.Word",
        ["xlsx"] = "DemaConsulting.DocDown.Excel",
        ["pptx"] = "DemaConsulting.DocDown.PowerPoint",
        ["vsdx"] = "DemaConsulting.DocDown.Visio",
        ["vsdm"] = "DemaConsulting.DocDown.Visio",
        ["html"] = "DemaConsulting.DocDown.Html"
    };

    /// <summary>
    ///     The identifiers of the legacy binary Office formats Core detects but DocDown does not extract.
    /// </summary>
    /// <remarks>
    ///     Detection of these formats is deliberate and stays: telling the reader "this is a .doc,
    ///     and DocDown does not support legacy binary formats" is far more useful than "unrecognized
    ///     format". Held as a set so the remedy can state that unsupported status plainly for these
    ///     formats and only these. This is remedy text, not selection logic — it never alters which
    ///     extractor is chosen.
    /// </remarks>
    private static readonly HashSet<string> LegacyBinaryFormatIds =
        new(StringComparer.Ordinal) { "doc", "xls", "ppt", "vsd" };

    /// <summary>
    ///     Builds the remedy text shown when no registered extractor supports a format.
    /// </summary>
    /// <param name="format">The detected format that no candidate handles.</param>
    /// <returns>Remedy text naming the providing package when the format is well known.</returns>
    /// <remarks>
    ///     Core deliberately recognizes more formats than it can extract, so "unsupported here"
    ///     must not read as "unsupported by DocDown" — except for the legacy binary formats, where
    ///     that is exactly what it means. For a legacy binary the remedy says so declaratively,
    ///     because no DocDown package will extract one and any pointer to a package or an
    ///     environment precondition would promise a capability that does not exist. For every other
    ///     well-known format the text names the package that provides the extractor: a statement of
    ///     fact about where the capability lives, not a command. It issues no instruction at all,
    ///     sets no expectation of a delivery date, and reads correctly whether or not the extractor
    ///     packages are published, so publication needs no code change here. Pure and
    ///     side-effect free.
    /// </remarks>
    private static string RemedyFor(DocumentFormat format)
    {
        // Legacy binaries are detected but never extractable, so the remedy states that and nothing more
        if (LegacyBinaryFormatIds.Contains(format.Id))
        {
            return
                $"DocDown does not support the legacy binary Office formats, so '{format.Id}' " +
                $"cannot be extracted by any DocDown package. Only the modern XML-based Office " +
                $"formats are supported; re-saving the document in its modern format makes it " +
                $"extractable.";
        }

        // Formats absent from the naming table fall back to generic wording rather than a fake package
        if (!WellKnownPackages.TryGetValue(format.Id, out var package))
        {
            return $"Register an extractor that supports '{format.Id}'.";
        }

        return
            $"No extractor is registered for '{format.Id}'. Core does not extract this format " +
            $"itself; that capability comes from the separate {package} extractor package, which " +
            $"a host registers with the engine.";
    }

    /// <summary>
    ///     Selects the extractor to run for a detected format, or reports why none can be chosen.
    /// </summary>
    /// <param name="format">The detected format the extraction must handle. Must not be null.</param>
    /// <param name="options">The effective options driving capability requirements and any override. Must not be null.</param>
    /// <param name="candidates">The extractor candidates with their current availability. Must not be null.</param>
    /// <returns>
    ///     A <see cref="SelectionResult"/> whose <see cref="SelectionResult.Selected"/> is non-null on
    ///     success and whose <see cref="SelectionResult.Failure"/> is non-null (with a displayable
    ///     explanation) when no extractor could be chosen. The <see cref="SelectionResult.Trace"/>
    ///     always contains one verdict per candidate.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="format"/>, <paramref name="options"/>, or
    ///     <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Pure and side-effect free. A caller override is treated as a command that never silently
    ///     falls back: if the named extractor does not apply or is unavailable the selection fails
    ///     rather than choosing a different backend. The trace is ordered with the selected candidate
    ///     first and the remainder by identifier so it is stable across calls and independent of
    ///     registration order.
    /// </remarks>
    public SelectionResult Select(
        FormatDetection format, ExtractionOptions options, IReadOnlyList<ExtractorCandidate> candidates)
    {
        // Reject null inputs so the function is total over the values it accepts
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(candidates);

        var mode = string.IsNullOrEmpty(options.PreferredExtractorId)
            ? SelectionMode.Automatic
            : SelectionMode.CallerOverride;
        var required = ComputeRequired(options);

        // Verdicts are keyed by the extractor identifier, which the registry guarantees is unique
        var verdicts = new Dictionary<string, CandidateVerdict>(StringComparer.Ordinal);

        // Step 1: keep only candidates that support the detected format; the rest are recorded verbatim
        var formatMatched = new List<ExtractorCandidate>();
        foreach (var candidate in candidates)
        {
            if (Supports(candidate.Descriptor, format.Format))
            {
                formatMatched.Add(candidate);
            }
            else
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.FormatNotSupported,
                    $"Does not support '{format.Format.Id}'; handles {FormatsOf(candidate)}.");
            }
        }

        if (formatMatched.Count == 0)
        {
            // No backend can even attempt the format; every candidate is a format mismatch
            var trace = BuildTrace(verdicts);
            var failure = MakeFailure(ExtractionFailureKind.NoExtractorForFormat, DiagnosticCodes.NoExtractorForFormat,
                "No registered extractor supports the detected format.", format, trace,
                RemedyFor(format.Format));
            return new SelectionResult(null, mode, required, ExtractorCapabilities.None, trace, failure);
        }

        // Steps 2-3: reduce to the candidates that will actually be ranked (override or availability filter)
        var available = ResolveAvailable(format, options, mode, required, formatMatched, verdicts, out var earlyFailure);
        if (earlyFailure is not null)
        {
            return earlyFailure;
        }

        // Step 5: a hard capability requirement that no full satisfier can meet is a failure, not a degrade
        bool IsFull(ExtractorCandidate candidate) =>
            (candidate.Availability.EffectiveCapabilities & required) == required;

        if (options.RequireCapabilities.HasValue && !available.Any(IsFull))
        {
            foreach (var candidate in available)
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.CapabilitiesInsufficient,
                    $"Missing required capabilities: {MissingNames(candidate, required)}.");
            }

            var trace = BuildTrace(verdicts);
            var failure = MakeFailure(ExtractionFailureKind.RequiredCapabilitiesUnavailable,
                DiagnosticCodes.RequiredCapabilitiesUnavailable,
                $"No available extractor provides the required capabilities: {CapabilityNames(required)}.",
                format, trace, "Retry without requiring those capabilities, or install a backend that provides them.");
            return new SelectionResult(null, mode, required, ExtractorCapabilities.None, trace, failure);
        }

        // Step 6: rank with a total order that never consults registration order
        var ranked = available
            .OrderByDescending(candidate => IsFull(candidate) ? 1 : 0)
            .ThenByDescending(candidate => (candidate.Availability.EffectiveCapabilities & required).CountFlags())
            .ThenByDescending(candidate => candidate.Descriptor.Priority)
            .ThenBy(candidate => candidate.Descriptor.Id, StringComparer.Ordinal)
            .ToList();

        // Step 7: the head wins; losers are classified by whether they could have satisfied the request
        var winner = ranked[0];
        var satisfied = winner.Availability.EffectiveCapabilities & required;
        foreach (var candidate in available)
        {
            if (ReferenceEquals(candidate, winner))
            {
                continue;
            }

            if (IsFull(candidate))
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.OutrankedByHigherFidelity,
                    $"A higher-fidelity backend ('{winner.Descriptor.Id}') was selected.");
            }
            else
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.CapabilitiesInsufficient,
                    $"Missing required capabilities: {MissingNames(candidate, required)}.");
            }
        }

        SetVerdict(verdicts, winner, CandidateOutcome.Selected,
            $"Selected: satisfies {Count(satisfied)} of {Count(required)} requested capabilities.");

        var winningTrace = BuildTrace(verdicts);
        return new SelectionResult(winner.Descriptor, mode, required, satisfied, winningTrace, null);
    }

    /// <summary>
    ///     Applies the caller override or the availability filter to reduce the format-matched
    ///     candidates to those eligible for ranking.
    /// </summary>
    /// <param name="format">The detected format, used to compose failure explanations.</param>
    /// <param name="options">The effective options carrying any preferred extractor identifier.</param>
    /// <param name="mode">The selection mode derived from whether an override is present.</param>
    /// <param name="required">The required capabilities, used only for failure explanations.</param>
    /// <param name="formatMatched">The candidates that support the detected format.</param>
    /// <param name="verdicts">The verdict map to populate for excluded and unavailable candidates.</param>
    /// <param name="failure">
    ///     Set to the terminal <see cref="SelectionResult"/> when the override does not apply, the
    ///     override is unavailable, or no candidate is available; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     The candidates eligible for ranking; empty and paired with a non-null
    ///     <paramref name="failure"/> when selection cannot proceed.
    /// </returns>
    /// <remarks>
    ///     Split out from <see cref="Select"/> so the override rules — which must never silently fall
    ///     back — and the automatic availability filter read as one focused decision. Pure and
    ///     side-effect free apart from populating <paramref name="verdicts"/>.
    /// </remarks>
    private static List<ExtractorCandidate> ResolveAvailable(
        FormatDetection format, ExtractionOptions options, SelectionMode mode, ExtractorCapabilities required,
        List<ExtractorCandidate> formatMatched, Dictionary<string, CandidateVerdict> verdicts,
        out SelectionResult? failure)
    {
        failure = null;

        // Caller override: reduce to the single named backend, never falling back to another
        if (mode == SelectionMode.CallerOverride)
        {
            var named = formatMatched.FirstOrDefault(
                candidate => string.Equals(candidate.Descriptor.Id, options.PreferredExtractorId, StringComparison.Ordinal));

            // The named backend does not support this format (it is either absent or a format mismatch)
            if (named is null)
            {
                foreach (var candidate in formatMatched)
                {
                    SetVerdict(verdicts, candidate, CandidateOutcome.ExcludedByOverride,
                        $"Excluded because the caller requested '{options.PreferredExtractorId}'.");
                }

                var trace = BuildTrace(verdicts);
                failure = new SelectionResult(null, mode, required, ExtractorCapabilities.None, trace,
                    MakeFailure(ExtractionFailureKind.RequestedExtractorNotApplicable,
                        DiagnosticCodes.RequestedExtractorNotApplicable,
                        $"The requested extractor '{options.PreferredExtractorId}' does not support the detected format.",
                        format, trace,
                        $"Omit the preferred extractor, or request one that supports '{format.Format.Id}'."));
                return [];
            }

            // Every other in-format candidate is excluded by the operator's explicit command
            foreach (var candidate in formatMatched.Where(candidate => !ReferenceEquals(candidate, named)))
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.ExcludedByOverride,
                    $"Excluded because the caller requested '{options.PreferredExtractorId}'.");
            }

            // A named-but-unavailable backend fails rather than degrading to a different one
            if (!named.Availability.IsAvailable)
            {
                SetVerdict(verdicts, named, CandidateOutcome.Unavailable, ReasonOf(named));
                var trace = BuildTrace(verdicts);
                failure = new SelectionResult(null, mode, required, ExtractorCapabilities.None, trace,
                    MakeFailure(ExtractionFailureKind.RequestedExtractorUnavailable,
                        DiagnosticCodes.RequestedExtractorUnavailable,
                        $"The requested extractor '{options.PreferredExtractorId}' is unavailable in this environment.",
                        format, trace,
                        "Run where the requested extractor is available, or choose a different extractor."));
                return [];
            }

            return [named];
        }

        // Automatic: drop unavailable candidates, recording each reason so the failure message is good
        var eligible = new List<ExtractorCandidate>();
        foreach (var candidate in formatMatched)
        {
            if (candidate.Availability.IsAvailable)
            {
                eligible.Add(candidate);
            }
            else
            {
                SetVerdict(verdicts, candidate, CandidateOutcome.Unavailable, ReasonOf(candidate));
            }
        }

        if (eligible.Count == 0)
        {
            var trace = BuildTrace(verdicts);
            failure = new SelectionResult(null, mode, required, ExtractorCapabilities.None, trace,
                MakeFailure(ExtractionFailureKind.NoAvailableExtractor, DiagnosticCodes.NoAvailableExtractor,
                    "No available extractor can process the detected format.", format, trace,
                    $"Install or enable a backend that supports '{format.Format.Id}' in this environment."));
        }

        return eligible;
    }

    /// <summary>
    ///     Computes the capabilities the extraction requires from the request.
    /// </summary>
    /// <param name="options">The effective options.</param>
    /// <returns>The required capability set: <see cref="ExtractorCapabilities.Text"/> plus any implied by the options.</returns>
    /// <remarks>
    ///     Text is always required because a document-to-markdown extraction that produces no text is
    ///     pointless; the image, page, and explicit requirements are added only when the caller asked
    ///     for them so an unrequested capability never forces a degrade. Pure.
    /// </remarks>
    private static ExtractorCapabilities ComputeRequired(ExtractionOptions options)
    {
        // Text is the irreducible requirement; the rest are additive from the request
        var required = ExtractorCapabilities.Text;
        if (options.IncludeEmbeddedImages)
        {
            required |= ExtractorCapabilities.EmbeddedImages;
        }

        if (options.RenderPages)
        {
            required |= ExtractorCapabilities.RenderedPages;
        }

        if (options.RequireCapabilities.HasValue)
        {
            required |= options.RequireCapabilities.Value;
        }

        return required;
    }

    /// <summary>
    ///     Determines whether an extractor supports the given format by identifier.
    /// </summary>
    /// <param name="descriptor">The candidate descriptor.</param>
    /// <param name="format">The detected format.</param>
    /// <returns><see langword="true"/> when the descriptor lists a format with a matching identifier.</returns>
    /// <remarks>
    ///     Matches on the format identifier (ordinal) rather than full value equality so a custom
    ///     format declared with the same identifier but a differing media-type string still matches.
    ///     Pure.
    /// </remarks>
    private static bool Supports(ExtractorDescriptor descriptor, DocumentFormat format) =>
        descriptor.SupportedFormats.Any(supported => string.Equals(supported.Id, format.Id, StringComparison.Ordinal));

    /// <summary>
    ///     Records a verdict for a candidate, overwriting any prior verdict for the same identifier.
    /// </summary>
    /// <param name="verdicts">The verdict map keyed by extractor identifier.</param>
    /// <param name="candidate">The candidate the verdict concerns.</param>
    /// <param name="outcome">The classification for the candidate.</param>
    /// <param name="detail">The human-readable explanation of the outcome.</param>
    /// <remarks>
    ///     Overwriting is intentional: a candidate can be provisionally classified (for example
    ///     excluded by override) and then finalized (for example unavailable) as the algorithm
    ///     narrows the field, and only the final verdict should appear in the trace. Pure apart from
    ///     the map mutation.
    /// </remarks>
    private static void SetVerdict(
        Dictionary<string, CandidateVerdict> verdicts, ExtractorCandidate candidate,
        CandidateOutcome outcome, string detail) =>
        verdicts[candidate.Descriptor.Id] = new CandidateVerdict(
            candidate.Descriptor.Id, candidate.Descriptor.DisplayName, candidate.Descriptor.Priority, outcome, detail);

    /// <summary>
    ///     Builds the deterministic candidate trace from the accumulated verdicts.
    /// </summary>
    /// <param name="verdicts">The verdict map keyed by extractor identifier.</param>
    /// <returns>The verdicts ordered with the selected candidate first, then by identifier ascending.</returns>
    /// <remarks>
    ///     Ordering the selected candidate first and then by identifier makes the trace independent of
    ///     registration order, so the same inputs always serialize to the same trace. Pure.
    /// </remarks>
    private static IReadOnlyList<CandidateVerdict> BuildTrace(Dictionary<string, CandidateVerdict> verdicts) =>
        verdicts.Values
            .OrderBy(verdict => verdict.Outcome == CandidateOutcome.Selected ? 0 : 1)
            .ThenBy(verdict => verdict.ExtractorId, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    ///     Builds a structured selection failure with a ready-to-display explanation.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    /// <param name="code">The fixed diagnostic code for the kind.</param>
    /// <param name="headline">The one-line headline shown first in the explanation.</param>
    /// <param name="format">The detected format, named in the explanation.</param>
    /// <param name="trace">The candidate verdicts to render as an indented per-backend breakdown.</param>
    /// <param name="remedy">A suggested remedy, rendered on a trailing <c>Remedy:</c> line.</param>
    /// <returns>The composed <see cref="ExtractionFailure"/>.</returns>
    /// <remarks>
    ///     The explanation follows the worked example's shape — headline, detected format, one
    ///     indented block per candidate naming the backend, its verdict, and its specific reason, then
    ///     a remedy line — because a named-backend, named-reason message is the difference between a
    ///     vague error and a useful one. Pure.
    /// </remarks>
    private static ExtractionFailure MakeFailure(
        ExtractionFailureKind kind, string code, string headline, FormatDetection format,
        IReadOnlyList<CandidateVerdict> trace, string? remedy)
    {
        // Compose the multi-line explanation once so the failure carries display-ready prose
        var builder = new StringBuilder();
        builder.Append(headline).Append('\n');
        builder.Append("Detected format: ").Append(format.Describe()).Append('\n');

        if (trace.Count > 0)
        {
            builder.Append('\n').Append("Candidates considered:\n");
            foreach (var verdict in trace)
            {
                builder.Append("  - ").Append(verdict.ExtractorId).Append("  ")
                    .Append(verdict.DisplayName).Append("  [").Append(VerdictLabel(verdict.Outcome)).Append("]\n");
                builder.Append("        ").Append(verdict.Detail).Append('\n');
            }
        }

        if (!string.IsNullOrEmpty(remedy))
        {
            builder.Append('\n').Append("Remedy: ").Append(remedy).Append('\n');
        }

        return new ExtractionFailure(kind, code, headline, builder.ToString().TrimEnd('\n'), trace, remedy);
    }

    /// <summary>
    ///     Maps a candidate outcome to the uppercase label used in a failure explanation.
    /// </summary>
    /// <param name="outcome">The candidate outcome.</param>
    /// <returns>A short uppercase label describing the outcome.</returns>
    /// <remarks>Kept in one place so the explanation vocabulary stays consistent. Pure.</remarks>
    private static string VerdictLabel(CandidateOutcome outcome) => outcome switch
    {
        CandidateOutcome.Selected => "SELECTED",
        CandidateOutcome.FormatNotSupported => "FORMAT NOT SUPPORTED",
        CandidateOutcome.Unavailable => "UNAVAILABLE",
        CandidateOutcome.CapabilitiesInsufficient => "CAPABILITIES INSUFFICIENT",
        CandidateOutcome.OutrankedByHigherFidelity => "OUTRANKED",
        CandidateOutcome.ExcludedByOverride => "EXCLUDED BY OVERRIDE",
        _ => outcome.ToString().ToUpperInvariant()
    };

    /// <summary>
    ///     Renders a candidate's supported formats as a comma-separated list of identifiers.
    /// </summary>
    /// <param name="candidate">The candidate whose supported formats are listed.</param>
    /// <returns>The comma-separated identifiers, or <c>nothing</c> when the candidate supports none.</returns>
    /// <remarks>Used only for the format-mismatch verdict detail. Pure.</remarks>
    private static string FormatsOf(ExtractorCandidate candidate)
    {
        // Name what the backend does handle so a mismatch verdict is actionable
        var ids = candidate.Descriptor.SupportedFormats.Select(format => format.Id).ToList();
        return ids.Count == 0 ? "nothing" : string.Join(", ", ids);
    }

    /// <summary>
    ///     Renders the required capabilities a candidate is missing as a comma-separated list.
    /// </summary>
    /// <param name="candidate">The candidate whose effective capabilities are compared against the requirement.</param>
    /// <param name="required">The required capability set.</param>
    /// <returns>The comma-separated camelCase names of the missing capabilities.</returns>
    /// <remarks>Names precisely what is missing so a capabilities verdict explains itself. Pure.</remarks>
    private static string MissingNames(ExtractorCandidate candidate, ExtractorCapabilities required)
    {
        // Mask the complement of the effective set to the required bits to isolate what is missing
        var missing = required & ~candidate.Availability.EffectiveCapabilities;
        return CapabilityNames(missing);
    }

    /// <summary>
    ///     Renders a capability set as a comma-separated list of camelCase names.
    /// </summary>
    /// <param name="capabilities">The capability set to render.</param>
    /// <returns>The comma-separated camelCase names, or <c>none</c> when empty.</returns>
    /// <remarks>Reuses the canonical flag ordering so names match the manifest. Pure.</remarks>
    private static string CapabilityNames(ExtractorCapabilities capabilities)
    {
        // Defer to the shared projection so selection and serialization never diverge
        var names = capabilities.ToCamelCaseNames();
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    /// <summary>
    ///     Reads a candidate's unavailable reason, substituting a stable fallback when none was given.
    /// </summary>
    /// <param name="candidate">The unavailable candidate.</param>
    /// <returns>The recorded unavailable reason, or a generic fallback.</returns>
    /// <remarks>
    ///     Recording the verbatim reason here is what makes the eventual failure message good, so a
    ///     missing reason is replaced rather than dropped. Pure.
    /// </remarks>
    private static string ReasonOf(ExtractorCandidate candidate) =>
        string.IsNullOrEmpty(candidate.Availability.UnavailableReason)
            ? "unavailable in this environment"
            : candidate.Availability.UnavailableReason;

    /// <summary>
    ///     Formats a capability set's flag count as an invariant-culture string.
    /// </summary>
    /// <param name="capabilities">The capability set to count.</param>
    /// <returns>The number of set flags rendered with the invariant culture.</returns>
    /// <remarks>Centralizes the culture-invariant formatting so verdict details are stable. Pure.</remarks>
    private static string Count(ExtractorCapabilities capabilities) =>
        capabilities.CountFlags().ToString(CultureInfo.InvariantCulture);
}
