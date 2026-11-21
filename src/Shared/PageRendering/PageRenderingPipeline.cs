using System.Globalization;
using System.Reflection;
using DemaConsulting.DocDown.Core;

namespace DemaConsulting.DocDown.Rendering.Shared;

/// <summary>
///     The extraction pipeline shared by the page-rendering superset backends: buffer the source,
///     delegate the managed aspects with rendering suppressed, then rasterize the requested pages
///     with per-page fault isolation.
/// </summary>
/// <remarks>
///     <para>
///         The PDF, PowerPoint, and Visio page-rendering backends follow the identical
///         delegate-then-rasterize contract and differ only in the managed backend they compose, the
///         rasterizer they drive, the environment fact they record, and whether a rendered unit is
///         called a "page" or a "slide". This class holds that contract once so the three backends
///         cannot drift apart.
///     </para>
///     <para>
///         The file is compiled into each rendering assembly as an <c>internal</c> type (a linked
///         source file) rather than published from Core, so the rendering packages keep depending on
///         Core's public contract only and no new public API is introduced. Instances hold only the
///         immutable rasterization seam and are safe to reuse across extractions.
///     </para>
/// </remarks>
internal sealed class PageRenderingPipeline
{
    /// <summary>The per-page rasterization function (source bytes, zero-based index, DPI) to PNG bytes.</summary>
    /// <remarks>Injected so a test can substitute a function that faults on a chosen page.</remarks>
    private readonly Func<byte[], int, int, byte[]> _render;

    /// <summary>The page-count function the page selection is read from.</summary>
    /// <remarks>Injected for the same reason as <see cref="_render"/>, to exercise the count-failure path.</remarks>
    private readonly Func<byte[], int> _pageCount;

