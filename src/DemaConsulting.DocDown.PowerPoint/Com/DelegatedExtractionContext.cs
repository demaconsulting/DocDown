using DocDown.Core;

namespace DocDown.PowerPoint.Com;

/// <summary>
///     A private <see cref="IExtractionContext"/> that reuses an outer context but substitutes a
///     modified options object for the delegated managed extraction.
/// </summary>
/// <remarks>
///     Core keeps its own <see cref="IExtractionContext"/> implementation internal, so this backend
///     supplies its own to run the managed backend against the same sink, format, environment, and
///     cancellation token while overriding the options — specifically to force
///     <see cref="ExtractionOptions.RenderPages"/> off so the delegate does not emit a render gap
///     that this backend answers itself. It also composes the delegate's output through a
///     <see cref="ComposingDelegatedSink"/> so the delegate's "rendering not provided" fact and
///     stale images-gap remedy do not contradict the rendering this COM run performs. Immutable
///     after construction and safe to read from the extraction thread.
/// </remarks>
internal sealed class DelegatedExtractionContext : IExtractionContext
{
    /// <summary>The outer context whose format, environment, and token are reused.</summary>
    private readonly IExtractionContext _inner;

    /// <summary>The composing wrapper over the outer sink that reconciles the delegate's rendering statements.</summary>
    private readonly ComposingDelegatedSink _sink;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegatedExtractionContext"/> class.
    /// </summary>
    /// <param name="inner">The outer context to reuse. Must not be null.</param>
    /// <param name="options">The already-cloned options for the delegated extraction. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="options"/> is null.</exception>
    public DelegatedExtractionContext(IExtractionContext inner, ExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        _inner = inner;
        _sink = new ComposingDelegatedSink(inner.Sink);
        Options = options;
    }

    /// <inheritdoc />
    public ExtractionOptions Options { get; }

    /// <inheritdoc />
    public IExtractionSink Sink => _sink;

    /// <inheritdoc />
    public FormatDetection DetectedFormat => _inner.DetectedFormat;

    /// <inheritdoc />
    public ExtractorDescriptor SelectedExtractor => _inner.SelectedExtractor;

    /// <inheritdoc />
    public ExtractionEnvironment Environment => _inner.Environment;

    /// <inheritdoc />
    public CancellationToken CancellationToken => _inner.CancellationToken;
}
