using System.Globalization;
using System.Runtime.Versioning;
using DocDown.Core;
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
    /// <summary>The resolution the render self-test asks for, chosen because it is the ordinary screen resolution and keeps the export quick.</summary>
    private const int SelfTestRenderDpi = 96;

    /// <summary>The name the self-test deck's theme and its schemes carry, so a stray artifact is identifiable as this case's own.</summary>
    private const string SelfTestThemeName = "DocDown Self Test";

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

    /// <summary>Runs the end-to-end render self-test: builds a deck and rasterizes it through the real COM adapter.</summary>
    /// <param name="context">The self-test context supplying the work folder and cancellation.</param>
    /// <returns>The case result: passed when a genuine slide image came back, skipped where PowerPoint is absent, failed otherwise.</returns>
    /// <remarks>
    ///     The availability case proves only that PowerPoint can be activated; this case walks
    ///     through the door, which is the only way the COM boundary — activation, read-only open,
    ///     point-to-pixel conversion, PNG export, and session teardown — is exercised anywhere. It
    ///     builds its own synthetic single-slide deck rather than shipping a fixture, so the case
    ///     carries no document content of its own, and drives
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
            File.WriteAllBytes(path, BuildSelfTestDeck());
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
    /// <param name="path">The absolute path of the synthetic deck to render.</param>
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
    ///     The deck has exactly one slide whose size is known, so the case can assert a real image
    ///     came back — one slide, non-empty PNG bytes with the PNG signature, and dimensions in the
    ///     plausible range — rather than merely that the call returned. Pure.
    /// </remarks>
    private static string? DescribeRenderShortfall(IReadOnlyList<PowerPointRenderedSlide> slides)
    {
        if (slides.Count != 1)
        {
            return $"Microsoft PowerPoint returned {slides.Count.ToString(CultureInfo.InvariantCulture)} slides "
                   + "for a deck the self-test built with exactly one.";
        }

        var slide = slides[0];
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
    ///     Builds the synthetic single-slide deck the render self-test rasterizes.
    /// </summary>
    /// <returns>The bytes of a one-slide presentation carrying an invented title line.</returns>
    /// <remarks>
    ///     Built rather than committed so the case ships no document of its own and no content from
    ///     any real presentation can reach it. The deck is deliberately complete — theme, slide
    ///     master, blank layout, and an explicit slide size — because Microsoft PowerPoint refuses to
    ///     open a deck missing any of them, and a refused open would report the environment as broken
    ///     when only the fixture was. Pure apart from its allocations.
    /// </remarks>
    private static byte[] BuildSelfTestDeck()
    {
        using var buffer = new MemoryStream();
        using (var document = PresentationDocument.Create(buffer, PresentationDocumentType.Presentation))
        {
            // The theme hangs from the presentation part and is shared with the master, which is the
            // arrangement PowerPoint expects to find when it opens the package
            var presentationPart = document.AddPresentationPart();
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            var themePart = presentationPart.AddNewPart<ThemePart>();
            masterPart.AddPart(themePart);
            themePart.Theme = BuildSelfTestTheme();

            // A blank layout and a master that declares it: the minimum furniture a slide needs to
            // inherit formatting from
            layoutPart.SlideLayout = BuildSelfTestLayout();
            masterPart.SlideMaster = BuildSelfTestMaster(masterPart.GetIdOfPart(layoutPart));
            layoutPart.AddPart(masterPart);

            // The one slide the case renders, carrying visible invented text so the exported image
            // has real content rather than an empty canvas
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = WithChildren(
                new P.Slide(),
                WithChildren(new P.CommonSlideData(), SelfTestTitleShapeTree()),
                WithChildren(new P.ColorMapOverride(), new A.MasterColorMapping()));
            slidePart.AddPart(layoutPart);

            presentationPart.Presentation = BuildSelfTestPresentation(
                presentationPart.GetIdOfPart(masterPart), presentationPart.GetIdOfPart(slidePart));
            presentationPart.Presentation.Save();
        }

        return buffer.ToArray();
    }

    /// <summary>Appends the given children to an Open XML element and returns it.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="element">The element to populate. Must not be null.</param>
    /// <param name="children">The children to append, in document order.</param>
    /// <returns>The same element, so calls compose into a tree at a single expression.</returns>
    /// <remarks>
    ///     The Open XML types offer element-tree constructors, but they collide with their
    ///     sequence-taking overloads in a way static analysis flags on every call. Appending
    ///     explicitly is unambiguous and keeps the tree readable. Mutates and returns its argument.
    /// </remarks>
    private static T WithChildren<T>(T element, params DocumentFormat.OpenXml.OpenXmlElement[] children)
        where T : DocumentFormat.OpenXml.OpenXmlElement
    {
        foreach (var child in children)
        {
            element.AppendChild(child);
        }

        return element;
    }

    /// <summary>Builds the blank slide layout the self-test slide inherits from.</summary>
    /// <returns>A preserved blank layout carrying no placeholders.</returns>
    private static P.SlideLayout BuildSelfTestLayout()
    {
        var layout = WithChildren(
            new P.SlideLayout(),
            WithChildren(new P.CommonSlideData { Name = "Blank" }, EmptySelfTestShapeTree()),
            WithChildren(new P.ColorMapOverride(), new A.MasterColorMapping()));
        layout.Type = P.SlideLayoutValues.Blank;
        layout.Preserve = true;
        return layout;
    }

    /// <summary>Builds the slide master that owns the self-test layout.</summary>
    /// <param name="layoutRelationshipId">The relationship id of the layout part the master declares.</param>
    /// <returns>A master carrying the color map, the layout declaration, and empty text styles.</returns>
    private static P.SlideMaster BuildSelfTestMaster(string layoutRelationshipId) =>
        WithChildren(
            new P.SlideMaster(),
            WithChildren(new P.CommonSlideData(), EmptySelfTestShapeTree()),
            BuildSelfTestColorMap(),
            WithChildren(
                new P.SlideLayoutIdList(),
                new P.SlideLayoutId { Id = 2147483649U, RelationshipId = layoutRelationshipId }),
            WithChildren(new P.TextStyles(), new P.TitleStyle(), new P.BodyStyle(), new P.OtherStyle()));

    /// <summary>Builds the presentation part's root element, declaring the master, the slide, and the page sizes.</summary>
    /// <param name="masterRelationshipId">The relationship id of the slide master part.</param>
    /// <param name="slideRelationshipId">The relationship id of the single slide part.</param>
    /// <returns>The presentation element.</returns>
    /// <remarks>The slide size is a standard widescreen slide, which fixes the pixel dimensions the render case judges.</remarks>
    private static P.Presentation BuildSelfTestPresentation(string masterRelationshipId, string slideRelationshipId) =>
        WithChildren(
            new P.Presentation(),
            WithChildren(
                new P.SlideMasterIdList(),
                new P.SlideMasterId { Id = 2147483648U, RelationshipId = masterRelationshipId }),
            WithChildren(
                new P.SlideIdList(),
                new P.SlideId { Id = 256U, RelationshipId = slideRelationshipId }),
            new P.SlideSize { Cx = 12192000, Cy = 6858000 },
            new P.NotesSize { Cx = 6858000, Cy = 9144000 });

    /// <summary>Builds the mandatory group-shape properties every shape tree begins with.</summary>
    /// <returns>The non-visual group-shape properties element.</returns>
    private static P.NonVisualGroupShapeProperties SelfTestGroupProperties() =>
        WithChildren(
            new P.NonVisualGroupShapeProperties(),
            new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
            new P.NonVisualGroupShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties());

    /// <summary>Builds an empty shape tree for the self-test deck's master and layout.</summary>
    /// <returns>A shape tree carrying only the mandatory group-shape furniture.</returns>
    private static P.ShapeTree EmptySelfTestShapeTree() =>
        WithChildren(new P.ShapeTree(), SelfTestGroupProperties(), new P.GroupShapeProperties());

    /// <summary>Builds the self-test slide's shape tree: one positioned text box carrying an invented title.</summary>
    /// <returns>The shape tree for the single self-test slide.</returns>
    /// <remarks>The text is invented for this case alone so no real presentation's wording can ever reach a rendered self-test image.</remarks>
    private static P.ShapeTree SelfTestTitleShapeTree() =>
        WithChildren(
            new P.ShapeTree(),
            SelfTestGroupProperties(),
            new P.GroupShapeProperties(),
            WithChildren(
                new P.Shape(),
                WithChildren(
                    new P.NonVisualShapeProperties(),
                    new P.NonVisualDrawingProperties { Id = 2U, Name = "Self Test Title" },
                    new P.NonVisualShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                WithChildren(
                    new P.ShapeProperties(),
                    WithChildren(
                        new A.Transform2D(),
                        new A.Offset { X = 838200L, Y = 1143000L },
                        new A.Extents { Cx = 10515600L, Cy = 1325563L }),
                    SelfTestRectangleGeometry()),
                WithChildren(
                    new P.TextBody(),
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    WithChildren(
                        new A.Paragraph(),
                        WithChildren(
                            new A.Run(),
                            new A.RunProperties { Language = "en-US", FontSize = 4000 },
                            new A.Text("DocDown PowerPoint render self test"))))));

    /// <summary>Builds the rectangular outline the self-test title shape occupies.</summary>
    /// <returns>A preset rectangle geometry with no adjustments.</returns>
    private static A.PresetGeometry SelfTestRectangleGeometry()
    {
        var geometry = WithChildren(new A.PresetGeometry(), new A.AdjustValueList());
        geometry.Preset = A.ShapeTypeValues.Rectangle;
        return geometry;
    }

    /// <summary>Builds the color map that ties the self-test master to its theme's color scheme.</summary>
    /// <returns>The color map element, with every slot mapped to its conventional scheme entry.</returns>
    private static P.ColorMap BuildSelfTestColorMap() => new()
    {
        Background1 = A.ColorSchemeIndexValues.Light1,
        Text1 = A.ColorSchemeIndexValues.Dark1,
        Background2 = A.ColorSchemeIndexValues.Light2,
        Text2 = A.ColorSchemeIndexValues.Dark2,
        Accent1 = A.ColorSchemeIndexValues.Accent1,
        Accent2 = A.ColorSchemeIndexValues.Accent2,
        Accent3 = A.ColorSchemeIndexValues.Accent3,
        Accent4 = A.ColorSchemeIndexValues.Accent4,
        Accent5 = A.ColorSchemeIndexValues.Accent5,
        Accent6 = A.ColorSchemeIndexValues.Accent6,
        Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
        FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
    };

    /// <summary>Builds the minimal theme the self-test deck's master requires.</summary>
    /// <returns>A theme declaring a color scheme, a font scheme, and a format scheme.</returns>
    /// <remarks>
    ///     Every scheme is present because PowerPoint rejects a deck whose master has no theme, and
    ///     each style list carries the three entries the format schedule requires. The colors and
    ///     typefaces are ordinary defaults chosen only so the render has something to draw with.
    /// </remarks>
    private static A.Theme BuildSelfTestTheme()
    {
        var theme = WithChildren(
            new A.Theme(),
            WithChildren(
                new A.ThemeElements(),
                BuildSelfTestColorScheme(),
                BuildSelfTestFontScheme(),
                BuildSelfTestFormatScheme()));
        theme.Name = SelfTestThemeName;
        return theme;
    }

    /// <summary>Builds the self-test theme's color scheme.</summary>
    /// <returns>A color scheme naming every slot the schema requires.</returns>
    /// <remarks>The colors are ordinary defaults chosen only so the render has something to draw with.</remarks>
    private static A.ColorScheme BuildSelfTestColorScheme()
    {
        var scheme = WithChildren(
            new A.ColorScheme(),
            WithChildren(
                new A.Dark1Color(),
                new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
            WithChildren(
                new A.Light1Color(),
                new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
            WithChildren(new A.Dark2Color(), new A.RgbColorModelHex { Val = "1F3864" }),
            WithChildren(new A.Light2Color(), new A.RgbColorModelHex { Val = "E7E6E6" }),
            WithChildren(new A.Accent1Color(), new A.RgbColorModelHex { Val = "4472C4" }),
            WithChildren(new A.Accent2Color(), new A.RgbColorModelHex { Val = "ED7D31" }),
            WithChildren(new A.Accent3Color(), new A.RgbColorModelHex { Val = "A5A5A5" }),
            WithChildren(new A.Accent4Color(), new A.RgbColorModelHex { Val = "FFC000" }),
            WithChildren(new A.Accent5Color(), new A.RgbColorModelHex { Val = "5B9BD5" }),
            WithChildren(new A.Accent6Color(), new A.RgbColorModelHex { Val = "70AD47" }),
            WithChildren(new A.Hyperlink(), new A.RgbColorModelHex { Val = "0563C1" }),
            WithChildren(new A.FollowedHyperlinkColor(), new A.RgbColorModelHex { Val = "954F72" }));
        scheme.Name = SelfTestThemeName;
        return scheme;
    }

    /// <summary>Builds the self-test theme's font scheme.</summary>
    /// <returns>A font scheme declaring a major and a minor font that name no typeface.</returns>
    /// <remarks>
    ///     The typefaces are left empty so the deck asks for whichever font the rendering host
    ///     defaults to, rather than naming one that may not be installed on the machine running the
    ///     self-test.
    /// </remarks>
    private static A.FontScheme BuildSelfTestFontScheme()
    {
        var scheme = WithChildren(
            new A.FontScheme(),
            WithChildren(
                new A.MajorFont(),
                new A.LatinFont { Typeface = string.Empty },
                new A.EastAsianFont { Typeface = string.Empty },
                new A.ComplexScriptFont { Typeface = string.Empty }),
            WithChildren(
                new A.MinorFont(),
                new A.LatinFont { Typeface = string.Empty },
                new A.EastAsianFont { Typeface = string.Empty },
                new A.ComplexScriptFont { Typeface = string.Empty }));
        scheme.Name = SelfTestThemeName;
        return scheme;
    }

    /// <summary>Builds the self-test theme's format scheme.</summary>
    /// <returns>A format scheme whose four style lists each carry the three entries the schema requires.</returns>
    private static A.FormatScheme BuildSelfTestFormatScheme()
    {
        var scheme = WithChildren(
            new A.FormatScheme(),
            WithChildren(new A.FillStyleList(), SelfTestFill(), SelfTestFill(), SelfTestFill()),
            WithChildren(new A.LineStyleList(), SelfTestOutline(), SelfTestOutline(), SelfTestOutline()),
            WithChildren(new A.EffectStyleList(), SelfTestEffect(), SelfTestEffect(), SelfTestEffect()),
            WithChildren(new A.BackgroundFillStyleList(), SelfTestFill(), SelfTestFill(), SelfTestFill()));
        scheme.Name = SelfTestThemeName;
        return scheme;
    }

    /// <summary>Builds one plain white fill entry for the self-test theme's style lists.</summary>
    /// <returns>A solid white fill.</returns>
    /// <remarks>A new instance each call because an Open XML element belongs to exactly one parent.</remarks>
    private static A.SolidFill SelfTestFill() =>
        WithChildren(new A.SolidFill(), new A.RgbColorModelHex { Val = "FFFFFF" });

    /// <summary>Builds one plain black line entry for the self-test theme's line style list.</summary>
    /// <returns>A thin solid black outline.</returns>
    /// <remarks>A new instance each call because an Open XML element belongs to exactly one parent.</remarks>
    private static A.Outline SelfTestOutline()
    {
        var outline = WithChildren(
            new A.Outline(),
            WithChildren(new A.SolidFill(), new A.RgbColorModelHex { Val = "000000" }));
        outline.Width = 6350;
        return outline;
    }

    /// <summary>Builds one empty effect entry for the self-test theme's effect style list.</summary>
    /// <returns>An effect style carrying no effects.</returns>
    /// <remarks>A new instance each call because an Open XML element belongs to exactly one parent.</remarks>
    private static A.EffectStyle SelfTestEffect() =>
        WithChildren(new A.EffectStyle(), new A.EffectList());

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
