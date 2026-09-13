using System.Globalization;
using DocDown.Core;
using DocDown.PowerPoint.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DocDown.PowerPoint.Com;

/// <summary>
///     The PowerPoint COM automation backend: a full superset extractor that produces slide text,
///     titles, speaker notes, and document metadata by delegating to the managed Open XML backend,
///     then rasterizes each slide to a PNG through Microsoft PowerPoint over late-bound COM.
/// </summary>
/// <remarks>
///     <para>
///         The engine selects exactly one backend, so the COM path cannot be a render-only add-on:
///         it must produce the same slide text, titles, speaker notes, and metadata as the managed
///         backend, then add the slide images the managed backend cannot. It is chosen over the
///         managed backend only when page rendering is actually requested; otherwise the managed
///         backend wins on priority and no COM is touched.
///     </para>
///     <para>
///         Rather than re-implement text and notes extraction, it constructs a
///         <see cref="PowerPointOpenXmlExtractor"/> and runs it against the same sink with rendering
///         suppressed, then adds the rendered slides the managed backend cannot. Everything apart
///         from talking to PowerPoint is exercised cross-platform by injecting a stub
///         <see cref="IPowerPointAutomation"/>; the real adapter is the single untestable COM
///         boundary, proven by release-time self-tests. Rendering is environment-dependent, so the
///         backend reports unavailable off Windows or without PowerPoint, and a slide that cannot be
///         rendered becomes a plain-language note while the run continues with the remaining slides.
///     </para>
/// </remarks>
public sealed class PowerPointComExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The factory that produces the PowerPoint automation session, or <see langword="null"/> when a test declares no adapter.</summary>
    private readonly Func<IPowerPointAutomation>? _automationFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointComExtractor"/> class backed by the
    ///     real PowerPoint COM adapter.
    /// </summary>
    /// <remarks>Constructing the extractor never launches PowerPoint; availability defers to the environment check.</remarks>
    public PowerPointComExtractor()
        : this(CreateDefaultAutomation)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointComExtractor"/> class with an
    ///     injected automation factory.
    /// </summary>
    /// <param name="automationFactory">
    ///     The factory that produces the PowerPoint automation session, or <see langword="null"/> to
    ///     declare that no adapter is present so the backend probes unavailable on every platform.
    /// </param>
    /// <remarks>Internal so tests can inject a stub and exercise the whole extraction path without Microsoft Office.</remarks>
    internal PowerPointComExtractor(Func<IPowerPointAutomation>? automationFactory)
    {
        _automationFactory = automationFactory;
    }

    /// <inheritdoc />
    public string Id => "powerpoint-com";

    /// <inheritdoc />
    public string DisplayName => "PowerPoint (COM automation)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Pptx];

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Cheap, side-effect free, and never launches PowerPoint. When a test supplies no factory the
    ///     backend cannot work here regardless of the machine, so the probe reports unavailable.
    ///     Otherwise it falls through to the operating-system and ProgID check.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() =>
        _automationFactory is null
            ? ExtractorAvailability.Unavailable(
                "The PowerPoint COM automation adapter is not available in this build.")
            : PowerPointComAvailability.Probe();

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Buffer the source once: the managed backend reads it for text and notes, and the renderer
        // reads a file, and a stream source is not re-readable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Delegate the managed aspects to the Open XML backend with rendering suppressed, so it writes
        // slide text, titles, speaker notes, and inventory without any contradictory rendering fact
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await new PowerPointOpenXmlExtractor()
            .ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);

        // Record the authoritative rendering fact, complementing the managed backend's own fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.PowerPoint", "pages.renderer", "Microsoft PowerPoint (COM automation)", Available: true));

        var factory = _automationFactory
            ?? throw new PowerPointExtractionException(
                "The PowerPoint COM automation adapter is not available in this build.");

        await RenderSlidesAsync(bytes, source, factory, sink, options, cancellationToken).ConfigureAwait(false);
        return baseOutcome;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes the cases that prove the real PowerPoint adapter works in its deployed
    ///     environment. Enumeration is cheap and environment-independent; the engine wraps every case
    ///     as skipped where the backend is unavailable so a machine without PowerPoint produces no
    ///     false failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("powerpoint.com.available", Id, RunAvailable)
    ];

    /// <summary>
    ///     Renders every slide to the sink, isolating each slide's faults.
    /// </summary>
    /// <param name="bytes">The buffered source deck bytes.</param>
    /// <param name="source">The document source, consulted for a file path.</param>
    /// <param name="factory">The automation factory.</param>
    /// <param name="sink">The sink to write rendered slides and notes through.</param>
    /// <param name="options">The effective options carrying the render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    ///     Renders every slide in one session; a slide that cannot be exported is recorded as a
    ///     plain-language note rather than aborting the extraction. Side effect: writes pages and
    ///     notes on the sink.
    /// </remarks>
    private static async ValueTask RenderSlidesAsync(
        byte[] bytes, DocumentSource source, Func<IPowerPointAutomation> factory,
        IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        // PowerPoint opens a file, so materialize the buffered bytes to a temporary path that works
        // whether the source was a file or a stream
        var (path, isTemporary) = MaterializePath(bytes, source);
        try
        {
            IReadOnlyList<PowerPointRenderedSlide> slides;
            using (var automation = factory())
            {
                slides = automation.Render(path, options.PageRenderDpi);
            }

            foreach (var slide in slides)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (slide.Png is { } png)
                {
                    using var pageStream = new MemoryStream(png, writable: false);
                    await sink.AddPageAsync(slide.SlideNumber, pageStream, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ReportSlideFailureNote(sink, slide.SlideNumber, slide.FailureReason);
                }
            }
        }
        finally
        {
            if (isTemporary)
            {
                TryDelete(path);
            }
        }
    }

    /// <summary>Materializes a path PowerPoint can open, preferring the source file and falling back to a temp copy.</summary>
    /// <param name="bytes">The buffered deck bytes.</param>
    /// <param name="source">The document source.</param>
    /// <returns>The path and whether it is a temporary file this backend must delete.</returns>
    private static (string Path, bool IsTemporary) MaterializePath(byte[] bytes, DocumentSource source)
    {
        if (source.Path is { } existing && File.Exists(existing))
        {
            return (existing, false);
        }

        var temp = Path.Combine(Path.GetTempPath(), "docdown-pptx-" + Guid.NewGuid().ToString("N") + ".pptx");
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

    /// <summary>Reports a single slide's render failure as a plain-language note.</summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="slideNumber">The 1-based slide number that failed.</param>
    /// <param name="detail">A short description of what went wrong for this slide.</param>
    /// <remarks>
    ///     A render attempt that did not complete is a fact about this extraction, not a judgment
    ///     about the presentation, so it is stated as a plain note with the slide number and the
    ///     renderer's detail when one is available. Side effect: records a note on the sink.
    /// </remarks>
    private static void ReportSlideFailureNote(IExtractionSink sink, int slideNumber, string? detail)
    {
        var slide = slideNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote(
            $"Slide {slide} could not be rendered ({detail ?? "unknown reason"})."));
    }

    /// <summary>Creates the real PowerPoint adapter, guarding the Windows-only type so it is never constructed off Windows.</summary>
    /// <returns>A new <see cref="PowerPointAutomation"/> adapter.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown off Windows, where availability probing has already excluded the backend.</exception>
    private static IPowerPointAutomation CreateDefaultAutomation()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The PowerPoint COM automation adapter runs only on Windows; availability probing selects this "
                + "backend only where Microsoft PowerPoint is present.");
        }

        return new PowerPointAutomation();
    }

    /// <summary>Runs the availability self-test on Windows, skipping cleanly elsewhere.</summary>
    /// <param name="context">The self-test context.</param>
    /// <returns>The case result.</returns>
    private static SelfTestResult RunAvailable(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!OperatingSystem.IsWindows())
        {
            return SelfTestResult.Skipped("Microsoft PowerPoint COM automation is available only on Windows.");
        }

        var probe = PowerPointComAvailability.Probe();
        return probe.IsAvailable
            ? SelfTestResult.Passed(TimeSpan.Zero)
            : SelfTestResult.Skipped(probe.UnavailableReason ?? "Microsoft PowerPoint is not available.");
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
