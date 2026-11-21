using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Rendering.Shared;

namespace DemaConsulting.DocDown.Pdf.Rendering;

/// <summary>
///     The PDF page-rendering backend: a full superset extractor that produces text, embedded
///     images, and document metadata by delegating to the managed PDF backend, then rasterizes the
///     requested pages to PNG images through the fully-managed DemaConsulting.CanvasNet.Pdf
///     rasterizer.
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
///         <see cref="ProbeAvailability"/> is unconditional: page rendering through CanvasNet.Pdf is
///         a fully-managed capability with no native stack to probe for, so this backend always
///         reports itself available and states that it provides rendered pages here. This mirrors
///         <c>DemaConsulting.DocDown.Pdf</c>'s own <c>PdfDocumentExtractor.ProbeAvailability</c>, which is likewise
///         unconditional for the same reason.
///     </para>
///     <para>
///         Rendering can still fail for an individual page after extraction has started. Each page
///         that cannot be rasterized is reported as a plain <see cref="ExtractionNote"/> naming the
///         page, the remaining pages continue rendering, and normal completion still returns
///         <see cref="ExtractionOutcome.Produced"/>. Instances hold no per-extraction state and are
///         safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class PdfPageRenderingExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The embedded PDF the self-test reads.</summary>
    /// <remarks>A real PDF exported from Microsoft Word, so the rasterizer is given a page a real producer wrote.</remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Pdf.Rendering.Resources.probe.pdf";

    /// <summary>The shared delegate-then-rasterize pipeline this backend drives.</summary>
    /// <remarks>
    ///     Holds the rasterization function (default <see cref="PageRenderer.Render"/>) and the
    ///     page-count function it reads its selection from. Both are injectable through the
    ///     internal constructors so a test can substitute a function that faults on cue, which is
    ///     the only reliable way to exercise the per-page fault-isolation and count-failure paths.
    /// </remarks>
    private readonly PageRenderingPipeline _pipeline;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfPageRenderingExtractor"/> class that
    ///     rasterizes through the real CanvasNet.Pdf-backed <see cref="PageRenderer"/>.
    /// </summary>
    /// <remarks>The production constructor; the parameterless shape is what the registration seam calls.</remarks>
    public PdfPageRenderingExtractor()
        : this(PageRenderer.Render, PageRenderer.GetPageCount)
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
        : this(render, PageRenderer.GetPageCount)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfPageRenderingExtractor"/> class with a
    ///     supplied rasterization function and page-count function.
    /// </summary>
    /// <param name="render">
    ///     The function that renders one page (source bytes, zero-based page index, DPI) to PNG
    ///     bytes. Must not be null.
    /// </param>
    /// <param name="pageCount">
    ///     The function that reports how many pages the source document has. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="render"/> or <paramref name="pageCount"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Internal so tests can inject a faulting counter to prove a count failure becomes a
    ///     recorded note rather than an unreadable extraction.
    /// </remarks>
    internal PdfPageRenderingExtractor(Func<byte[], int, int, byte[]> render, Func<byte[], int> pageCount)
    {
        _pipeline = new PageRenderingPipeline(render, pageCount, "page");
    }

    /// <inheritdoc />
    public string Id => "pdf-rendering";

    /// <inheritdoc />
    public string DisplayName => "PDF pages (CanvasNet.Pdf)";

    /// <inheritdoc />
    public IReadOnlyCollection<DocumentFormat> SupportedFormats => [DocumentFormat.Pdf];

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Unconditional: page rendering through CanvasNet.Pdf is a fully-managed capability with no
    ///     native stack whose availability could vary by environment, so this backend always reports
    ///     itself available and states that rendered pages are provided here.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available(providesRenderedPages: true);

    /// <inheritdoc />
    /// <remarks>
    ///     Delegates the managed aspects to <see cref="PdfDocumentExtractor"/> with rendering suppressed, records
    ///     the <c>pages.renderer</c> environment fact, and rasterizes the requested pages only when
    ///     rendering was requested. A managed-parser fault propagates to Core unchanged.
    /// </remarks>
    public ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context) =>
        _pipeline.ExtractAsync(
            source,
            context,
            new PdfDocumentExtractor(),
            new EnvironmentFact("DemaConsulting.DocDown.Pdf.Rendering", "pages.renderer", "CanvasNet.Pdf (managed)", Available: true));

    /// <inheritdoc />
    /// <remarks>
    ///     Contributes one case that proves the rasterizer genuinely functions in this deployment:
    ///     it renders a page of the embedded PDF to a PNG. Rendering is always available, so this
    ///     case reports a pass or a fault as data; there is no unavailable-stack skip path to report.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("pdf-rendering.renderRoundTrip", Id, RunRenderRoundTrip)
    ];

    /// <summary>
    ///     Runs the render round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Rasterizes a page of the embedded probe document through the production
    ///     <see cref="PageRenderer"/>, which proves the rasterizer is genuinely functional here, not
    ///     merely referenceable. Reports every fault as data rather than throwing.
    /// </remarks>
    private static SelfTestResult RunRenderRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return PageRenderingPipeline.RunRenderRoundTrip(
            typeof(PdfPageRenderingExtractor).Assembly, ProbeResourceName, PageRenderer.Render, "page");
    }
}
