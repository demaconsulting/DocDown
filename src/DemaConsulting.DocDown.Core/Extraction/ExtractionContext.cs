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
