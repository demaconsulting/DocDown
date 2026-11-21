using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.PowerPoint.OpenXml;
using DemaConsulting.DocDown.Rendering.Shared;
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

    /// <summary>The shared delegate-then-rasterize pipeline this backend drives.</summary>
    /// <remarks>
    ///     Holds the rasterization function (default <see cref="SlideRenderer.Render"/>) and the
    ///     slide-count function it reads its selection from. Both are injectable through the
    ///     internal constructors so a test can substitute a function that faults on cue, which is
    ///     the only reliable way to exercise the per-slide fault-isolation and count-failure paths.
    /// </remarks>
    private readonly PageRenderingPipeline _pipeline;

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
        _pipeline = new PageRenderingPipeline(render, pageCount, "slide");
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
    /// <remarks>
    ///     Delegates the managed aspects to <see cref="PowerPointOpenXmlExtractor"/> with rendering suppressed, records
    ///     the <c>pages.renderer</c> environment fact, and rasterizes the requested slides only when
    ///     rendering was requested. A managed-parser fault propagates to Core unchanged.
    /// </remarks>
    public ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context) =>
        _pipeline.ExtractAsync(
            source,
            context,
            new PowerPointOpenXmlExtractor(),
            new EnvironmentFact("DemaConsulting.DocDown.PowerPoint", "pages.renderer", "CanvasNet.Pptx (managed)", Available: true));

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
    ///     Runs the render round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Rasterizes a slide of the embedded probe document through the production
    ///     <see cref="SlideRenderer"/>, which proves the rasterizer is genuinely functional here, not
    ///     merely referenceable. Reports every fault as data rather than throwing.
    /// </remarks>
    private static SelfTestResult RunRenderRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return PageRenderingPipeline.RunRenderRoundTrip(
            typeof(PowerPointPageRenderingExtractor).Assembly, ProbeResourceName, SlideRenderer.Render, "slide");
    }
}
