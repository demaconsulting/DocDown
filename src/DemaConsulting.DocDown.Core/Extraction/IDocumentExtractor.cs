namespace DocDown.Core;

/// <summary>
///     The contract a document extractor (backend) implements to participate in DocDown.
/// </summary>
/// <remarks>
///     <para>
///         An extractor converts a source document into content, images, and pages by writing
///         through the <see cref="IExtractionSink"/> on its context; it is never given an output
///         path and never touches the output filesystem directly.
///     </para>
///     <para>
///         <see cref="ProbeAvailability"/> is a hot, frequently called control point and carries
///         strict obligations: it must be cheap (well under 50 ms), side-effect free, must not
///         open the document or launch an external application, and must not throw. Core does not
///         trust callers to honor the no-throw rule — it defensively catches exceptions from this
///         method, treats the extractor as unavailable, and emits a diagnostic — but a
///         conforming implementation still must not throw.
///     </para>
///     <para>
///         Implementations should be safe to invoke from the extraction's thread; Core does not
///         call a single extractor instance concurrently for one extraction.
///     </para>
/// </remarks>
public interface IDocumentExtractor
{
    /// <summary>
    ///     Gets the extractor's stable, unique identifier used for lookup and override.
    /// </summary>
    /// <remarks>
    ///     Must be stable across releases because callers and manifests reference it; duplicate
    ///     identifiers are rejected at build time.
    /// </remarks>
    string Id { get; }

    /// <summary>
    ///     Gets a human-readable name for the extractor shown in summaries and status.
    /// </summary>
    /// <remarks>
    ///     Distinct from <see cref="Id"/> so the display text can change without breaking the
    ///     stable lookup key.
    /// </remarks>
    string DisplayName { get; }

    /// <summary>
    ///     Gets the document formats this extractor can process.
    /// </summary>
    /// <remarks>
    ///     Used by selection to filter candidates before considering availability or
    ///     capabilities.
    /// </remarks>
    IReadOnlyCollection<DocumentFormat> SupportedFormats { get; }

    /// <summary>
    ///     Gets the extractor's ranking priority; higher values are preferred when more than one
    ///     candidate remains.
    /// </summary>
    /// <remarks>
    ///     Only breaks a remaining choice after format and the page-rendering preference have been
    ///     applied; the final tie-break is the extractor identifier, so selection is deterministic.
    /// </remarks>
    int Priority { get; }

    /// <summary>
    ///     Gets a value indicating whether rendering document pages to images is a meaningful
    ///     request for this extractor's formats.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Page rendering is a request, not a demand: for a paginated format (PDF, Word,
    ///         PowerPoint, Visio) a <c>--pages</c> request that this environment cannot satisfy is
    ///         recorded as a plain note, because a better environment would deliver rendered pages.
    ///         For a non-paginated format such as a spreadsheet there is no page grid to render, so
    ///         the same request applies to nothing and the engine honors it with silence rather than
    ///         a note.
    ///     </para>
    ///     <para>
    ///         The default is <see langword="true"/> because most formats are paginated in principle;
    ///         an extractor whose format has no pages overrides it to <see langword="false"/>. This is
    ///         a property of the format the extractor handles, not of whether this particular backend
    ///         happens to render, so a managed backend that cannot render still reports
    ///         <see langword="true"/> when a companion backend could.
    ///     </para>
    /// </remarks>
    bool PageRenderingApplicable => true;

    /// <summary>
    ///     Probes whether the extractor can run in the current environment and whether it can render
    ///     document pages here.
    /// </summary>
    /// <returns>An <see cref="ExtractorAvailability"/> describing availability and page-rendering support.</returns>
    /// <remarks>
    ///     Must be cheap (&lt; 50 ms), side-effect free, and must not open the document, launch an
    ///     application, or throw. Core caches the result until availability is refreshed and
    ///     defensively treats a thrown exception as unavailable.
    /// </remarks>
    ExtractorAvailability ProbeAvailability();

    /// <summary>
    ///     Extracts the source document, writing all output through the context's sink.
    /// </summary>
    /// <param name="source">The document to extract. The extractor opens it via <see cref="DocumentSource.OpenRead"/>.</param>
    /// <param name="context">The extraction context providing options, the sink, and cancellation.</param>
    /// <returns>
    ///     A <see cref="ValueTask{TResult}"/> yielding <see cref="ExtractionOutcome.Produced"/> once
    ///     the extractor has written its output.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     May be thrown when the context's cancellation token is signaled; this propagates rather
    ///     than being converted into a structured failure.
    /// </exception>
    /// <remarks>
    ///     The extractor must route every artifact through <paramref name="context"/>'s sink and
    ///     use the relative paths the sink returns in markdown links. Any other thrown exception is
    ///     caught by Core and converted into an <see cref="ExtractionOutcome.Unreadable"/> result with
    ///     a prose failure, so the extractor need not translate failures into results itself.
    /// </remarks>
    ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context);
}

