namespace DocDown.Core;

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
