using System.Globalization;
using System.Runtime.Versioning;
using DocDown.Core;
using DocDown.Office.Com;
using DocDown.PowerPoint.OpenXml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using CoreFormat = DocDown.Core.DocumentFormat;
using P = DocumentFormat.OpenXml.Presentation;

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
    /// <summary>The embedded deck the render self-test rasterizes.</summary>
    /// <remarks>
    ///     A real .pptx authored in Microsoft PowerPoint. Using what PowerPoint itself wrote removed
    ///     the synthesizer that stood here, which had to construct a theme, color scheme, font
    ///     scheme, format scheme, slide master and layout before PowerPoint would open the deck at all.
    /// </remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Office.Resources.probe.pptx";

    /// <summary>The number of slides the embedded deck carries.</summary>
    /// <remarks>Stated here so the render self-test asserts the renderer returned an image for every slide rather than merely for one.</remarks>
    private const int ProbeSlideCount = 2;

    /// <summary>The resolution the render self-test asks for, chosen because it is the ordinary screen resolution and keeps the export quick.</summary>
    private const int SelfTestRenderDpi = 96;


    /// <summary>The smallest pixel dimension a genuinely rendered slide can plausibly have.</summary>
    /// <remarks>A slide rendered at any usable resolution is hundreds of pixels across, so anything smaller indicates a stub or truncated image rather than a real render.</remarks>
    private const int MinimumPlausiblePixels = 64;

    /// <summary>The largest pixel dimension a rendered self-test slide can plausibly have.</summary>
    /// <remarks>The self-test deck is a single 13.3-by-7.5-inch slide rendered at 96 DPI, so a dimension beyond this bound means the export did not describe that slide.</remarks>
    private const int MaximumPlausiblePixels = 20000;

    /// <summary>How long the render self-test waits for the released PowerPoint host to exit before calling it a leak.</summary>
    /// <remarks>Releasing the last reference asks the host to unwind, which is not instantaneous; a few seconds separates an ordinary teardown from an orphaned process.</remarks>
    private const int ProcessExitGraceMilliseconds = 10000;

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
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone(), "powerpoint.pageRendering");
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
    ///     environment: that PowerPoint can be reached at all, and that a deck actually rasterizes
    ///     through it end to end. Enumeration is cheap and environment-independent; the engine wraps
    ///     every case as skipped where the backend is unavailable so a machine without PowerPoint
    ///     produces no false failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("powerpoint.com.available", Id, RunAvailable),
        new SelfTestCase("powerpoint.com.render", Id, RunRender)
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

    /// <summary>Runs the end-to-end render self-test: writes the embedded probe deck out and rasterizes it through the real COM adapter.</summary>
    /// <param name="context">The self-test context supplying the work folder and cancellation.</param>
    /// <returns>The case result: passed when a genuine slide image came back, skipped where PowerPoint is absent, failed otherwise.</returns>
    /// <remarks>
    ///     The availability case proves only that PowerPoint can be activated; this case walks
    ///     through the door, which is the only way the COM boundary — activation, read-only open,
    ///     point-to-pixel conversion, PNG export, and session teardown — is exercised anywhere. It
    ///     writes the embedded probe deck into the work folder rather than synthesizing one, so the
    ///     case renders a deck PowerPoint itself authored, and drives
    ///     <see cref="PowerPointAutomation"/> unchanged so the render is bounded by the adapter's
    ///     existing watchdog and leaves no orphaned PowerPoint process. A machine without PowerPoint
    ///     reports a reasoned skip, never a failure, and every fault is reported as data rather than
    ///     thrown at the caller. Side effect: writes and deletes one temporary deck in the context's
    ///     work folder, and runs Microsoft PowerPoint.
    /// </remarks>
    private static SelfTestResult RunRender(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A render can only be attempted where the adapter can run at all; anywhere else the case
        // reports a reasoned skip naming the missing application rather than a false failure
        if (!OperatingSystem.IsWindows())
        {
            return SelfTestResult.Skipped(
                "Microsoft PowerPoint COM automation is available only on Windows, so a slide cannot be rendered here.");
        }

        var probe = PowerPointComAvailability.Probe();
        if (!probe.IsAvailable)
        {
            return SelfTestResult.Skipped(
                probe.UnavailableReason
                ?? "Microsoft PowerPoint is not available, so a slide cannot be rendered here.");
        }

        var started = DateTimeOffset.UtcNow;
        var path = Path.Combine(
            context.WorkFolder, "docdown-powerpoint-selftest-" + Guid.NewGuid().ToString("N") + ".pptx");
        try
        {
            File.WriteAllBytes(path, SelfTestProbe.Load(typeof(PowerPointComExtractor).Assembly, ProbeResourceName));
            return RenderAndInspect(path, started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"Microsoft PowerPoint could not render a slide in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
        finally
        {
            // The case owns this deck, so it removes it on every path rather than leaving it for the
            // work folder's own cleanup
            TryDelete(path);
        }
    }

    /// <summary>Renders the self-test deck through the real adapter and judges what came back.</summary>
    /// <param name="path">The absolute path of the probe deck to render.</param>
    /// <param name="started">When the case started, so the result carries a true duration.</param>
    /// <returns>The case result.</returns>
    /// <remarks>
    ///     Uses <see cref="PowerPointAutomation"/> exactly as extraction does, so the watchdog,
    ///     forced process termination, and deterministic teardown under test are the ones that ship
    ///     rather than a parallel copy. Windows-only, which the caller has already established.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static SelfTestResult RenderAndInspect(string path, DateTimeOffset started)
    {
        using var automation = new PowerPointAutomation();
        var slides = automation.Render(path, SelfTestRenderDpi);

        var failure = DescribeRenderShortfall(slides)
                      ?? DescribeProcessShortfall(automation.LastOwnedProcessId);
        return failure is null
            ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
            : SelfTestResult.Failed(failure, DateTimeOffset.UtcNow - started);
    }

    /// <summary>Describes what is wrong with the rendered slides, or reports nothing when a genuine image came back.</summary>
    /// <param name="slides">The slides the adapter returned.</param>
    /// <returns>A plain-language shortfall description, or <see langword="null"/> when the render is sound.</returns>
    /// <remarks>
    ///     The embedded deck's slide count is known, so the case can assert a real image came back for
    ///     every slide — the expected number of slides, non-empty PNG bytes with the PNG signature, and
    ///     dimensions in the plausible range — rather than merely that the call returned. Checking every
    ///     slide rather than the first also catches a renderer that silently drops one. Pure.
    /// </remarks>
    private static string? DescribeRenderShortfall(IReadOnlyList<PowerPointRenderedSlide> slides)
    {
        if (slides.Count != ProbeSlideCount)
        {
            return $"Microsoft PowerPoint returned {slides.Count.ToString(CultureInfo.InvariantCulture)} slides "
                   + $"for the embedded deck, which has {ProbeSlideCount.ToString(CultureInfo.InvariantCulture)}.";
        }

        foreach (var slide in slides)
        {
            var shortfall = DescribeSlideShortfall(slide);
            if (shortfall is not null)
            {
                return shortfall;
            }
        }

        return null;
    }

    /// <summary>
    ///     Describes what is wrong with one rendered slide, or <see langword="null"/> when it is sound.
    /// </summary>
    /// <param name="slide">The rendered slide to inspect.</param>
    /// <returns>A plain description of the shortfall, or <see langword="null"/>.</returns>
    /// <remarks>Structural checks only: that bytes came back, that they are a PNG, and that the dimensions are plausible. Pure.</remarks>
    private static string? DescribeSlideShortfall(PowerPointRenderedSlide slide)
    {
        if (slide.Png is not { Length: > 0 } png)
        {
            return "Microsoft PowerPoint rendered no image for the self-test slide "
                   + $"({slide.FailureReason ?? "no reason given"}).";
        }

        if (!IsPng(png))
        {
            return "Microsoft PowerPoint produced output for the self-test slide that is not a PNG image.";
        }

        var (width, height) = ReadPngDimensions(png);
        return IsPlausible(width) && IsPlausible(height)
            ? null
            : "Microsoft PowerPoint produced a PNG for the self-test slide whose dimensions are implausible "
              + $"({width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)} pixels).";
    }

    /// <summary>Describes a PowerPoint process the render left behind, or reports nothing when none remains.</summary>
    /// <param name="processId">The process id the adapter owned, or zero when it could not isolate one.</param>
    /// <returns>A plain-language shortfall description, or <see langword="null"/> when no process leaked.</returns>
    /// <remarks>
    ///     Rendering is only correct if the host it started is gone afterwards, because an orphaned
    ///     PowerPoint holds a license, a temporary profile, and memory on the machine that ran the
    ///     tool. A released host takes a moment to unwind, so the check waits a short grace period
    ///     before calling a still-running process a leak. Read-only inspection of the process table.
    /// </remarks>
    private static string? DescribeProcessShortfall(int processId)
    {
        if (processId <= 0)
        {
            // The adapter could not isolate a process it owned — for example another PowerPoint
            // started concurrently — so there is nothing this case can honestly assert about it
            return null;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.WaitForExit(ProcessExitGraceMilliseconds)
                ? null
                : "The Microsoft PowerPoint process the render started is still running "
                  + "after the session ended.";
        }
        catch (ArgumentException)
        {
            // The process id is no longer known to the operating system, which is exactly the
            // outcome this check is looking for
            return null;
        }
    }

    /// <summary>Tests whether a pixel dimension is in the range a genuinely rendered slide can occupy.</summary>
    /// <param name="pixels">The dimension to judge.</param>
    /// <returns><see langword="true"/> when the dimension is plausible; otherwise <see langword="false"/>.</returns>
    private static bool IsPlausible(int pixels) =>
        pixels is >= MinimumPlausiblePixels and <= MaximumPlausiblePixels;

    /// <summary>Tests whether a byte buffer begins with the PNG signature.</summary>
    /// <param name="bytes">The bytes to inspect.</param>
    /// <returns><see langword="true"/> when the buffer starts with the eight-byte PNG signature; otherwise <see langword="false"/>.</returns>
    /// <remarks>A structural check the self-test can make without decoding the image: enough to prove the export produced a PNG. Pure.</remarks>
    private static bool IsPng(byte[] bytes) =>
        bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
        && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    /// <summary>Reads the pixel dimensions from a PNG image header.</summary>
    /// <param name="bytes">A buffer already known to carry the PNG signature and a full IHDR chunk.</param>
    /// <returns>The image width and height in pixels.</returns>
    /// <remarks>The IHDR chunk follows the signature at a fixed offset and stores both dimensions as big-endian 32-bit integers. Pure.</remarks>
    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
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
