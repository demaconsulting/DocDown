using DocDown.Core;
using DocDown.Visio.Markdown;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DocDown.Visio.OpenXml;

/// <summary>
///     The Visio Open Packaging backend: extracts every page's name, shape text, and — the headline
///     extraction — the directed connector topology from a <c>.vsdx</c> or <c>.vsdm</c> into the
///     DocDown output contract, with no Visio installation present.
/// </summary>
/// <remarks>
///     <para>
///         A Visio drawing is an engineering schematic whose meaning is carried by which shapes
///         exist, what they are labeled, which page they belong to, and how they are connected. A
///         list of disconnected strings is not a schematic, so this backend resolves each connector
///         into a real directed edge (<c>Inlet Tank → Transfer Pump</c>) and surfaces every page name.
///         It is 100% managed — <c>DocumentFormat.OpenXml</c> has zero Visio types, so it reads the
///         package directly with <see cref="System.IO.Packaging"/> — and it deliberately stops at the
///         logical document structure: page names, shape text, connector topology, embedded images,
///         and document metadata. The spatial arrangement that only a render can recover is delivered
///         by the separate COM backend when Microsoft Visio is available.
///     </para>
///     <para>
///         Because nothing about this backend is environment-dependent, <see cref="ProbeAvailability"/>
///         is unconditional and performs no I/O, reporting that this backend is available but does
///         not render pages. Adverse drawings are not translated into results here: Core catches any
///         exception and converts it into a structured failure with the full layout still written.
///         Instances hold no per-extraction state and are safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class VisioOpenXmlExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The embedded Visio drawing the parse round-trip self-test reads.</summary>
    /// <remarks>
    ///     A real .vsdx authored in Microsoft Visio: one page holding two labeled shapes joined by a
    ///     glued dynamic connector, so reading it exercises shape text and topology together.
    /// </remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Office.Resources.probe.vsdx";
    /// <summary>
    ///     Creates a Visio Open Packaging extractor ready to register with a <see cref="DocDownBuilder"/>.
    /// </summary>
    /// <remarks>
    ///     Construction is deliberately trivial: the extractor holds no per-extraction state, opens
    ///     no file, and probes no environment here, so a single instance can be registered once and
    ///     reused across concurrent extractions. Most callers never call this directly and instead
    ///     use <c>AddVisio</c>, which registers a factory with the builder.
    /// </remarks>
    public VisioOpenXmlExtractor()
    {
    }

    /// <inheritdoc />
    public string Id => "visio-openxml";

    /// <inheritdoc />
    public string DisplayName => "Visio (Open Packaging)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Vsdx, CoreFormat.Vsdm];

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    /// <remarks>
    ///     Always available, without rendered-page support. <see cref="System.IO.Packaging"/> is a
    ///     managed assembly, so if this type could be constructed the extractor can run. Performs no
    ///     I/O and cannot throw.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available();

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var cancellationToken = context.CancellationToken;

        // Record what parsed this document, and state plainly that page rendering is not on offer here
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Visio", "visio.backend", "System.IO.Packaging (managed)", Available: true));
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Visio", "visio.pageRendering", "not provided by this extractor", Available: false));

        // Buffer the source: the package reader must seek, and a stream source is not guaranteed seekable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Any fault from here propagates to Core, which converts it into a structured failure
        using var stream = new MemoryStream(bytes, writable: false);
        var model = VisioPackageReader.Read(stream);

        await VisioContentEmitter.EmitAsync(sink, context.Options, model, cancellationToken).ConfigureAwait(false);
        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The cases describe this backend's own behavior: that it can read the drawing authored in
    ///     Microsoft Visio and embedded here — including its topology — and that page rendering is
    ///     genuinely not provided rather than
    ///     merely untested. The rendering case reports as skipped with a reason.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("visio.openxml.parseRoundTrip", Id, RunParseRoundTrip),
        new SelfTestCase("visio.pageRendering", Id, static _ => SelfTestResult.Skipped(
            "This extractor does not render pages; page rendering needs the COM backend."))
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>Buffering gives the reader a seekable stream to open the package. Read-only I/O.</remarks>
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
    ///     Reads the embedded drawing authored in Microsoft Visio — two labeled shapes joined by a
    ///     glued connector — parses it back, and confirms the connection resolved — proving the
    ///     topology path is genuinely functional in this deployment rather than merely present.
    /// </remarks>
    private static SelfTestResult RunParseRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = DateTimeOffset.UtcNow;

        try
        {
            var bytes = SelfTestProbe.Load(typeof(VisioOpenXmlExtractor).Assembly, ProbeResourceName);
            using var stream = new MemoryStream(bytes, writable: false);
            var model = VisioPackageReader.Read(stream);
            var page = model.Pages.Count == 1 ? model.Pages[0] : null;
            return page is { Connections.Count: 1 }
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The Open Packaging reader read the embedded Visio drawing but did not resolve its single connection.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The Open Packaging reader could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }
}
