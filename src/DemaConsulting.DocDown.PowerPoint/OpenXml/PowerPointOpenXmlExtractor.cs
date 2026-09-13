using DocDown.Core;
using DocDown.PowerPoint.Markdown;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using CoreFormat = DocDown.Core.DocumentFormat;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocDown.PowerPoint.OpenXml;

/// <summary>
///     The PowerPoint Open XML backend: extracts every slide's text, title, and — above all —
///     speaker notes, in presentation order, from a <c>.pptx</c> into the DocDown output contract.
/// </summary>
/// <remarks>
///     <para>
///         A deck carries content that never appears in any render: speaker notes hold the
///         narration, the caveats, and the argument the slide only gestures at, and they are
///         invisible to any renderer. This backend therefore always extracts, from the file itself,
///         the text of every slide, the slide title where one exists, the speaker notes, the deck's
///         embedded images, and the slide order. It is 100% managed and declares
///         <see cref="ExtractorCapabilities.Text"/>, <see cref="ExtractorCapabilities.EmbeddedImages"/>,
///         <see cref="ExtractorCapabilities.DocumentStructure"/>,
///         and <see cref="ExtractorCapabilities.DocumentMetadata"/> and pointedly not
///         <see cref="ExtractorCapabilities.RenderedPages"/>: rasterizing a slide needs a renderer
///         this package's managed backend deliberately does not ship. Rendering is delivered by the
///         separate COM backend when Microsoft PowerPoint is available.
///     </para>
///     <para>
///         Because nothing about this backend is environment-dependent, <see cref="ProbeAvailability"/>
///         is unconditional and performs no I/O. Adverse decks are not translated into results here:
///         Core catches any exception and converts it into a structured failure with the full layout
///         still written. Instances hold no per-extraction state and are safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class PowerPointOpenXmlExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>
    ///     Creates a PowerPoint Open XML extractor ready to register with a <see cref="DocDownBuilder"/>.
    /// </summary>
    /// <remarks>
    ///     Construction is deliberately trivial: the extractor holds no per-extraction state, opens
    ///     no file, and probes no environment here, so a single instance can be registered once and
    ///     reused across concurrent extractions. Most callers never call this directly and instead
    ///     use <c>AddPowerPoint</c>, which registers a factory with the builder.
    /// </remarks>
    public PowerPointOpenXmlExtractor()
    {
    }

    /// <inheritdoc />
    public string Id => "powerpoint-openxml";

    /// <inheritdoc />
    public string DisplayName => "PowerPoint (Open XML SDK)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Pptx];

    /// <inheritdoc />
    public ExtractorCapabilities Capabilities =>
        ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages
        | ExtractorCapabilities.DocumentMetadata | ExtractorCapabilities.DocumentStructure;

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    /// <remarks>
    ///     Always available, with the full declared capability set. The Open XML SDK is a managed
    ///     assembly that ships inside this package. Performs no I/O and cannot throw.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available(Capabilities);

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var cancellationToken = context.CancellationToken;

        // Record what parsed this document, and state plainly that page rendering is not on offer here
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.PowerPoint", "powerpoint.backend", "Open XML SDK (managed)", Available: true));
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.PowerPoint", "powerpoint.pageRendering", "not provided by this extractor", Available: false));

        // Buffer the source: the package reader must seek, and a stream source is not guaranteed seekable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Any fault from here propagates to Core, which converts it into a structured failure
        using var stream = new MemoryStream(bytes, writable: false);
        var model = PowerPointOpenXmlReader.Read(stream);

        var degraded = await PowerPointContentEmitter.EmitAsync(sink, context.Options, model, cancellationToken)
            .ConfigureAwait(false);
        return degraded ? ExtractionOutcome.Degraded : ExtractionOutcome.Succeeded;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The cases describe this backend's own behavior in its deployed environment: that it can
    ///     read a deck it builds itself, and that page rendering is genuinely not provided rather than
    ///     merely untested. The rendering case reports as skipped with a reason, because a capability
    ///     this package's managed backend does not claim must not be reported as a failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("powerpoint.openxml.parseRoundTrip", Id, RunParseRoundTrip),
        new SelfTestCase("powerpoint.pageRendering", Id, static _ => SelfTestResult.Skipped(
            "This extractor does not provide the renderedPages capability; slide rendering needs the COM backend."))
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>Buffering gives the reader a seekable stream to open the package. Read-only I/O.</remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Runs the parse round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Builds a one-slide deck in memory and reads it back, which proves the SDK is genuinely
    ///     functional in this deployment rather than merely present.
    /// </remarks>
    private static SelfTestResult RunParseRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = DateTimeOffset.UtcNow;

        try
        {
            using var stream = new MemoryStream();
            BuildProbeDeck(stream);
            stream.Position = 0;
            var model = PowerPointOpenXmlReader.Read(stream);
            return model.Slides.Count > 0
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The Open XML reader read a deck it built itself but found no slides.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The Open XML reader could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Builds the one-slide deck the parse round-trip case reads back.
    /// </summary>
    /// <param name="stream">The stream to write the deck into.</param>
    /// <remarks>Built rather than embedded so the case ships no binary payload. Side effect: writes to the stream.</remarks>
    private static void BuildProbeDeck(Stream stream)
    {
        using var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var placeholder = new P.ApplicationNonVisualDrawingProperties();
        placeholder.AppendChild(new P.PlaceholderShape { Type = P.PlaceholderValues.Title });

        var nonVisualShapeProperties = new P.NonVisualShapeProperties();
        nonVisualShapeProperties.AppendChild(new P.NonVisualDrawingProperties { Id = 2U, Name = "Title" });
        nonVisualShapeProperties.AppendChild(new P.NonVisualShapeDrawingProperties());
        nonVisualShapeProperties.AppendChild(placeholder);

        var run = new D.Run();
        run.AppendChild(new D.Text("DocDown self test"));
        var paragraph = new D.Paragraph();
        paragraph.AppendChild(run);
        var textBody = new P.TextBody();
        textBody.AppendChild(new D.BodyProperties());
        textBody.AppendChild(new D.ListStyle());
        textBody.AppendChild(paragraph);

        var shape = new P.Shape();
        shape.AppendChild(nonVisualShapeProperties);
        shape.AppendChild(new P.ShapeProperties());
        shape.AppendChild(textBody);

        var nonVisualGroupShapeProperties = new P.NonVisualGroupShapeProperties();
        nonVisualGroupShapeProperties.AppendChild(new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty });
        nonVisualGroupShapeProperties.AppendChild(new P.NonVisualGroupShapeDrawingProperties());
        nonVisualGroupShapeProperties.AppendChild(new P.ApplicationNonVisualDrawingProperties());

        var shapeTree = new P.ShapeTree();
        shapeTree.AppendChild(nonVisualGroupShapeProperties);
        shapeTree.AppendChild(new P.GroupShapeProperties());
        shapeTree.AppendChild(shape);

        var commonSlideData = new P.CommonSlideData();
        commonSlideData.AppendChild(shapeTree);
        var slide = new P.Slide();
        slide.AppendChild(commonSlideData);
        slidePart.Slide = slide;

        var slideIdList = new P.SlideIdList();
        slideIdList.AppendChild(new P.SlideId { Id = 256U, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
        presentationPart.Presentation.AppendChild(slideIdList);
    }
}
