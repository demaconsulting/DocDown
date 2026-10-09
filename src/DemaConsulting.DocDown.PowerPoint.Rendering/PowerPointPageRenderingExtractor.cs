using System.Globalization;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.PowerPoint.OpenXml;
using CoreFormat = DemaConsulting.DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.PowerPoint.Rendering;

/// <summary>
///     The PowerPoint slide-rendering backend: a full superset extractor that produces text,
///     embedded images, and document metadata by delegating to the managed PowerPoint backend,
///     then rasterizes the requested slides to PNG images through the fully-managed
///     DemaConsulting.CanvasNet.Pptx rasterizer.
/// </summary>
/// <remarks>
///     <para>
///         This backend deliberately composes <see cref="PowerPointOpenXmlExtractor"/> rather than
///         re-implementing the managed PowerPoint path. It clones the caller's
///         <see cref="ExtractionOptions"/>, forces <see cref="ExtractionOptions.RenderPages"/> off
///         on the delegated copy, and runs the managed backend against the same sink so text,
///         embedded images, and document metadata are emitted exactly once before slide rendering
///         begins.
///     </para>
///     <para>
///         <see cref="ProbeAvailability"/> is unconditional: slide rendering through CanvasNet.Pptx
///         is a fully-managed capability with no native stack to probe for, so this backend always
///         reports itself available and states that it provides rendered pages here. This mirrors
///         <c>DemaConsulting.DocDown.Pdf.Rendering</c>'s own <c>PdfPageRenderingExtractor.ProbeAvailability</c>,
///         which is likewise unconditional for the same reason.
///     </para>
///     <para>
///         Rendering can still fail for an individual slide after extraction has started. Each
///         slide that cannot be rasterized is reported as a plain <see cref="ExtractionNote"/>
///         naming the slide, the remaining slides continue rendering, and normal completion still
///         returns <see cref="ExtractionOutcome.Produced"/>. Instances hold no per-extraction state
///         and are safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class PowerPointPageRenderingExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The embedded PowerPoint presentation the self-test reads.</summary>
    /// <remarks>A real file authored in Microsoft PowerPoint, so the rasterizer is given a slide a real producer wrote.</remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.PowerPoint.Rendering.Resources.probe.pptx";

    /// <summary>The per-slide rasterization function this backend drives.</summary>
    /// <remarks>
    ///     Defaults to <see cref="SlideRenderer.Render"/>. Held as a delegate so a test can
    ///     substitute a function that faults on a chosen slide, which is the only reliable way to
    ///     exercise the per-slide fault-isolation path without depending on the rasterizer failing
    ///     on cue. The delegate carries no CanvasNet type, so it does not widen this backend's
    ///     surface.
    /// </remarks>
    private readonly Func<byte[], int, int, byte[]> _render;

    /// <summary>
    ///     The slide-count function this backend reads its slide selection from.
    /// </summary>
    /// <remarks>
    ///     Defaults to <see cref="SlideRenderer.GetSlideCount"/>, so counting goes through the same
    ///     rasterization seam as rendering. Held as a delegate for the same reason as
    ///     <see cref="_render"/>: a test can substitute a function that faults, which is the only
    ///     reliable way to exercise the count-failure path.
    /// </remarks>
    private readonly Func<byte[], int> _pageCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointPageRenderingExtractor"/> class
    ///     that rasterizes through the real CanvasNet.Pptx-backed <see cref="SlideRenderer"/>.
    /// </summary>
    /// <remarks>The production constructor; the parameterless shape is what the registration seam calls.</remarks>
    public PowerPointPageRenderingExtractor()
        : this(SlideRenderer.Render, SlideRenderer.GetSlideCount)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointPageRenderingExtractor"/> class
    ///     with a supplied rasterization function.
    /// </summary>
    /// <param name="render">
    ///     The function that renders one slide (source bytes, zero-based slide index, DPI) to PNG
    ///     bytes. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Internal so tests can inject a faulting renderer to prove per-slide failures become
    ///     recorded notes; production always flows through the parameterless constructor.
    /// </remarks>
    internal PowerPointPageRenderingExtractor(Func<byte[], int, int, byte[]> render)
        : this(render, SlideRenderer.GetSlideCount)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointPageRenderingExtractor"/> class
    ///     with a supplied rasterization function and slide-count function.
    /// </summary>
    /// <param name="render">
    ///     The function that renders one slide (source bytes, zero-based slide index, DPI) to PNG
    ///     bytes. Must not be null.
    /// </param>
    /// <param name="pageCount">
    ///     The function that reports how many slides the source presentation has. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="render"/> or <paramref name="pageCount"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Internal so tests can inject a faulting counter to prove a count failure becomes a
    ///     recorded note rather than an unreadable extraction.
    /// </remarks>
    internal PowerPointPageRenderingExtractor(Func<byte[], int, int, byte[]> render, Func<byte[], int> pageCount)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(pageCount);
        _render = render;
        _pageCount = pageCount;
    }

    /// <inheritdoc />
    public string Id => "powerpoint-rendering";

    /// <inheritdoc />
    public string DisplayName => "PowerPoint slides (CanvasNet.Pptx)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Pptx];

    /// <inheritdoc />
    /// <remarks>
    ///     Ranks below <c>PowerPointOpenXmlExtractor</c>'s priority of 10, so the managed Open XML
    ///     backend still wins when page rendering was not requested. When rendering is requested,
    ///     <see cref="ExtractorSelector"/> first filters candidates to those that provide rendered
    ///     pages; this is the only such backend for <see cref="CoreFormat.Pptx"/>, so it always wins
    ///     once rendering is requested regardless of this priority value.
    /// </remarks>
    public int Priority => 5;

    /// <inheritdoc />
    /// <remarks>
    ///     Unconditional: slide rendering through CanvasNet.Pptx is a fully-managed capability with
    ///     no native stack whose availability could vary by environment, so this backend always
    ///     reports itself available and states that rendered pages are provided here.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available(providesRenderedPages: true);

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Buffer the source once: the managed backend reads it for text/images/metadata and the
        // rasterizer reads it again for rasterization, and a stream source is not re-readable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Delegate the managed aspects to the base backend, with rendering suppressed so it writes
        // content, images, metadata, and its own notes without attempting slide output itself. A
        // parser fault (malformed, truncated) propagates from here to Core unchanged, which
        // converts it into a structured failure that still writes the full layout.
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await new PowerPointOpenXmlExtractor()
            .ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);
        if (baseOutcome == ExtractionOutcome.Unreadable)
        {
            return baseOutcome;
        }

        // Record the authoritative rendering fact under a distinct key, complementing (not
        // contradicting) the base backend's own powerpoint.pageRendering fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DemaConsulting.DocDown.PowerPoint", "pages.renderer", "CanvasNet.Pptx (managed)", Available: true));

        // Rasterize the requested slides; per-slide faults become notes rather than exceptions
        // that abort the extraction. Skipped when rendering was not requested, so a host that
        // registers only this backend (without the managed backend) still gets plain extraction
        // at no rasterization cost - selection may hand this backend a non-rendering request when
        // it is the sole candidate for the format.
        if (options.RenderPages)
        {
            await RenderSlidesAsync(bytes, sink, options, cancellationToken).ConfigureAwait(false);
        }

        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes one case that proves the rasterizer genuinely functions in this deployment:
    ///     it renders a slide of the embedded presentation to a PNG. Rendering is always available,
    ///     so this case reports a pass or a fault as data; there is no unavailable-stack skip path
    ///     to report.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("powerpoint-rendering.renderRoundTrip", Id, RunRenderRoundTrip)
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     Both consumers of the document — the managed backend and the rasterizer — need to
    ///     read it from the start, and a stream-backed source is read-once and possibly non-seekable.
    ///     Buffering once makes both reads identical rather than making a stream source fail late.
    ///     Read-only I/O.
    /// </remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Rasterizes the requested slides to the sink, isolating each slide's faults.
    /// </summary>
    /// <param name="bytes">The buffered source presentation bytes.</param>
    /// <param name="sink">The sink to write rendered slides and notes through.</param>
    /// <param name="options">The effective options carrying the page range and render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    ///     Renders each selected slide independently so one unrenderable slide records a note
    ///     rather than aborting the whole extraction. Cancellation propagates; every other fault is
    ///     caught per slide and recorded as a note naming that slide. When no slide was selected to
    ///     render — an empty deck or a page range matching nothing — this backend records that
    ///     outcome itself rather than relying on Core to infer it, because Core cannot tell this
    ///     backend's own note apart from an unrelated one the delegated base backend may have
    ///     already recorded (for example an embedded-image fault). Side effect: writes pages and
    ///     notes on the sink.
    /// </remarks>
    private async ValueTask RenderSlidesAsync(
        byte[] bytes, IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        IReadOnlyList<int> pageNumbers;
        try
        {
            pageNumbers = SelectPageNumbers(bytes, options.Pages);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a caller decision, not a count fault; let it propagate
            throw;
        }
#pragma warning disable CA1031 // A count fault costs the slides, not the extraction: the delegated content is already written
        catch (Exception)
#pragma warning restore CA1031
        {
            // The managed delegate already read this document, so its text, images and metadata
            // stand. Only the rendered slides are lost, and that is reported as a plain note
            // rather than collapsing an otherwise good extraction into an unreadable one.
            sink.ReportNote(new ExtractionNote("Slides could not be counted, so no slide images were rendered."));
            return;
        }

        if (pageNumbers.Count == 0)
        {
            // No slide was selected to render (an empty deck, or a page range matching nothing):
            // state that plainly ourselves, since Core cannot distinguish this from a note the
            // delegated base backend already recorded for an unrelated reason.
            sink.ReportNote(new ExtractionNote(
                "Page rendering was requested and a renderer was available, but no pages were produced."));
            return;
        }

        foreach (var pageNumber in pageNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] png;
            try
            {
                png = _render(bytes, pageNumber - 1, options.PageRenderDpi);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is a caller decision, not a render fault; let it propagate
                throw;
            }
            catch (InvalidDataException)
            {
                ReportPageFailure(sink, pageNumber);
                continue;
            }
            catch (ArgumentOutOfRangeException)
            {
                ReportPageFailure(sink, pageNumber);
                continue;
            }
            catch (OutOfMemoryException)
            {
                ReportPageFailure(sink, pageNumber);
                continue;
            }
#pragma warning disable CA1031 // Per-slide fault isolation: any render fault becomes a note, never an exception to the caller
            catch (Exception)
#pragma warning restore CA1031
            {
                ReportPageFailure(sink, pageNumber);
                continue;
            }

            using var pageStream = new MemoryStream(png, writable: false);
            await sink.AddPageAsync(pageNumber, pageStream, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Selects the 1-based slide numbers to render, honoring a requested page range.
    /// </summary>
    /// <param name="bytes">The buffered source presentation bytes.</param>
    /// <param name="range">The requested inclusive page range, or <see langword="null"/> for the whole document.</param>
    /// <returns>The selected slide numbers in document order; empty when the document has no slides.</returns>
    /// <remarks>
    ///     Reads the slide count through <see cref="SlideRenderer"/> — the package's single
    ///     rasterization seam, and the same component that will rasterize the slides — so the
    ///     selection cannot disagree with what the renderer can actually reach. Counting with a
    ///     second parser would both parse the document twice and risk a mismatch between the two.
    ///     Slides outside the document are silently absent from the selection rather than an error,
    ///     matching the managed backend's page-range behavior. Read-only over the buffered bytes.
    /// </remarks>
    private IReadOnlyList<int> SelectPageNumbers(byte[] bytes, PageRange? range)
    {
        var pageCount = _pageCount(bytes);
        var numbers = new List<int>(pageCount);
        for (var number = 1; number <= pageCount; number++)
        {
            if (range is { } selected && !selected.Contains(number))
            {
                continue;
            }

            numbers.Add(number);
        }

        return numbers;
    }

    /// <summary>
    ///     Reports a single slide's render failure as a plain note.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="pageNumber">The 1-based slide number that failed.</param>
    /// <remarks>
    ///     Keeps the report aligned with DocDown's reduced output model: the note states only the
    ///     extraction fact that this slide could not be rasterized, without assigning a code,
    ///     severity, remedy, or impact. Side effect: records on the sink.
    /// </remarks>
    private static void ReportPageFailure(IExtractionSink sink, int pageNumber)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote($"Slide {page} could not be rasterized."));
    }

    /// <summary>
    ///     Runs the render round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Rasterizes a slide of the embedded presentation, which proves the rasterizer is
    ///     genuinely functional here, not merely referenceable. Rendering is always available, so
    ///     this case always runs; it reports every fault as data rather than throwing.
    /// </remarks>
    private static SelfTestResult RunRenderRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var started = DateTimeOffset.UtcNow;
        try
        {
            var bytes = SelfTestProbe.Load(typeof(PowerPointPageRenderingExtractor).Assembly, ProbeResourceName);
            var png = SlideRenderer.Render(bytes, 0, 96);
            return IsValidPng(png)
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The slide renderer produced output that is not a valid PNG for the embedded document.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The slide renderer could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Tests whether a byte buffer begins with the PNG signature.
    /// </summary>
    /// <param name="bytes">The bytes to inspect.</param>
    /// <returns><see langword="true"/> when the buffer starts with the eight-byte PNG signature; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     A structural check the self-test can make without decoding the image: enough to prove the
    ///     encoder produced a PNG, not the pixels it contains. Pure.
    /// </remarks>
    private static bool IsValidPng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
        && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
}

