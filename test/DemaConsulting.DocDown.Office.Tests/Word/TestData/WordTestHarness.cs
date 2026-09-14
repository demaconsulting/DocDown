using DocDown.Core;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Office.Tests.Word.TestData;

/// <summary>
///     Runs a single extractor directly against a real sink and returns the extraction folder, so a
///     backend's <see cref="IDocumentExtractor.ExtractAsync"/> can be exercised without the engine's
///     availability selection.
/// </summary>
/// <remarks>Writes <c>content.md</c> from the buffered sink content so callers can read the rendered flow.</remarks>
internal static class WordTestHarness
{
    /// <summary>
    ///     Extracts a source through an extractor into a fresh scratch folder and writes its content.
    /// </summary>
    /// <param name="extractor">The extractor to run.</param>
    /// <param name="source">The document source.</param>
    /// <param name="scratch">The scratch folder path.</param>
    /// <param name="options">The extraction options.</param>
    /// <param name="format">The detected document format to report on the context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction folder path and the extractor's reported outcome.</returns>
    public static async Task<(string Folder, ExtractionOutcome Outcome)> ExtractAsync(
        IDocumentExtractor extractor, DocumentSource source, string scratch, ExtractionOptions options,
        CoreFormat format, CancellationToken cancellationToken)
    {
        var folder = ScratchFolder.Prepare(scratch, options.ScratchFolder);
        var sink = new ExtractionSink(folder, options);
        var descriptor = new ExtractorDescriptor(
            extractor.Id, extractor.DisplayName, extractor.SupportedFormats.ToList(),
            extractor.Priority, extractor.PageRenderingApplicable);
        var environment = new ExtractionEnvironment("TestOS 1.0", "X64", "test-runtime 8.0", "test-rid", []);
        var detection = new FormatDetection(format, DetectionBasis.Extension, 0.9);
        var context = new HarnessContext(options, sink, detection, descriptor, environment, cancellationToken);

        var outcome = await extractor.ExtractAsync(source, context);
        await ContentWriter.WriteAsync(sink, null, cancellationToken);
        return (folder.AbsolutePath, outcome);
    }

    /// <summary>
    ///     Reads the content document from an extraction folder.
    /// </summary>
    /// <param name="folder">The extraction folder.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The content markdown.</returns>
    public static Task<string> ReadContentAsync(string folder, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(Path.Combine(folder, "content.md"), cancellationToken);

    /// <summary>
    ///     A minimal read-only extraction context backed by a real sink.
    /// </summary>
    /// <remarks>Exposes exactly what an extractor reads; holds no output path.</remarks>
    private sealed class HarnessContext : IExtractionContext
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HarnessContext"/> class.
        /// </summary>
        /// <param name="options">The effective options.</param>
        /// <param name="sink">The sink.</param>
        /// <param name="detectedFormat">The detected format.</param>
        /// <param name="selectedExtractor">The selected extractor descriptor.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public HarnessContext(
            ExtractionOptions options, IExtractionSink sink, FormatDetection detectedFormat,
            ExtractorDescriptor selectedExtractor, ExtractionEnvironment environment, CancellationToken cancellationToken)
        {
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
}
