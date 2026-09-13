using System.Globalization;
using DocDown.Core;
using UglyToad.PdfPig;

namespace DocDown.Pdf.Rendering;

/// <summary>
///     The PDF page-rendering backend: a full superset extractor that produces text, embedded
///     images, and document metadata by delegating to the managed PDF backend, then rasterizes the
///     requested pages to PNG images through PDFium.
/// </summary>
/// <remarks>
///     <para>
///         The engine selects exactly one backend for an extraction, so a rendering backend that
///         advertised only <see cref="ExtractorCapabilities.RenderedPages"/> would lose selection to
///         the managed PDF backend (which satisfies more of the request) and never render anything.
///         This extractor therefore declares the full set —
///         <see cref="ExtractorCapabilities.Text"/>, <see cref="ExtractorCapabilities.EmbeddedImages"/>,
///         <see cref="ExtractorCapabilities.DocumentMetadata"/>, and
///         <see cref="ExtractorCapabilities.RenderedPages"/> — and delivers all four. It is chosen
///         over the base backend only when page rendering is actually requested; otherwise the
///         lighter managed backend wins on the identifier tie-break and no native code is touched.
///     </para>
///     <para>
///         Rather than re-implement text, image, and metadata extraction, it constructs a
///         <see cref="PdfDocumentExtractor"/> and runs it against the same sink with a cloned options
///         object whose <see cref="ExtractionOptions.RenderPages"/> is forced off. That reuse is
///         exact — the managed backend writes the content, the images, the document info, and its own
///         structural gaps — and, because rendering is suppressed on the delegate, the base backend
///         does not emit its own "rendering unavailable" gap. This backend then adds the pages the
///         base one cannot. The one honest artifact of the delegation is that the base backend's
///         <c>pdf.pageRendering = not provided by this extractor</c> environment fact still appears;
///         it is literally true of the managed inner backend and is complemented, not contradicted,
///         by this backend's own <c>pages.renderer</c> fact.
///     </para>
///     <para>
///         Rendering is environment-dependent, so honesty is enforced at every failure point.
///         <see cref="ProbeAvailability"/> reports the backend unavailable, with a reason, when the
///         PDFium native cannot load — which lets selection degrade through the engine's unchanged
///         <c>DD0301</c> path exactly as if this package were not registered. A page that cannot be
///         rasterized — an unsupported page, an out-of-memory at a high DPI on a very large page, or
///         a native fault mid-run — becomes a counted, reason-bearing gap naming the page, and the
///         run continues with the remaining pages; no exception reaches the caller and the
///         <c>pages/</c> folder is never silently empty. Instances hold no per-extraction state and
///         are safe to register once and reuse; concurrent renders are serialized by
///         <see cref="PageRenderer"/>.
///     </para>
/// </remarks>
public sealed class PdfPageRenderingExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The per-page rasterization function this backend drives.</summary>
    /// <remarks>
    ///     Defaults to <see cref="PageRenderer.Render"/>. Held as a delegate so a test can substitute
    ///     a function that faults on a chosen page, which is the only reliable way to exercise the
    ///     per-page fault-isolation path without depending on the native renderer failing on cue.
    ///     The delegate carries no PDFtoImage type, so it does not widen this backend's surface.
    /// </remarks>
    private readonly Func<byte[], int, int, byte[]> _render;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfPageRenderingExtractor"/> class that
    ///     rasterizes through the real PDFium-backed <see cref="PageRenderer"/>.
    /// </summary>
    /// <remarks>The production constructor; the parameterless shape is what the registration seam calls.</remarks>
    public PdfPageRenderingExtractor()
        : this(PageRenderer.Render)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfPageRenderingExtractor"/> class with a
    ///     supplied rasterization function.
    /// </summary>
    /// <param name="render">
    ///     The function that renders one page (source bytes, zero-based page index, DPI) to PNG
    ///     bytes. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Internal so tests can inject a faulting renderer to prove per-page failures become counted
    ///     gaps; production always flows through the parameterless constructor.
    /// </remarks>
    internal PdfPageRenderingExtractor(Func<byte[], int, int, byte[]> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        _render = render;
    }

    /// <inheritdoc />
    public string Id => "pdf-rendering";

    /// <inheritdoc />
    public string DisplayName => "PDF pages (PDFtoImage/PDFium)";

    /// <inheritdoc />
    public IReadOnlyCollection<DocumentFormat> SupportedFormats => [DocumentFormat.Pdf];

    /// <inheritdoc />
    /// <remarks>
    ///     The full superset. The rendered-pages capability is the reason this backend exists; the
    ///     other three are declared because a single-backend selection would otherwise pass this
    ///     backend over, and because it genuinely delivers them by delegating to the managed backend.
    /// </remarks>
    public ExtractorCapabilities Capabilities =>
        ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages
        | ExtractorCapabilities.DocumentMetadata | ExtractorCapabilities.RenderedPages;

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Cheap and non-throwing: it asks <see cref="PageRenderer"/> whether the PDFium native can
    ///     load for the current runtime identifier (a one-time, cached, side-effect-light check) and
    ///     never rasterizes. When the native stack is present the full capability set is effective;
    ///     when it is absent the backend reports unavailable with a reason, so selection can degrade
    ///     honestly rather than fail at render time.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability()
    {
        var probe = PageRenderer.ProbeAvailability();
        return probe.IsAvailable
            ? ExtractorAvailability.Available(Capabilities)
            : ExtractorAvailability.Unavailable(
                $"PDF page rendering is unavailable: {probe.Reason}.");
    }

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Buffer the source once: the managed backend reads it for text/images/metadata and the
        // native renderer reads it again for rasterization, and a stream source is not re-readable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Delegate the managed aspects to the base backend, with rendering suppressed so it writes
        // content, images, metadata, and its structural gaps but not its "rendering unavailable" gap.
        // A parser fault (encrypted, malformed, truncated) propagates from here to Core unchanged,
        // which converts it into a structured failure that still writes the full layout.
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await new PdfDocumentExtractor()
            .ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);

        // Record the authoritative rendering fact under a distinct key, complementing (not
        // contradicting) the base backend's own pdf.pageRendering fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Pdf.Rendering", "pages.renderer", "PDFtoImage (PDFium/SkiaSharp, native)", Available: true));

        // Rasterize the requested pages; per-page faults become counted gaps, not exceptions
        var pagesDegraded = await RenderPagesAsync(bytes, sink, options, cancellationToken).ConfigureAwait(false);

        return baseOutcome == ExtractionOutcome.Degraded || pagesDegraded
            ? ExtractionOutcome.Degraded
            : baseOutcome;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes one case that proves the native stack genuinely rasterizes in this
    ///     deployment: it builds a one-page PDF and renders it to a PNG. Where the native binary is
    ///     absent the case reports a reasoned skip rather than a failure, because an unavailable
    ///     capability must not be recorded as a broken one. The engine also wraps this backend's
    ///     cases to skip when <see cref="ProbeAvailability"/> reports it unavailable, so the skip is
    ///     honest whether observed here or upstream.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("pdf-rendering.renderRoundTrip", Id, RunRenderRoundTrip)
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     Both consumers of the document — the managed backend and the native renderer — need to
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
    ///     Rasterizes the requested pages to the sink, isolating each page's faults.
    /// </summary>
    /// <param name="bytes">The buffered source PDF bytes.</param>
    /// <param name="sink">The sink to write rendered pages and gaps through.</param>
    /// <param name="options">The effective options carrying the page range and render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when any page failed to render; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Renders each selected page independently so one unrenderable page degrades the run with a
    ///     counted gap rather than aborting it. Cancellation propagates; every other fault is caught
    ///     per page and turned into a diagnostic and a gap. Side effect: writes pages and gaps on the
    ///     sink.
    /// </remarks>
    private async ValueTask<bool> RenderPagesAsync(
        byte[] bytes, IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        var pageNumbers = SelectPageNumbers(bytes, options.Pages);
        sink.ReportFound(GapKind.Pages, pageNumbers.Count);

        var failures = new List<int>();
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
            catch (OutOfMemoryException)
            {
                ReportPageFailure(
                    sink, PdfRenderingDiagnosticCodes.PageTooLarge, pageNumber,
                    "ran out of memory rasterizing the page at the requested DPI");
                failures.Add(pageNumber);
                continue;
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or BadImageFormatException or System.Runtime.InteropServices.SEHException)
            {
                // A native-level fault raised mid-run by the rasterizer; isolate it like any other
                ReportPageFailure(
                    sink, PdfRenderingDiagnosticCodes.NativeFault, pageNumber,
                    $"could not be rasterized because the native renderer faulted ({exception.Message})");
                failures.Add(pageNumber);
                continue;
            }
#pragma warning disable CA1031 // Per-page fault isolation: any render fault becomes a counted gap, never an exception to the caller
            catch (Exception exception)
#pragma warning restore CA1031
            {
                ReportPageFailure(
                    sink, PdfRenderingDiagnosticCodes.PageRenderFailed, pageNumber,
                    $"could not be rasterized ({exception.Message})");
                failures.Add(pageNumber);
                continue;
            }

            using var pageStream = new MemoryStream(png, writable: false);
            await sink.AddPageAsync(pageNumber, pageStream, cancellationToken).ConfigureAwait(false);
        }

        if (failures.Count > 0)
        {
            ReportPageFailuresGap(sink, failures);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Selects the 1-based page numbers to render, honoring a requested page range.
    /// </summary>
    /// <param name="bytes">The buffered source PDF bytes.</param>
    /// <param name="range">The requested inclusive page range, or <see langword="null"/> for the whole document.</param>
    /// <returns>The selected page numbers in document order; empty when the document has no pages.</returns>
    /// <remarks>
    ///     Opens the document with the same managed parser the delegated backend uses, so the pages
    ///     rendered here are exactly the pages the base backend extracted text and images from.
    ///     Pages outside the document are silently absent from the selection rather than an error,
    ///     matching the managed backend's page-range behavior. Read-only over the buffered bytes.
    /// </remarks>
    private static IReadOnlyList<int> SelectPageNumbers(byte[] bytes, PageRange? range)
    {
        using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });
        var numbers = new List<int>(document.NumberOfPages);
        for (var number = 1; number <= document.NumberOfPages; number++)
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
    ///     Reports a single page's render failure as a diagnostic.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="code">The diagnostic code identifying the failure class.</param>
    /// <param name="pageNumber">The 1-based page number that failed.</param>
    /// <param name="detail">A short description of what went wrong for this page.</param>
    /// <remarks>
    ///     One diagnostic per failed page keeps the failure class machine-detectable and names the
    ///     specific page, while the accompanying counted gap (emitted once) summarizes the shortfall.
    ///     Side effect: records on the sink.
    /// </remarks>
    private static void ReportPageFailure(IExtractionSink sink, string code, int pageNumber, string detail)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            code, DiagnosticSeverity.Warning,
            $"Page {page} {detail}."));
    }

    /// <summary>
    ///     Reports the counted gap summarizing every page that could not be rendered.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="failures">The 1-based page numbers that failed to render.</param>
    /// <remarks>
    ///     A single gap with the affected count and the specific page numbers states the shortfall
    ///     precisely — how many pages, and which — so an absent page image is always explained rather
    ///     than silently missing. Side effect: records on the sink.
    /// </remarks>
    private static void ReportPageFailuresGap(IExtractionSink sink, IReadOnlyList<int> failures)
    {
        var pages = failures.Select(page => page.ToString(CultureInfo.InvariantCulture)).ToList();
        var count = failures.Count.ToString(CultureInfo.InvariantCulture);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Pages, "pages/", GapScope.PartiallyExtracted,
            $"{count} page(s) could not be rasterized and were omitted from the pages folder.",
            Impact: "Rendered page images for the named pages are not available.",
            Remedy: "Re-run at a lower DPI, or check that the pages are well-formed; the remaining pages were rendered.",
            AffectedCount: failures.Count,
            AffectedItems: pages));
    }

    /// <summary>
    ///     Runs the render round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Builds a one-page PDF in memory and rasterizes it, which proves the native stack is
    ///     genuinely functional here rather than merely loadable. Reports a reasoned skip when the
    ///     native binary is unavailable, and reports every fault as data rather than throwing.
    /// </remarks>
    private static SelfTestResult RunRenderRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var probe = PageRenderer.ProbeAvailability();
        if (!probe.IsAvailable)
        {
            return SelfTestResult.Skipped(
                $"PDF page rendering is unavailable in this environment: {probe.Reason}.");
        }

        var started = DateTimeOffset.UtcNow;
        try
        {
            var bytes = BuildProbeDocument();
            var png = PageRenderer.Render(bytes, 0, 96);
            return IsValidPng(png)
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The page renderer produced output that is not a valid PNG for a document it built itself.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The page renderer could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Builds the one-page document the render round-trip case rasterizes.
    /// </summary>
    /// <returns>The bytes of a minimal single-page PDF carrying a short line of text.</returns>
    /// <remarks>
    ///     Built rather than embedded so the case ships no binary payload and exercises the managed
    ///     writer and the native renderer together. Pure apart from the allocation.
    /// </remarks>
    private static byte[] BuildProbeDocument()
    {
        using var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddText("DocDown rendering self test", 12, new UglyToad.PdfPig.Core.PdfPoint(30, 600), font);
        return builder.Build();
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
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     A distinct <c>PDFR</c> prefix keeps this package's codes from colliding with Core's <c>DD</c>
///     range or the managed PDF backend's <c>PDF</c> range, so the ownership of any code in a
///     manifest is self-evident. Internal because the codes are a published output value, not an API
///     consumers program against. All members are constants and thread-safe.
/// </remarks>
internal static class PdfRenderingDiagnosticCodes
{
    /// <summary>A page could not be rasterized for a reason other than memory pressure.</summary>
    /// <remarks>Accompanies the counted gap naming the page; the run continued with the remaining pages.</remarks>
    internal const string PageRenderFailed = "PDFR0001";

    /// <summary>A page ran the renderer out of memory at the requested DPI.</summary>
    /// <remarks>Distinct from <see cref="PageRenderFailed"/> so a consumer can suggest a lower DPI specifically.</remarks>
    internal const string PageTooLarge = "PDFR0002";

    /// <summary>Reserved for a native fault raised mid-run by the rasterizer.</summary>
    /// <remarks>
    ///     Held distinct from <see cref="PageRenderFailed"/> so a genuine native fault can be told
    ///     apart from an ordinary unrenderable page if the failure surface is refined later.
    /// </remarks>
    internal const string NativeFault = "PDFR0003";
}

/// <summary>
///     A private <see cref="IExtractionContext"/> that reuses an outer context but substitutes a
///     modified options object for the delegated managed extraction.
/// </summary>
/// <remarks>
///     Core keeps its own <see cref="IExtractionContext"/> implementation internal, so this backend
///     supplies its own to run the managed backend against the same sink, format, environment, and
///     cancellation token while overriding the options — specifically to force
///     <see cref="ExtractionOptions.RenderPages"/> off so the delegate does not emit its own
///     rendering-unavailable gap. Immutable after construction and safe to read from the extraction
///     thread.
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
