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
