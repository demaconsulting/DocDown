using System.Text;

namespace DocDown.Core;

/// <summary>
///     Chooses the extractor to run for a detected format: filter by format, keep only what is
///     available here, and prefer a backend that can render pages when pages were requested.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Select"/> is a deliberately <strong>pure function</strong> of its arguments:
///         it performs no I/O, is not asynchronous, reads no clock, and holds no mutable static
///         state, so repeated calls with equal inputs always yield equal results. That purity is why
///         selection is a first-class unit rather than logic buried in <see cref="DocDownEngine"/> —
///         it can be exhaustively unit-tested with hand-built candidates and options.
///     </para>
///     <para>
///         There is no ranking explanation, no candidate verdict, and no capability negotiation: the
///         algorithm keeps the format-matched, available candidates, prefers one that can render
///         pages only when the caller asked for pages, and breaks any remaining choice by priority
///         then extractor identifier so the result is deterministic regardless of registration
///         order. When nothing remains, the failure states which package owns the format (an honest
///         fact, not an instruction).
///     </para>
///     <para>
///         Instances are stateless and therefore thread-safe; a single selector may be shared by
///         concurrent extractions.
///     </para>
/// </remarks>
public static class ExtractorSelector
{
    /// <summary>
    ///     Maps a well-known format identifier to the DocDown package that provides its extractor.
    /// </summary>
    /// <remarks>
    ///     This is a naming table only: it creates no project reference, no package reference, and
    ///     no dependency, so Core keeps its zero-runtime-dependency property. Its sole purpose is to
    ///     turn the unhelpful "no extractor is registered" message into one that names the package
    ///     which provides the missing extractor, so the reader knows which component owns the
    ///     capability. This is a statement of fact about where the capability lives, not advice.
    ///     Formats absent from the table fall back to generic wording rather than inventing a
    ///     package name. The legacy binary formats are deliberately absent: no DocDown package
    ///     extracts them.
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
    ///     format". Held as a set so the failure prose can state that unsupported status plainly for
    ///     these formats and only these. This never alters which extractor is chosen.
    /// </remarks>
    private static readonly HashSet<string> LegacyBinaryFormatIds =
        new(StringComparer.Ordinal) { "doc", "xls", "ppt", "vsd" };

    /// <summary>
    ///     Selects the extractor to run for a detected format, or reports in prose why none can be chosen.
    /// </summary>
    /// <param name="format">The detected format the extraction must handle. Must not be null.</param>
    /// <param name="options">The effective options; only <see cref="ExtractionOptions.RenderPages"/> affects selection. Must not be null.</param>
    /// <param name="candidates">The extractor candidates with their current availability. Must not be null.</param>
    /// <param name="failure">
    ///     Set to a prose failure naming the detected format (and, where known, the package that
    ///     provides the extractor) when no extractor can be chosen; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>The selected extractor descriptor, or <see langword="null"/> when none can be chosen.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="format"/>, <paramref name="options"/>, or
    ///     <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Pure and side-effect free. Keeps the format-matched, available candidates; when the caller
    ///     requested pages, prefers those that can render pages here; then chooses by descending
    ///     priority and finally by ascending identifier so the choice is stable across calls and
    ///     independent of registration order.
    /// </remarks>
    public static ExtractorDescriptor? Select(
        FormatDetection format, ExtractionOptions options, IReadOnlyList<ExtractorCandidate> candidates,
        out ExtractionFailure? failure)
    {
        // Reject null inputs so the function is total over the values it accepts
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(candidates);

        // Step 1: keep only candidates that support the detected format
        var formatMatched = candidates.Where(candidate => Supports(candidate.Descriptor, format.Format)).ToList();
        if (formatMatched.Count == 0)
        {
            failure = MakeFailure(
                "No registered extractor supports the detected format.", format,
                DescribeUnsupportedFormat(format.Format));
            return null;
        }

        // Step 2: keep only the candidates that can actually run in this environment
        var available = formatMatched.Where(candidate => candidate.Availability.IsAvailable).ToList();
        if (available.Count == 0)
        {
            failure = MakeFailure(
                "No available extractor can process the detected format in this environment.", format,
                DescribeUnsupportedFormat(format.Format));
            return null;
        }

        // Step 3: prefer a page renderer only when the caller asked for pages and one is available here
        var pool = available;
        if (options.RenderPages)
        {
            var renderers = available.Where(candidate => candidate.Availability.ProvidesRenderedPages).ToList();
            if (renderers.Count > 0)
            {
                pool = renderers;
            }
        }

        // Step 4: choose deterministically — higher priority wins, then the lower identifier
        var winner = pool
            .OrderByDescending(candidate => candidate.Descriptor.Priority)
            .ThenBy(candidate => candidate.Descriptor.Id, StringComparer.Ordinal)
            .First();

        failure = null;
        return winner.Descriptor;
    }

    /// <summary>
    ///     States, as fact, which package provides the extractor for an unsupported format.
    /// </summary>
    /// <param name="format">The detected format that no available candidate handles.</param>
    /// <returns>Prose naming the providing package, the legacy-binary status, or generic wording.</returns>
    /// <remarks>
    ///     Core deliberately recognizes more formats than it can extract, so "unsupported here" must
    ///     not read as "unsupported by DocDown" — except for the legacy binary formats, where that
    ///     is exactly what it means. This is a statement of fact about where the capability lives,
    ///     never an instruction to install anything. Pure and side-effect free.
    /// </remarks>
    private static string DescribeUnsupportedFormat(DocumentFormat format)
    {
        // Legacy binaries are detected but never extractable, so state that plainly
        if (LegacyBinaryFormatIds.Contains(format.Id))
        {
            return
                $"DocDown does not support the legacy binary Office formats, so '{format.Id}' " +
                "cannot be extracted by any DocDown package; only the modern XML-based Office " +
                "formats are supported.";
        }

        // Formats absent from the naming table get generic wording rather than a fabricated package name
        if (!WellKnownPackages.TryGetValue(format.Id, out var package))
        {
            return $"No registered extractor supports '{format.Id}'.";
        }

        return
            $"Core does not extract '{format.Id}' itself; that capability comes from the separate " +
            $"{package} extractor package, which a host registers with the engine.";
    }

    /// <summary>
    ///     Composes a prose failure with a headline, the detected format, and a fact about the format.
    /// </summary>
    /// <param name="headline">The one-line headline shown first in the explanation.</param>
    /// <param name="format">The detected format, named in the explanation.</param>
    /// <param name="detail">A fact about where the format's extractor lives, or its legacy status.</param>
    /// <returns>The composed <see cref="ExtractionFailure"/>.</returns>
    /// <remarks>Keeps the failure prose to fact only — no remedy, no candidate verdict. Pure.</remarks>
    private static ExtractionFailure MakeFailure(string headline, FormatDetection format, string detail)
    {
        var builder = new StringBuilder();
        builder.Append(headline).Append('\n');
        builder.Append("Detected format: ").Append(format.Describe()).Append('\n');
        builder.Append('\n').Append(detail);
        return new ExtractionFailure(headline, builder.ToString().TrimEnd('\n'));
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
}