/// <summary>
///     A private <see cref="IExtractionContext"/> that reuses an outer context but substitutes a
///     modified options object for the delegated managed extraction.
/// </summary>
/// <remarks>
///     Core keeps its own <see cref="IExtractionContext"/> implementation internal, so this backend
///     supplies its own to run the managed backend against the same sink, format, environment, and
///     cancellation token while overriding the options — specifically to force
///     <see cref="ExtractionOptions.RenderPages"/> off so the delegate leaves page output to this
///     backend. Immutable after construction and safe to read from the extraction thread.
/// </remarks>
internal sealed class DelegatedExtractionContext : IExtractionContext
{
    /// <summary>The outer context whose sink, format, environment, and token are reused.</summary>
    /// <remarks>Everything except the options passes straight through to the delegated backend.</remarks>
    private readonly IExtractionContext _inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegatedExtractionContext"/> class.
    /// </summary>
    /// <param name="inner">The outer context to reuse. Must not be null.</param>
    /// <param name="options">The already-cloned options for the delegated extraction. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="options"/> is null.</exception>
    /// <remarks>Captures the substitute options so the caller can adjust them before delegating.</remarks>
    public DelegatedExtractionContext(IExtractionContext inner, ExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        _inner = inner;
        Options = options;
    }

    /// <inheritdoc />
    public ExtractionOptions Options { get; }

    /// <inheritdoc />
    public IExtractionSink Sink => _inner.Sink;

    /// <inheritdoc />
    public FormatDetection DetectedFormat => _inner.DetectedFormat;

    /// <inheritdoc />
    public ExtractorDescriptor SelectedExtractor => _inner.SelectedExtractor;

    /// <inheritdoc />
    public ExtractionEnvironment Environment => _inner.Environment;

    /// <inheritdoc />
    public CancellationToken CancellationToken => _inner.CancellationToken;
}