/// <summary>
///     The availability of a document extractor in the current environment, and whether it can
///     render document pages to images here.
/// </summary>
/// <param name="IsAvailable">
///     <see langword="true"/> when the extractor can run in this environment; otherwise
///     <see langword="false"/>.
/// </param>
/// <param name="UnavailableReason">
///     A human-readable reason the extractor is unavailable, or <see langword="null"/> when it
///     is available.
/// </param>
/// <param name="ProvidesRenderedPages">
///     <see langword="true"/> when the extractor can render document pages to raster images in this
///     environment. This is the one environment-dependent fact selection needs: a managed backend
///     that only extracts text reports <see langword="false"/>, while a renderer whose native stack
///     loaded reports <see langword="true"/>.
/// </param>
/// <remarks>
///     Page rendering is separated out because it is the only capability that varies with the
///     environment and the only one selection reasons about: a backend may be able to render pages
///     in principle yet be unable to on a given platform, and selection must know what is truly
///     possible here. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ExtractorAvailability(
    bool IsAvailable, string? UnavailableReason, bool ProvidesRenderedPages)
{
    /// <summary>
    ///     Creates an availability result for an extractor that can run here.
    /// </summary>
    /// <param name="providesRenderedPages">
    ///     <see langword="true"/> when the extractor can render document pages to images in this
    ///     environment; <see langword="false"/> (the default) when it cannot.
    /// </param>
    /// <returns>An available <see cref="ExtractorAvailability"/> with no unavailable reason.</returns>
    /// <remarks>
    ///     A named factory makes available results read clearly at call sites and guarantees the
    ///     reason is <see langword="null"/> for the available case. Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Available(bool providesRenderedPages = false) =>
        new(true, null, providesRenderedPages);

    /// <summary>
    ///     Creates an availability result for an extractor that cannot run here.
    /// </summary>
    /// <param name="reason">The human-readable reason the extractor is unavailable. Must not be null or empty.</param>
    /// <returns>An unavailable <see cref="ExtractorAvailability"/> that renders no pages.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
    /// <remarks>
    ///     Requires a non-empty reason so an unavailable backend is never reported without an
    ///     explanation the caller can surface. Page rendering is forced to <see langword="false"/>
    ///     because nothing is usable when unavailable. Pure and thread-safe.
    /// </remarks>
    public static ExtractorAvailability Unavailable(string reason)
    {
        // Refuse an empty reason so an unavailable status always carries a displayable cause
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new ExtractorAvailability(false, reason, false);
    }
}

/// <summary>
///     An immutable description of a registered extractor's identity captured once at registration
///     time.
/// </summary>
/// <param name="Id">The extractor's stable, unique identifier used for lookup.</param>
/// <param name="DisplayName">A human-readable name for the extractor shown in summaries.</param>
/// <param name="SupportedFormats">The document formats the extractor can process.</param>
/// <param name="Priority">
///     The extractor's ranking priority; higher values are preferred when the format and
///     page-rendering preference leave more than one candidate.
/// </param>
/// <param name="PageRenderingApplicable">
///     Whether rendering document pages to images is a meaningful request for the extractor's
///     formats. <see langword="true"/> for a paginated format (the default), <see langword="false"/>
///     for a non-paginated one such as a spreadsheet, so the engine can honor a page request against
///     a non-paginated format with silence rather than a note about an absent renderer.
/// </param>
/// <remarks>
///     A descriptor is a snapshot detached from the live <see cref="IDocumentExtractor"/>
///     instance so selection and reporting can reason about an extractor without invoking it or
///     depending on its lifetime. Instances are immutable and thread-safe; the
///     <see cref="SupportedFormats"/> list is captured at construction and not copied defensively,
///     so callers must pass a list they will not mutate.
/// </remarks>
public sealed record ExtractorDescriptor(
    string Id, string DisplayName, IReadOnlyList<DocumentFormat> SupportedFormats,
    int Priority, bool PageRenderingApplicable = true);

/// <summary>
///     Marks an extractor as contributing self-test cases that Core can enumerate and run to
///     validate the backend in its deployed environment.
/// </summary>
/// <remarks>
///     The seam exists so a backend can prove it actually works where it is installed — native
///     dependencies and platform quirks make declared capabilities insufficient evidence on
///     their own. Implementations should return cheap, self-contained cases; Core wraps the
///     cases of an unavailable backend so they report as skipped without running.
/// </remarks>
public interface ISelfValidating
{
    /// <summary>
    ///     Returns the self-test cases this extractor contributes.
    /// </summary>
    /// <returns>
    ///     The self-test cases to run; an empty sequence when the extractor contributes none.
    ///     Must never be <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     Enumerated by the engine when assembling the full self-test suite. Implementations
    ///     should make this cheap and side-effect free; the actual work happens when a case's
    ///     delegate is invoked, not when the cases are enumerated.
    /// </remarks>
    IEnumerable<SelfTestCase> GetSelfTestCases();
}
