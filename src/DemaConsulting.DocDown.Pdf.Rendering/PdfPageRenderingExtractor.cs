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
///         This backend deliberately composes <see cref="PdfDocumentExtractor"/> rather than
///         re-implementing the managed PDF path. It clones the caller's
///         <see cref="ExtractionOptions"/>, forces <see cref="ExtractionOptions.RenderPages"/> off
///         on the delegated copy, and runs the managed backend against the same sink so text,
///         embedded images, and document metadata are emitted exactly once before page rendering
///         begins.
///     </para>
///     <para>
///         <see cref="ProbeAvailability"/> is the only place this backend reports whether page
///         rendering is possible in the current environment. When
///         <see cref="PageRenderer.ProbeAvailability"/> succeeds, the backend reports itself
///         available and states that it provides rendered pages here; when the probe fails, the
///         backend reports itself unavailable with the probe's reason so selection can choose
///         another extractor before any document is opened.
///     </para>
///     <para>
///         Rendering can still fail for an individual page after extraction has started. Each page
///         that cannot be rasterized is reported as a plain <see cref="ExtractionNote"/> naming the
///         page, the remaining pages continue rendering, and normal completion still returns
///         <see cref="ExtractionOutcome.Produced"/>. Instances hold no per-extraction state and are
///         safe to register once and reuse; concurrent renders are serialized by
///         <see cref="PageRenderer"/>.
///     </para>
/// </remarks>
public sealed class PdfPageRenderingExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The embedded PDF the self-test reads.</summary>
    /// <remarks>A real PDF exported from Microsoft Word, so the rasterizer is given a page a real producer wrote.</remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Pdf.Rendering.Resources.probe.pdf";

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
    ///     Internal so tests can inject a faulting renderer to prove per-page failures become
    ///     recorded notes; production always flows through the parameterless constructor.
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
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Cheap and non-throwing: it asks <see cref="PageRenderer"/> whether the PDFium native can
    ///     load for the current runtime identifier (a one-time, cached, side-effect-light check) and
    ///     never rasterizes. When the native stack is present the backend reports that rendered pages
    ///     are available here; when it is absent the backend reports unavailable with a reason so
    ///     selection can choose another extractor before any extraction starts.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability()
    {
        var probe = PageRenderer.ProbeAvailability();
        return probe.IsAvailable
            ? ExtractorAvailability.Available(providesRenderedPages: true)
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
        // content, images, metadata, and its own notes without attempting page output itself. A
        // parser fault (encrypted, malformed, truncated) propagates from here to Core unchanged,
        // which converts it into a structured failure that still writes the full layout.
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await new PdfDocumentExtractor()
            .ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);
        if (baseOutcome == ExtractionOutcome.Unreadable)
        {
            return baseOutcome;
        }

        // Record the authoritative rendering fact under a distinct key, complementing (not
        // contradicting) the base backend's own pdf.pageRendering fact
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Pdf.Rendering", "pages.renderer", "PDFtoImage (PDFium/SkiaSharp, native)", Available: true));

        // Rasterize the requested pages; per-page faults become notes rather than exceptions that
        // abort the extraction
        await RenderPagesAsync(bytes, sink, options, cancellationToken).ConfigureAwait(false);
        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes one case that proves the native stack genuinely rasterizes in this
    ///     deployment: it builds a one-page PDF and renders it to a PNG. Where the native binary is
    ///     absent the case reports a reasoned skip rather than a failure, because an unavailable
    ///     renderer must not be recorded as a broken one. The engine also wraps this backend's cases
    ///     to skip when <see cref="ProbeAvailability"/> reports it unavailable, so the skip is honest
    ///     whether observed here or upstream.
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
    /// <param name="sink">The sink to write rendered pages and notes through.</param>
    /// <param name="options">The effective options carrying the page range and render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    ///     Renders each selected page independently so one unrenderable page records a note rather
    ///     than aborting the whole extraction. Cancellation propagates; every other fault is caught
    ///     per page and recorded as a note naming that page. Side effect: writes pages and notes on
    ///     the sink.
    /// </remarks>
    private async ValueTask RenderPagesAsync(
        byte[] bytes, IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        var pageNumbers = SelectPageNumbers(bytes, options.Pages);
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
                ReportPageFailure(sink, pageNumber);
                continue;
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or BadImageFormatException or System.Runtime.InteropServices.SEHException)
            {
                // A native-level fault raised mid-run by the rasterizer is still isolated to the
                // page that triggered it so the remaining pages can continue
                ReportPageFailure(sink, pageNumber);
                continue;
            }
#pragma warning disable CA1031 // Per-page fault isolation: any render fault becomes a note, never an exception to the caller
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
    ///     Reports a single page's render failure as a plain note.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="pageNumber">The 1-based page number that failed.</param>
    /// <remarks>
    ///     Keeps the report aligned with DocDown's reduced output model: the note states only the
    ///     extraction fact that this page could not be rasterized, without assigning a code,
    ///     severity, remedy, or impact. Side effect: records on the sink.
    /// </remarks>
    private static void ReportPageFailure(IExtractionSink sink, int pageNumber)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote($"Page {page} could not be rasterized."));
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
            var bytes = SelfTestProbe.Load(typeof(PdfPageRenderingExtractor).Assembly, ProbeResourceName);
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
