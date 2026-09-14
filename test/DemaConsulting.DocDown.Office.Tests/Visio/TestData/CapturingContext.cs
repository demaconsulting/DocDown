using DocDown.Core;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Office.Tests.Visio.TestData;

/// <summary>
///     A minimal read-only extraction context that pairs a supplied sink with plausible metadata, so
///     an extractor's own emissions can be captured without the engine.
/// </summary>
internal sealed class CapturingContext : IExtractionContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CapturingContext"/> class.
    /// </summary>
    /// <param name="sink">The sink to capture emissions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="options">The options, or <see langword="null"/> for defaults.</param>
    public CapturingContext(IExtractionSink sink, CancellationToken cancellationToken, ExtractionOptions? options = null)
    {
        Sink = sink;
        CancellationToken = cancellationToken;
        Options = options ?? new ExtractionOptions();
    }

    /// <inheritdoc />
    public ExtractionOptions Options { get; }

    /// <inheritdoc />
    public IExtractionSink Sink { get; }

    /// <inheritdoc />
    public FormatDetection DetectedFormat { get; } = new(CoreFormat.Vsdx, DetectionBasis.Extension, 0.9);

    /// <inheritdoc />
    public ExtractorDescriptor SelectedExtractor { get; } = new(
        "visio-com", "Visio (COM automation)", [CoreFormat.Vsdx, CoreFormat.Vsdm], 0);

    /// <inheritdoc />
    public ExtractionEnvironment Environment { get; } = new("TestOS 1.0", "X64", "test-runtime 8.0", "test-rid", []);

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }
}
