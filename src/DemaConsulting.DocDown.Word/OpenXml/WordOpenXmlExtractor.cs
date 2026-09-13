using DocDown.Core;
using DocDown.Word.Markdown;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using CoreFormat = DocDown.Core.DocumentFormat;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocDown.Word.OpenXml;

/// <summary>
///     The Word Open XML backend: extracts text, real tables, embedded images, document control, and
///     metadata from a <c>.docx</c> into the DocDown output contract.
/// </summary>
/// <remarks>
///     <para>
///         This backend is 100% managed and carries no native assets, which is both its strength and
///         the precise boundary of what it can promise. It reads the structure a Word document
///         already contains — styled paragraphs, genuine table grids, embedded image parts, and the
///         document information — and it does materially better than the PDF backend on tables, which
///         Open XML preserves where a PDF flattened them. It cannot render a page, because
///         rasterization needs a renderer this package deliberately does not ship, so it declares
///         <see cref="ExtractorCapabilities.Text"/>, <see cref="ExtractorCapabilities.EmbeddedImages"/>,
///         <see cref="ExtractorCapabilities.DocumentMetadata"/>, and
///         <see cref="ExtractorCapabilities.DocumentStructure"/> and pointedly not
///         <see cref="ExtractorCapabilities.RenderedPages"/>.
///     </para>
///     <para>
///         Because nothing about this backend is environment-dependent — no native binary, no
///         external application — <see cref="ProbeAvailability"/> is unconditional and performs no
///         I/O. Adverse documents are not translated into results here: Core catches any exception
///         and converts it into a structured failure with the full layout still written, so an
///         encrypted or malformed document surfaces as a failed result carrying a plain explanation
///         and never as an exception reaching the caller. Instances hold no per-extraction state and
///         are safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class WordOpenXmlExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>
    ///     Creates a Word Open XML extractor ready to register with a <see cref="DocDownBuilder"/>.
    /// </summary>
    /// <remarks>
    ///     Construction is deliberately trivial: the extractor holds no per-extraction state, opens
    ///     no file, and probes no environment here, so a single instance can be registered once and
    ///     reused across concurrent extractions. Most callers never call this directly and instead
    ///     use <c>AddWord</c>, which registers a factory with the builder.
    /// </remarks>
    public WordOpenXmlExtractor()
    {
    }

    /// <inheritdoc />
    public string Id => "word-openxml";

    /// <inheritdoc />
    public string DisplayName => "Word (Open XML SDK)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Docx];

    /// <inheritdoc />
    public ExtractorCapabilities Capabilities =>
        ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages
        | ExtractorCapabilities.DocumentMetadata | ExtractorCapabilities.DocumentStructure;

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    /// <remarks>
    ///     Always available, with the full declared capability set. There is nothing to probe: the
    ///     Open XML SDK is a managed assembly that ships inside this package, so if this type could be
    ///     constructed the extractor can run. Performs no I/O and cannot throw.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available(Capabilities);

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var cancellationToken = context.CancellationToken;

        // Record what parsed this document, and state plainly that page rendering is not on offer
        sink.ReportEnvironmentFact(new EnvironmentFact("DocDown.Word", "word.backend", "Open XML SDK (managed)", Available: true));
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Word", "word.pageRendering", "not provided by this extractor", Available: false));

        // Buffer the source: the package reader must seek, and a stream source is not guaranteed seekable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Any fault from here — encrypted (raised as WordExtractionException), malformed, truncated —
        // propagates to Core, which converts it into a structured failure that still writes the layout
        using var stream = new MemoryStream(bytes, writable: false);
        var model = new WordOpenXmlReader().Read(stream);

        var degraded = await WordContentEmitter.EmitAsync(sink, context.Options, model, cancellationToken)
            .ConfigureAwait(false);
        return degraded ? ExtractionOutcome.Degraded : ExtractionOutcome.Succeeded;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The cases describe this backend's own behavior in its deployed environment: that it can
    ///     read a document it builds itself, and that page rendering is genuinely unavailable rather
    ///     than merely untested. The rendering case reports as skipped with a reason, because a
    ///     capability this package does not claim must not be reported as a failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("word.openxml.parseRoundTrip", Id, RunParseRoundTrip),
        new SelfTestCase("word.pageRendering", Id, static _ => SelfTestResult.Skipped(
            "This extractor does not provide the renderedPages capability; page rendering is not attempted."))
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     Buffering makes a stream source and a file source behave identically and gives the reader a
    ///     seekable stream to inspect the OLE compound-file signature and open the package. Read-only I/O.
    /// </remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Runs the parse round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Builds a one-paragraph document in memory and reads it back, which proves the SDK is
    ///     genuinely functional in this deployment rather than merely present. Uses no filesystem
    ///     location of its own because the whole round trip fits in memory.
    /// </remarks>
    private static SelfTestResult RunParseRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = DateTimeOffset.UtcNow;

        try
        {
            using var stream = new MemoryStream();
            BuildProbeDocument(stream);
            stream.Position = 0;
            var model = new WordOpenXmlReader().Read(stream);
            return model.Body.Count > 0
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The Open XML reader read a document it built itself but found no content in its body.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The Open XML reader could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Builds the one-paragraph document the parse round-trip case reads back.
    /// </summary>
    /// <param name="stream">The stream to write the document into.</param>
    /// <remarks>
    ///     Built rather than embedded so the case ships no binary payload and exercises the writer and
    ///     the reader together. Side effect: writes to <paramref name="stream"/>.
    /// </remarks>
    private static void BuildProbeDocument(Stream stream)
    {
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();

        var run = new W.Run();
        run.AppendChild(new W.Text("DocDown self test"));
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(run);
        var body = new W.Body();
        body.AppendChild(paragraph);
        var root = new W.Document();
        root.AppendChild(body);
        mainPart.Document = root;
    }
}
