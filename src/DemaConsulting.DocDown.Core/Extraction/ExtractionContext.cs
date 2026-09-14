namespace DocDown.Core;

/// <summary>
///     The concrete, immutable implementation of <see cref="IExtractionContext"/> that Core
///     constructs for one extraction and hands to the selected extractor.
/// </summary>
/// <remarks>
///     Kept <see langword="internal"/> because only Core creates contexts — an extractor merely
///     consumes the interface — which prevents callers from fabricating a context that bypasses
///     Core's sink and path controls. All values are captured at construction, so an instance is
///     immutable and safe to read from the extraction's thread.
/// </remarks>
internal sealed class ExtractionContext : IExtractionContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtractionContext"/> class.
    /// </summary>
    /// <param name="options">The already-cloned effective options for this extraction.</param>
    /// <param name="sink">The sink the extractor writes all output through.</param>
    /// <param name="detectedFormat">The format detection that selected this extractor.</param>
    /// <param name="selectedExtractor">The descriptor of the selected extractor.</param>
    /// <param name="environment">The environment description for this extraction.</param>
    /// <param name="cancellationToken">The cancellation token the extractor must observe.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="options"/>, <paramref name="sink"/>,
    ///     <paramref name="detectedFormat"/>, <paramref name="selectedExtractor"/>, or
    ///     <paramref name="environment"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Validates the reference arguments up front so an extractor never observes a partially
    ///     populated context.
    /// </remarks>
    public ExtractionContext(
        ExtractionOptions options,
        IExtractionSink sink,
        FormatDetection detectedFormat,
        ExtractorDescriptor selectedExtractor,
        ExtractionEnvironment environment,
        CancellationToken cancellationToken)
    {
        // Reject null dependencies so the extractor always sees a fully formed context
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(detectedFormat);
        ArgumentNullException.ThrowIfNull(selectedExtractor);
        ArgumentNullException.ThrowIfNull(environment);

        Options = options;
        Sink = sink;
        DetectedFormat = detectedFormat;
        SelectedExtractor = selectedExtractor;
        Environment = environment;
        CancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public ExtractionOptions Options { get; }

    /// <inheritdoc />
    public IExtractionSink Sink { get; }

    /// <inheritdoc />
    public FormatDetection DetectedFormat { get; }

    /// <inheritdoc />
    public ExtractorDescriptor SelectedExtractor { get; }

    /// <inheritdoc />
    public ExtractionEnvironment Environment { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }
}

/// <summary>
///     The read-only context Core supplies to an extractor for the duration of one extraction.
/// </summary>
/// <remarks>
///     The context is the extractor's <em>only</em> channel to the outside world: it exposes the
///     effective options, the write-only <see cref="IExtractionSink"/>, the detection and
///     selection decisions, the environment, and a cancellation token. Crucially it exposes no
///     filesystem path — the extractor never learns where output is written, which is what keeps
///     path allocation and containment entirely under Core's control. Implementations are
///     immutable for the lifetime of an extraction and safe to read from the extraction's thread.
/// </remarks>
public interface IExtractionContext
{
    /// <summary>
    ///     Gets the effective, already-cloned options for this extraction.
    /// </summary>
    /// <remarks>
    ///     This is Core's private snapshot, so an extractor may read it freely without affecting
    ///     the caller's original options.
    /// </remarks>
    ExtractionOptions Options { get; }

    /// <summary>
    ///     Gets the sink the extractor writes all output through.
    /// </summary>
    /// <remarks>
    ///     The sink returns the relative paths the extractor must use in markdown links, so the
    ///     extractor never constructs a path itself.
    /// </remarks>
    IExtractionSink Sink { get; }

    /// <summary>
    ///     Gets the format detection that led to this extractor being selected.
    /// </summary>
    /// <remarks>
    ///     Lets the extractor adapt to the detected format and record provenance without
    ///     re-sniffing the content.
    /// </remarks>
    FormatDetection DetectedFormat { get; }

    /// <summary>
    ///     Gets the descriptor of the extractor selected for this extraction.
    /// </summary>
    /// <remarks>
    ///     Provided so an extractor can confirm its own selected identity and effective priority
    ///     as recorded by Core.
    /// </remarks>
    ExtractorDescriptor SelectedExtractor { get; }

    /// <summary>
    ///     Gets the environment description for this extraction.
    /// </summary>
    /// <remarks>
    ///     Exposed so an extractor can contribute environment-derived facts consistently with the
    ///     runtime information Core already captured.
    /// </remarks>
    ExtractionEnvironment Environment { get; }

    /// <summary>
    ///     Gets the cancellation token the extractor must observe.
    /// </summary>
    /// <remarks>
    ///     Extraction can be long-running, so honoring this token lets a caller abort promptly;
    ///     an <see cref="OperationCanceledException"/> raised from it propagates rather than being
    ///     converted into a failure.
    /// </remarks>
    CancellationToken CancellationToken { get; }
}
