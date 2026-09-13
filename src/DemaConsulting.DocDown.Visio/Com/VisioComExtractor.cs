using System.Globalization;
using DocDown.Core;
using DocDown.Visio.Markdown;
using DocDown.Visio.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DocDown.Visio.Com;

/// <summary>
///     The Visio COM automation backend: a full superset extractor that produces page names, shape
///     text, and the directed connector topology by delegating to the managed Open Packaging backend,
///     then rasterizes each page to a PNG through Microsoft Visio over late-bound COM.
/// </summary>
/// <remarks>
///     <para>
///         The engine selects exactly one backend, so a rendering backend that advertised only
///         <see cref="ExtractorCapabilities.RenderedPages"/> would lose selection to the managed
///         backend and never render. This extractor therefore declares the full set —
///         <see cref="ExtractorCapabilities.Text"/>, <see cref="ExtractorCapabilities.EmbeddedImages"/>,
///         <see cref="ExtractorCapabilities.DocumentStructure"/>,
///         <see cref="ExtractorCapabilities.DocumentMetadata"/>, and
///         <see cref="ExtractorCapabilities.RenderedPages"/> — and delivers all five. It is chosen
///         over the managed backend only when page rendering is actually requested; otherwise the
///         managed backend wins on priority and no COM is touched. Its identifier <c>visio-com</c>
///         sorts before <c>visio-openxml</c>, so the explicit priority (0 versus 10) is what keeps
///         the deterministic managed backend the default rather than the ordinal tie-break silently
///         selecting COM.
///     </para>
///     <para>
///         Rather than re-implement the topology extraction, it constructs a
///         <see cref="VisioOpenXmlExtractor"/> and runs it against the same sink with rendering
///         suppressed, then adds the rendered pages the managed backend cannot. Everything apart from
///         talking to Visio is exercised cross-platform by injecting a stub
///         <see cref="IVisioAutomation"/>; the real adapter is the single untestable COM boundary. A
///         page that cannot be rendered becomes a counted, reason-bearing gap while the run continues
///         with the remaining pages, and on any host the logical topology is still delivered by the
///         delegated managed backend.
///     </para>
/// </remarks>
public sealed class VisioComExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The factory that produces the Visio automation session, or <see langword="null"/> when a test declares no adapter.</summary>
    private readonly Func<IVisioAutomation>? _automationFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VisioComExtractor"/> class backed by the real
    ///     Visio COM adapter.
    /// </summary>
    /// <remarks>Constructing the extractor never launches Visio; availability defers to the environment check.</remarks>
    public VisioComExtractor()
        : this(CreateDefaultAutomation)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VisioComExtractor"/> class with an injected
    ///     automation factory.
    /// </summary>
    /// <param name="automationFactory">
    ///     The factory that produces the Visio automation session, or <see langword="null"/> to
    ///     declare that no adapter is present so the backend probes unavailable on every platform.
    /// </param>
    /// <remarks>Internal so tests can inject a stub and exercise the whole extraction path without Microsoft Office.</remarks>
    internal VisioComExtractor(Func<IVisioAutomation>? automationFactory)
    {
        _automationFactory = automationFactory;
    }

    /// <inheritdoc />
    public string Id => "visio-com";

    /// <inheritdoc />
    public string DisplayName => "Visio (COM automation)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Vsdx, CoreFormat.Vsdm];

    /// <inheritdoc />
    public ExtractorCapabilities Capabilities =>
        ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages | ExtractorCapabilities.DocumentMetadata
        | ExtractorCapabilities.DocumentStructure | ExtractorCapabilities.RenderedPages;

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Cheap, side-effect free, and never launches Visio. When a test supplies no factory the
    ///     backend cannot work here regardless of the machine, so the probe reports unavailable.
    ///     Otherwise it falls through to the operating-system and ProgID check.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() =>
        _automationFactory is null
            ? ExtractorAvailability.Unavailable("The Visio COM automation adapter is not available in this build.")
            : VisioComAvailability.Probe(Capabilities);

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Buffer the source once: the managed backend reads it for topology, and the renderer reads a
        // file, and a stream source is not re-readable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Delegate the managed aspects to the Open Packaging backend with rendering suppressed, so it
        // writes page names, shape text, and the directed topology but not a rendering gap this backend answers
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await new VisioOpenXmlExtractor()
            .ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);

        // Record the authoritative rendering fact, complementing the managed backend's own fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Visio", "pages.renderer", "Microsoft Visio (COM automation)", Available: true));

        var factory = _automationFactory
            ?? throw new VisioExtractionException("The Visio COM automation adapter is not available in this build.");

        var pagesDegraded = await RenderPagesAsync(bytes, source, factory, sink, options, cancellationToken)
            .ConfigureAwait(false);

        return baseOutcome == ExtractionOutcome.Degraded || pagesDegraded
            ? ExtractionOutcome.Degraded
            : baseOutcome;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes the cases that prove the real Visio adapter works in its deployed environment.
    ///     Enumeration is cheap and environment-independent; the engine wraps every case as skipped
    ///     where the backend is unavailable so a machine without Visio produces no false failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("visio.com.available", Id, RunAvailable)
    ];

    /// <summary>
    ///     Renders every foreground page to the sink, isolating each page's faults.
    /// </summary>
    /// <param name="bytes">The buffered source drawing bytes.</param>
    /// <param name="source">The document source, consulted for a file path.</param>
    /// <param name="factory">The automation factory.</param>
    /// <param name="sink">The sink to write rendered pages and gaps through.</param>
    /// <param name="options">The effective options carrying the render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when any page failed to render; otherwise <see langword="false"/>.</returns>
    /// <remarks>Renders every page in one session; a page that cannot be exported degrades the run with a counted gap.</remarks>
    private static async ValueTask<bool> RenderPagesAsync(
        byte[] bytes, DocumentSource source, Func<IVisioAutomation> factory,
        IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        var (path, isTemporary) = MaterializePath(bytes, source);
        try
        {
            IReadOnlyList<VisioRenderedPage> pages;
            using (var automation = factory())
            {
                pages = automation.Render(path, options.PageRenderDpi);
            }

            sink.ReportFound(GapKind.Pages, pages.Count);

            var failures = new List<int>();
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (page.Png is { } png)
                {
                    using var pageStream = new MemoryStream(png, writable: false);
                    await sink.AddPageAsync(page.PageNumber, pageStream, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ReportPageFailure(sink, page.PageNumber, page.FailureReason);
                    failures.Add(page.PageNumber);
                }
            }

            if (failures.Count > 0)
            {
                ReportPageFailuresGap(sink, failures);
                return true;
            }

            return false;
        }
        finally
        {
            if (isTemporary)
            {
                TryDelete(path);
            }
        }
    }

    /// <summary>Materializes a path Visio can open, preferring the source file and falling back to a temp copy.</summary>
    /// <param name="bytes">The buffered drawing bytes.</param>
    /// <param name="source">The document source.</param>
    /// <returns>The path and whether it is a temporary file this backend must delete.</returns>
    private static (string Path, bool IsTemporary) MaterializePath(byte[] bytes, DocumentSource source)
    {
        if (source.Path is { } existing && File.Exists(existing))
        {
            return (existing, false);
        }

        var extension = source.FileName.EndsWith(".vsdm", StringComparison.OrdinalIgnoreCase) ? ".vsdm" : ".vsdx";
        var temp = Path.Combine(Path.GetTempPath(), "docdown-vsdx-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(temp, bytes);
        return (temp, true);
    }

    /// <summary>Deletes a temporary file, ignoring any fault.</summary>
    /// <param name="path">The file to delete.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
#pragma warning disable CA1031 // Best-effort cleanup of a temporary file must never mask the render outcome
        catch (Exception)
#pragma warning restore CA1031
        {
            // A leftover temp file is harmless; the OS reclaims it
        }
    }

    /// <summary>Reports a single page's render failure as a diagnostic.</summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="pageNumber">The 1-based page number that failed.</param>
    /// <param name="detail">A short description of what went wrong for this page.</param>
    private static void ReportPageFailure(IExtractionSink sink, int pageNumber, string? detail)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            VisioDiagnosticCodes.PageRenderFailed, DiagnosticSeverity.Warning,
            $"Page {page} could not be rendered ({detail ?? "unknown reason"})."));
    }

    /// <summary>Reports the counted gap summarizing every page that could not be rendered.</summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="failures">The 1-based page numbers that failed to render.</param>
    private static void ReportPageFailuresGap(IExtractionSink sink, IReadOnlyList<int> failures)
    {
        var pages = failures.Select(page => page.ToString(CultureInfo.InvariantCulture)).ToList();
        var count = failures.Count.ToString(CultureInfo.InvariantCulture);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Pages, "pages/", GapScope.PartiallyExtracted,
            $"{count} page(s) could not be rendered and were omitted from the pages folder.",
            Impact: "Rendered images for the named pages are not available; their logical topology is still present.",
            Remedy: "Check that the pages are well-formed; the remaining pages were rendered.",
            AffectedCount: failures.Count,
            AffectedItems: pages));
    }

    /// <summary>Creates the real Visio adapter, guarding the Windows-only type so it is never constructed off Windows.</summary>
    /// <returns>A new <see cref="VisioAutomation"/> adapter.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown off Windows, where availability probing has already excluded the backend.</exception>
    private static IVisioAutomation CreateDefaultAutomation()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Visio COM automation adapter runs only on Windows; availability probing selects this "
                + "backend only where Microsoft Visio is present.");
        }

        return new VisioAutomation();
    }

    /// <summary>Runs the availability self-test on Windows, skipping cleanly elsewhere.</summary>
    /// <param name="context">The self-test context.</param>
    /// <returns>The case result.</returns>
    private static SelfTestResult RunAvailable(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!OperatingSystem.IsWindows())
        {
            return SelfTestResult.Skipped("Microsoft Visio COM automation is available only on Windows.");
        }

        var probe = VisioComAvailability.Probe(ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages);
        return probe.IsAvailable
            ? SelfTestResult.Passed(TimeSpan.Zero)
            : SelfTestResult.Skipped(probe.UnavailableReason ?? "Microsoft Visio is not available.");
    }

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>Buffering makes a stream source and a file source behave identically. Read-only I/O.</remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
