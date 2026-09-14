using System.Globalization;
using System.Runtime.Versioning;
using DocDown.Core;
using DocDown.Extraction;
using DocDown.Visio.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DocDown.Visio.Com;

/// <summary>
///     The Visio COM automation backend: a composing extractor that produces page names, shape text,
///     and the directed connector topology by delegating to the managed Open Packaging backend, then
///     rasterizes each page to a PNG through Microsoft Visio over late-bound COM.
/// </summary>
/// <remarks>
///     <para>
///         The engine selects exactly one backend. The managed extractor remains the ordinary default
///         because it carries the higher priority for non-rendering work, while this extractor becomes
///         relevant only when page rendering is requested and its availability probe can honestly
///         report rendered pages in the current environment. That keeps ordinary text-and-topology
///         extraction deterministic and avoids touching COM unless a caller asked for rendered pages.
///     </para>
///     <para>
///         Rather than re-implement the topology extraction, it constructs a
///         <see cref="VisioOpenXmlExtractor"/> and runs it against the same sink with rendering
///         suppressed, then adds the rendered pages the managed backend cannot. Everything apart from
///         talking to Visio is exercised cross-platform by injecting a stub
///         <see cref="IVisioAutomation"/>; the real adapter is the single untestable COM boundary. A
///         page that cannot be rendered becomes a plain note while the run continues with the
///         remaining pages, and on any host the logical topology is still delivered by the delegated
///         managed backend.
///     </para>
/// </remarks>
public sealed class VisioComExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The resolution the render self-test asks for, chosen because it is the ordinary screen resolution and keeps the export quick.</summary>
    private const int SelfTestRenderDpi = 96;

    /// <summary>The smallest pixel dimension a genuinely rendered page can plausibly have.</summary>
    /// <remarks>Visio crops the export to the drawing extent, and the self-test drawing spans several inches, so anything smaller indicates a stub or truncated image rather than a real render.</remarks>
    private const int MinimumPlausiblePixels = 32;

    /// <summary>The largest pixel dimension a rendered self-test page can plausibly have.</summary>
    /// <remarks>The self-test drawing is a letter-sized page rendered at 96 DPI, so a dimension beyond this bound means the export did not describe that page.</remarks>
    private const int MaximumPlausiblePixels = 20000;

    /// <summary>How long the render self-test waits for the released Visio host to exit before calling it a leak.</summary>
    /// <remarks>Releasing the last reference asks the host to unwind, which is not instantaneous; a few seconds separates an ordinary teardown from an orphaned process.</remarks>
    private const int ProcessExitGraceMilliseconds = 10000;

    /// <summary>The embedded Visio drawing the render self-test rasterizes.</summary>
    /// <remarks>
    ///     A real .vsdx authored in Microsoft Visio. Using what Visio itself wrote is what removed the
    ///     synthesizer that used to stand here: Visio refuses to open a package lacking a window part
    ///     and draws nothing for a shape with no geometry, so a hand-built package had to reproduce
    ///     both faithfully or report a working environment as broken.
    /// </remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Visio.Resources.probe.vsdx";

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
            : VisioComAvailability.Probe();

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
        // writes page names, shape text, and the directed topology but does not attempt page rendering
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        await new VisioOpenXmlExtractor().ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);

        // Record the authoritative rendering fact, complementing the managed backend's own fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Visio", "pages.renderer", "Microsoft Visio (COM automation)", Available: true));

        var factory = _automationFactory
            ?? throw new VisioExtractionException("The Visio COM automation adapter is not available in this build.");

        await RenderPagesAsync(bytes, source, factory, sink, options, cancellationToken).ConfigureAwait(false);
        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes the cases that prove the real Visio adapter works in its deployed environment:
    ///     that Visio can be reached at all, and that a drawing actually rasterizes through it end to
    ///     end. Enumeration is cheap and environment-independent; the engine wraps every case as
    ///     skipped where the backend is unavailable so a machine without Visio produces no false
    ///     failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("visio.com.available", Id, RunAvailable),
        new SelfTestCase("visio.com.render", Id, RunRender)
    ];

    /// <summary>
    ///     Renders every foreground page to the sink, isolating each page's faults.
    /// </summary>
    /// <param name="bytes">The buffered source drawing bytes.</param>
    /// <param name="source">The document source, consulted for a file path.</param>
    /// <param name="factory">The automation factory.</param>
    /// <param name="sink">The sink to write rendered pages and notes through.</param>
    /// <param name="options">The effective options carrying the render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    ///     Renders every page in one session; a page that cannot be exported becomes a plain note
    ///     while the run continues.
    /// </remarks>
    private static async ValueTask RenderPagesAsync(
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

    /// <summary>Reports a single page's render failure as a note.</summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="pageNumber">The 1-based page number that failed.</param>
    /// <param name="detail">A short description of what went wrong for this page.</param>
    private static void ReportPageFailure(IExtractionSink sink, int pageNumber, string? detail)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote(
            $"Page {page} could not be rendered ({detail ?? "unknown reason"})."));
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

        var probe = VisioComAvailability.Probe();
        return probe.IsAvailable
            ? SelfTestResult.Passed(TimeSpan.Zero)
            : SelfTestResult.Skipped(probe.UnavailableReason ?? "Microsoft Visio is not available.");
    }

    /// <summary>Runs the end-to-end render self-test: builds a drawing and rasterizes it through the real COM adapter.</summary>
    /// <param name="context">The self-test context supplying the work folder and cancellation.</param>
    /// <returns>The case result: passed when a genuine page image came back, skipped where Visio is absent, failed otherwise.</returns>
    /// <remarks>
    ///     The availability case proves only that Visio can be activated; this case walks through the
    ///     door, which is the only way the COM boundary — activation, read-only open, export
    ///     resolution, PNG export, and session teardown — is exercised anywhere. It builds its own
    ///     synthetic single-page drawing rather than shipping a fixture, so the case carries no
    ///     document content of its own, and drives <see cref="VisioAutomation"/> unchanged so the
    ///     render is bounded by the adapter's existing watchdog and leaves no orphaned Visio process.
    ///     A machine without Visio reports a reasoned skip, never a failure, and every fault is
    ///     reported as data rather than thrown at the caller. Side effect: writes and deletes one
    ///     temporary drawing in the context's work folder, and runs Microsoft Visio.
    /// </remarks>
    private static SelfTestResult RunRender(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A render can only be attempted where the adapter can run at all; anywhere else the case
        // reports a reasoned skip naming the missing application rather than a false failure
        if (!OperatingSystem.IsWindows())
        {
            return SelfTestResult.Skipped(
                "Microsoft Visio COM automation is available only on Windows, so a page cannot be rendered here.");
        }

        var probe = VisioComAvailability.Probe();
        if (!probe.IsAvailable)
        {
            return SelfTestResult.Skipped(
                probe.UnavailableReason ?? "Microsoft Visio is not available, so a page cannot be rendered here.");
        }

        var started = DateTimeOffset.UtcNow;
        var path = Path.Combine(
            context.WorkFolder, "docdown-visio-selftest-" + Guid.NewGuid().ToString("N") + ".vsdx");
        try
        {
            File.WriteAllBytes(path, SelfTestProbe.Load(typeof(VisioComExtractor).Assembly, ProbeResourceName));
            return RenderAndInspect(path, started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"Microsoft Visio could not render a page in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
        finally
        {
            // The case owns this drawing, so it removes it on every path rather than leaving it for
            // the work folder's own cleanup
            TryDelete(path);
        }
    }

    /// <summary>Renders the self-test drawing through the real adapter and judges what came back.</summary>
    /// <param name="path">The absolute path of the synthetic drawing to render.</param>
    /// <param name="started">When the case started, so the result carries a true duration.</param>
    /// <returns>The case result.</returns>
    /// <remarks>
    ///     Uses <see cref="VisioAutomation"/> exactly as extraction does, so the watchdog, forced
    ///     process termination, and deterministic teardown under test are the ones that ship rather
    ///     than a parallel copy. Windows-only, which the caller has already established.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static SelfTestResult RenderAndInspect(string path, DateTimeOffset started)
    {
        using var automation = new VisioAutomation();
        var pages = automation.Render(path, SelfTestRenderDpi);

        var failure = DescribeRenderShortfall(pages)
                      ?? DescribeProcessShortfall(automation.LastOwnedProcessId);
        return failure is null
            ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
            : SelfTestResult.Failed(failure, DateTimeOffset.UtcNow - started);
    }

    /// <summary>Describes what is wrong with the rendered pages, or reports nothing when a genuine image came back.</summary>
    /// <param name="pages">The pages the adapter returned.</param>
    /// <returns>A plain-language shortfall description, or <see langword="null"/> when the render is sound.</returns>
    /// <remarks>
    ///     The drawing has exactly one foreground page whose extent is known, so the case can assert
    ///     a real image came back — one page, non-empty PNG bytes with the PNG signature, and
    ///     dimensions in the plausible range — rather than merely that the call returned. Pure.
    /// </remarks>
    private static string? DescribeRenderShortfall(IReadOnlyList<VisioRenderedPage> pages)
    {
        if (pages.Count != 1)
        {
            return $"Microsoft Visio returned {pages.Count.ToString(CultureInfo.InvariantCulture)} pages "
                   + "for a drawing the self-test built with exactly one.";
        }

        var page = pages[0];
        if (page.Png is not { Length: > 0 } png)
        {
            return "Microsoft Visio rendered no image for the self-test page "
                   + $"({page.FailureReason ?? "no reason given"}).";
        }

        if (!IsPng(png))
        {
            return "Microsoft Visio produced output for the self-test page that is not a PNG image.";
        }

        var (width, height) = ReadPngDimensions(png);
        return IsPlausible(width) && IsPlausible(height)
            ? null
            : "Microsoft Visio produced a PNG for the self-test page whose dimensions are implausible "
              + $"({width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)} pixels).";
    }

    /// <summary>Describes a Visio process the render left behind, or reports nothing when none remains.</summary>
    /// <param name="processId">The process id the adapter owned, or zero when it could not isolate one.</param>
    /// <returns>A plain-language shortfall description, or <see langword="null"/> when no process leaked.</returns>
    /// <remarks>
    ///     Rendering is only correct if the host it started is gone afterwards, because an orphaned
    ///     Visio holds a license, a temporary profile, and memory on the machine that ran the tool. A
    ///     released host takes a moment to unwind, so the check waits a short grace period before
    ///     calling a still-running process a leak. Read-only inspection of the process table.
    /// </remarks>
    private static string? DescribeProcessShortfall(int processId)
    {
        if (processId <= 0)
        {
            // The adapter could not isolate a process it owned — for example another Visio started
            // concurrently — so there is nothing this case can honestly assert about it
            return null;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.WaitForExit(ProcessExitGraceMilliseconds)
                ? null
                : "The Microsoft Visio process the render started is still running after the session ended.";
        }
        catch (ArgumentException)
        {
            // The process id is no longer known to the operating system, which is exactly the
            // outcome this check is looking for
            return null;
        }
    }

    /// <summary>Tests whether a pixel dimension is in the range a genuinely rendered page can occupy.</summary>
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
