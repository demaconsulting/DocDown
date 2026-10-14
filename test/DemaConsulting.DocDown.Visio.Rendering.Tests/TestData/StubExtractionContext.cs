using DemaConsulting.DocDown.Core;
using CoreFormat = DemaConsulting.DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Visio.Rendering.Tests.TestData;

/// <summary>
///     A minimal <see cref="IExtractionContext"/> for driving a Visio extractor outside the engine.
/// </summary>
/// <remarks>
///     The engine's real context is assembled from a selection result and an environment the engine
///     owns, none of which a unit test of a backend needs. Supplying a plain stand-in keeps these
///     tests scoped to the extractor, and lets a test pass a token it controls. Immutable after construction.
/// </remarks>
internal sealed class StubExtractionContext : IExtractionContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StubExtractionContext"/> class.
    /// </summary>
    /// <param name="options">The options the extractor should observe.</param>
    /// <param name="sink">The sink the extractor writes through.</param>
    /// <param name="cancellationToken">The token the extractor should observe.</param>
    /// <remarks>Fills the detection, selection, and environment members with values matching a Visio backend.</remarks>
    public StubExtractionContext(ExtractionOptions options, IExtractionSink sink, CancellationToken cancellationToken)
    {
        Options = options;
        Sink = sink;
        CancellationToken = cancellationToken;
        DetectedFormat = new FormatDetection(CoreFormat.Vsdx, DetectionBasis.Extension);
        SelectedExtractor = new ExtractorDescriptor("visio-test", "Visio (test)", [CoreFormat.Vsdx], 0);
        Environment = new ExtractionEnvironment("TestOS", "X64", "test-runtime", "test-rid", []);
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