    /// <summary>The lower-case name of one rendered unit ("page" or "slide") used in notes.</summary>
    /// <remarks>Keeps each backend's notes phrased in its document's own vocabulary.</remarks>
    private readonly string _unitName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PageRenderingPipeline"/> class.
    /// </summary>
    /// <param name="render">
    ///     The function that renders one page (source bytes, zero-based page index, DPI) to PNG
    ///     bytes. Must not be null.
    /// </param>
    /// <param name="pageCount">The function that reports how many pages the source has. Must not be null.</param>
    /// <param name="unitName">The lower-case name of one rendered unit, such as <c>"page"</c> or <c>"slide"</c>.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="render"/>, <paramref name="pageCount"/>, or <paramref name="unitName"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <remarks>Captures the rasterization seam; performs no I/O.</remarks>
    public PageRenderingPipeline(Func<byte[], int, int, byte[]> render, Func<byte[], int> pageCount, string unitName)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(pageCount);
        ArgumentNullException.ThrowIfNull(unitName);
        _render = render;
        _pageCount = pageCount;
        _unitName = unitName;
    }

    /// <summary>
    ///     Runs a full superset extraction: the managed aspects through <paramref name="managedBackend"/>,
    ///     then the requested rendered pages.
    /// </summary>
    /// <param name="source">The document to extract. Must not be null.</param>
    /// <param name="context">The engine-supplied extraction context. Must not be null.</param>
    /// <param name="managedBackend">The managed backend that produces text, images, and metadata. Must not be null.</param>
    /// <param name="rendererFact">The environment fact naming the rasterizer, recorded once the managed aspects succeed.</param>
    /// <returns>
    ///     <see cref="ExtractionOutcome.Unreadable"/> when the managed backend reports it; otherwise
    ///     <see cref="ExtractionOutcome.Produced"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="source"/>, <paramref name="context"/>, or <paramref name="managedBackend"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     The source is buffered once because both the managed backend and the rasterizer read it
    ///     from the start and a stream source is not re-readable. A parser fault in the managed
    ///     backend propagates unchanged so Core converts it into a structured failure that still
    ///     writes the full layout. Page rasterization is skipped when rendering was not requested,
    ///     because selection may hand a rendering backend a non-rendering request when it is the
    ///     sole candidate for the format. Side effects: writes content, images, metadata, pages,
    ///     facts, and notes on the sink.
    /// </remarks>
    public async ValueTask<ExtractionOutcome> ExtractAsync(
        DocumentSource source, IExtractionContext context, IDocumentExtractor managedBackend, EnvironmentFact rendererFact)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(managedBackend);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Buffer the source once: the managed backend reads it for text/images/metadata and the
        // rasterizer reads it again for rasterization, and a stream source is not re-readable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Delegate the managed aspects to the base backend, with rendering suppressed so it writes
        // content, images, metadata, and its own notes without attempting page output itself
        var delegatedContext = new DelegatedExtractionContext(context, options.Clone());
        delegatedContext.Options.RenderPages = false;
        using var delegatedStream = new MemoryStream(bytes, writable: false);
        var delegatedSource = DocumentSource.FromStream(delegatedStream, source.FileName);
        var baseOutcome = await managedBackend.ExtractAsync(delegatedSource, delegatedContext).ConfigureAwait(false);
        if (baseOutcome == ExtractionOutcome.Unreadable)
        {
            return baseOutcome;
        }

        // Record the authoritative rendering fact under a distinct key, complementing (not
        // contradicting) the base backend's own page-rendering fact
        sink.ReportEnvironmentFact(rendererFact);

        // Rasterize the requested pages only when rendering was actually requested
        if (options.RenderPages)
        {
            await RenderPagesAsync(bytes, sink, options, cancellationToken).ConfigureAwait(false);
        }

        return ExtractionOutcome.Produced;
    }

    /// <summary>
    ///     Runs a rendering backend's round-trip self-test against its embedded probe document.
    /// </summary>
    /// <param name="assembly">The rendering assembly carrying the probe resource. Must not be null.</param>
    /// <param name="probeResourceName">The manifest resource name of the probe document.</param>
    /// <param name="render">The production rasterization function to exercise. Must not be null.</param>
    /// <param name="unitName">The lower-case name of one rendered unit, used in failure messages.</param>
    /// <returns>A passed result when the first page renders to a valid PNG; otherwise a failed result.</returns>
    /// <remarks>
    ///     Proves the rasterizer is genuinely functional in this deployment, not merely referenceable.
    ///     A self-test is an isolation boundary: every fault is reported as data rather than thrown at
    ///     the caller, so one broken backend cannot abort the whole validation run.
    /// </remarks>
    public static SelfTestResult RunRenderRoundTrip(
        Assembly assembly, string probeResourceName, Func<byte[], int, int, byte[]> render, string unitName)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var bytes = SelfTestProbe.Load(assembly, probeResourceName);
            var png = render(bytes, 0, 96);
            return IsValidPng(png)
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    $"The {unitName} renderer produced output that is not a valid PNG for the embedded document.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The {unitName} renderer could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     A stream-backed source is read-once and possibly non-seekable, so buffering once makes
    ///     both reads identical rather than making a stream source fail late. Read-only I/O.
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
    /// <param name="bytes">The buffered source document bytes.</param>
    /// <param name="sink">The sink to write rendered pages and notes through.</param>
    /// <param name="options">The effective options carrying the page range and render DPI.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    ///     Renders each selected page independently so one unrenderable page records a note rather
    ///     than aborting the whole extraction. Cancellation propagates; every other fault is caught
    ///     per page and recorded as a note naming that page. When no page was selected to render,
    ///     the outcome is recorded here rather than inferred by Core, because Core cannot tell this
    ///     note apart from an unrelated one the delegated base backend may have already recorded.
    ///     Side effect: writes pages and notes on the sink.
    /// </remarks>
    private async ValueTask RenderPagesAsync(
        byte[] bytes, IExtractionSink sink, ExtractionOptions options, CancellationToken cancellationToken)
    {
        List<int> pageNumbers;
        try
        {
            pageNumbers = SelectPageNumbers(bytes, options.Pages);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a caller decision, not a count fault; let it propagate
            throw;
        }
#pragma warning disable CA1031 // A count fault costs the pages, not the extraction: the delegated content is already written
        catch (Exception)
#pragma warning restore CA1031
        {
            // The managed delegate already read this document, so its text, images and metadata
            // stand. Only the rendered pages are lost, and that is reported as a plain note rather
            // than collapsing an otherwise good extraction into an unreadable one.
            sink.ReportNote(new ExtractionNote(
                $"{Capitalize(_unitName)}s could not be counted, so no {_unitName} images were rendered."));
            return;
        }

        if (pageNumbers.Count == 0)
        {
            // No page was selected to render (an empty document, or a page range matching
            // nothing): state that plainly, since Core cannot distinguish this from a note the
            // delegated base backend already recorded for an unrelated reason
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
    /// <param name="bytes">The buffered source document bytes.</param>
    /// <param name="range">The requested inclusive page range, or <see langword="null"/> for the whole document.</param>
    /// <returns>The selected page numbers in document order; empty when the document has no pages.</returns>
    /// <remarks>
    ///     Reads the page count through the same rasterization seam that renders the pages, so the
    ///     selection cannot disagree with what the renderer can actually reach. Pages outside the
    ///     document are silently absent from the selection rather than an error, matching the
    ///     managed backends' page-range behavior. Read-only over the buffered bytes.
    /// </remarks>
    private List<int> SelectPageNumbers(byte[] bytes, PageRange? range)
    {
        var pageCount = _pageCount(bytes);
        var numbers = new List<int>(pageCount);
        for (var number = 1; number <= pageCount; number++)
        {
            if (range is not { } selected || selected.Contains(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    /// <summary>
    ///     Reports a single page's render failure as a plain note.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="pageNumber">The 1-based page number that failed.</param>
    /// <remarks>
    ///     The note states only the extraction fact that this page could not be rasterized, without
    ///     assigning a code, severity, remedy, or impact. Side effect: records on the sink.
    /// </remarks>
    private void ReportPageFailure(IExtractionSink sink, int pageNumber)
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        sink.ReportNote(new ExtractionNote($"{Capitalize(_unitName)} {page} could not be rasterized."));
    }

    /// <summary>
    ///     Upper-cases the first character of a unit name for use at the start of a sentence.
    /// </summary>
    /// <param name="unitName">The lower-case unit name; must be non-empty.</param>
    /// <returns>The unit name with its first character upper-cased.</returns>
    /// <remarks>Pure; uses the invariant culture so notes do not vary by machine locale.</remarks>
    private static string Capitalize(string unitName) =>
        string.Concat(char.ToUpperInvariant(unitName[0]).ToString(), unitName[1..]);

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
///     An <see cref="IExtractionContext"/> that reuses an outer context but substitutes a modified
///     options object for the delegated managed extraction.
/// </summary>
/// <remarks>
///     Core keeps its own <see cref="IExtractionContext"/> implementation internal, so the rendering
///     backends supply their own to run the managed backend against the same sink, format,
///     environment, and cancellation token while overriding the options — specifically to force
///     <see cref="ExtractionOptions.RenderPages"/> off so the delegate leaves page output to the
///     rendering backend. Immutable after construction and safe to read from the extraction thread.
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
