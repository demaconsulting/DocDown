using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Visio.Markdown;
using DocDown.Visio.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Visio.Markdown;

/// <summary>
///     Unit tests for <see cref="VisioContentEmitter"/>, exercising topology rendering and the
///     inventory-and-notes policy from hand-built models with no drawing behind them.
/// </summary>
public class VisioContentEmitterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the directed topology is rendered as a readable edge list resolving shape ids to
    ///     their text (<c>Inlet Tank → Transfer Pump</c>), under the page name heading.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_RendersDirectedTopologyWithShapeText()
    {
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "Wash System",
                [new VisioShapeModel("1", "Inlet Tank"), new VisioShapeModel("2", "Transfer Pump")],
                [new VisioConnectionModel("1", "2")])
        ]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Wash System", content, StringComparison.Ordinal);
        Assert.Contains("Inlet Tank \u2192 Transfer Pump", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an unlabeled connector endpoint degrades to a stable shape-id label rather than an
    ///     empty edge.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_UnlabeledEndpoint_UsesShapeIdLabel()
    {
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "P",
                [new VisioShapeModel("1", "Source"), new VisioShapeModel("2", null)],
                [new VisioConnectionModel("1", "2")])
        ]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Source \u2192 (shape 2)", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a default extraction of a drawing that embeds no content images records no notes
    ///     and reports the looked-for content inventory, including zero connections.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_NoImages_LeavesNotesEmptyAndReportsInventory()
    {
        var model = new VisioDocumentModel([new VisioPageModel("P", [new VisioShapeModel("1", "A")], [])]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Empty(sink.Images);
        Assert.Empty(sink.Notes);
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "pages", Count: 1, LookedFor: true });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "labeled shapes", Count: 1, LookedFor: true });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "connections", Count: 0, LookedFor: true });
    }

    /// <summary>
    ///     Proves a drawing's embedded content images are written through the sink as passthroughs.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_WithRasterImages_WritesThroughSink()
    {
        var model = new VisioDocumentModel(
            [new VisioPageModel("P", [new VisioShapeModel("1", "A")], [])],
            [new EmbeddedImage([1, 2, 3], "image/png", "photo", "/visio/media/image1.png")]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var image = Assert.Single(sink.Images);
        Assert.Equal("image/png", image.Hint.MediaType);
        Assert.Equal(ImageTransform.Passthrough, image.Hint.Transform);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a vector metafile is written through the sink in the encoding the drawing stored
    ///     it in, with nothing reported.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_VectorImage_WritesSourceEncodingWithoutNote()
    {
        var model = new VisioDocumentModel(
            [new VisioPageModel("P", [new VisioShapeModel("1", "A")], [])],
            [new EmbeddedImage([1, 2, 3], "image/x-emf", "schematic", "/visio/media/image1.emf")]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var image = Assert.Single(sink.Images);
        Assert.Equal("image/x-emf", image.Hint.MediaType);
        Assert.Equal(ImageTransform.Passthrough, image.Hint.Transform);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an empty drawing still writes empty content and reports zero-count inventory rather
    ///     than a special gap or diagnostic.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_EmptyDrawing_WritesEmptyContentAndZeroCountInventory()
    {
        var model = new VisioDocumentModel([]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Equal(string.Empty, Assert.Single(sink.ContentWrites));
        Assert.Equal(0, Assert.Single(sink.DocumentInfos).PageCount);
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "pages", Count: 0, LookedFor: true });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "labeled shapes", Count: 0, LookedFor: true });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "connections", Count: 0, LookedFor: true });
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a text-less endpoint instantiated from a named master is rendered by its type, and
    ///     visibly as a type rather than as a name someone authored.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_TextLessEndpointWithMaster_RendersTypeLabel()
    {
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, MixedLabelModel(), Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Inlet Tank \u2192 (Positive displacement, shape 2)", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an endpoint whose master describes the connector itself keeps the shape-id fallback,
    ///     so the topology never reports a wire as though it were equipment.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_ConnectiveMasterEndpoint_KeepsShapeIdLabel()
    {
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, MixedLabelModel(), Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("(shape 3)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Dynamic connector", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the labeling convention is stated in the content whenever a label is not the
    ///     shape's own text, so a human reading <c>content.md</c> alone cannot mistake a type for a name.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_NonTextLabelsPresent_StatesConventionInContent()
    {
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, MixedLabelModel(), Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("> " + VisioShapeLabeler.Convention, content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a page whose every endpoint carries its own text states no convention, because
    ///     there is no ambiguity to resolve and the note would be pure noise.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_AllEndpointsCarryText_OmitsConventionFromContent()
    {
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "Wash System",
                [new VisioShapeModel("1", "Inlet Tank"), new VisioShapeModel("2", "Transfer Pump")],
                [new VisioConnectionModel("1", "2")])
        ]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.DoesNotContain(VisioShapeLabeler.Convention, content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a drawing with no connections still reports a looked-for zero-connection
    ///     inventory line and omits the connections section from the content.
    /// </summary>
    [Fact]
    public async Task VisioContentEmitter_Emit_NoConnections_ReportsZeroConnectionInventory()
    {
        var model = new VisioDocumentModel([new VisioPageModel("P", [new VisioShapeModel("1", "A")], [])]);
        var sink = new RecordingSink();

        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.DoesNotContain("## Connections", content, StringComparison.Ordinal);
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "connections", Count: 0, LookedFor: true });
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves the single-flow <c>content.md</c> image link resolves on disk from the scratch root.
    /// </summary>
    /// <remarks>
    ///     A drawing single-flows into the root <c>content.md</c>, so its link must stay
    ///     root-relative and resolve unchanged.
    /// </remarks>
    [Fact]
    public async Task VisioContentEmitter_Emit_ImageLinks_ResolveOnDisk()
    {
        // Act: emit through the real sink and finalize the content document
        var resolved = await EmitAndResolveLinksAsync();

        // Assert: the root content.md image link resolved on disk
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Emits an image-bearing drawing through a real sink and content writer, then asserts every
    ///     inline image link resolves on disk.
    /// </summary>
    /// <returns>The number of local resource links that were checked and resolved.</returns>
    /// <remarks>
    ///     Uses a real <see cref="ExtractionSink"/> over a temporary scratch folder so the image bytes
    ///     and page part files reach disk and link resolution is genuine rather than a string check.
    /// </remarks>
    private static async Task<int> EmitAndResolveLinksAsync()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true };
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/visio/media/image1.png", AltText: "Diagram", SourcePages: [1]);
        var page = new VisioPageModel("Wash System", [new VisioShapeModel("1", "A")], [],
            [new VisioPageImageRef("/visio/media/image1.png", "Diagram")]);
        var model = new VisioDocumentModel([page], [image]);

        await VisioContentEmitter.EmitAsync(sink, options, model, Ct);
        await ContentWriter.WriteAsync(sink, "Drawing", Ct);

        return MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(folder.AbsolutePath);
    }

    /// <summary>
    ///     Proves multi-line shape text stays inside its own markdown list item, with nothing
    ///     truncated.
    /// </summary>
    /// <remarks>
    ///     Emitted flat, the continuation lines start at column zero and the list structure collapses,
    ///     so a renderer merges a pump's name with the part numbers beneath it. Those part numbers are
    ///     the reason the Visio text is worth extracting at all, so they must survive intact and stay
    ///     attached to the shape they belong to.
    /// </remarks>
    [Fact]
    public async Task VisioContentEmitter_Emit_MultiLineShapeText_StaysInsideItsListItem()
    {
        // Arrange: a shape whose text spans three lines, two of them part numbers
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "P",
                [new VisioShapeModel("1", "Transfer Pump\nModel TP-200 PN:11111-01\nInline Valve PN 2222-0")],
                [])
        ]);
        var sink = new RecordingSink();

        // Act: emit the drawing
        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: continuation lines are indented into the item and hard-broken, and nothing is lost
        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains(
            "- Transfer Pump  \n  Model TP-200 PN:11111-01  \n  Inline Valve PN 2222-0\n",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a shape whose text is a bare callout number is omitted, and the omission is counted
    ///     in the content rather than left silent.
    /// </summary>
    /// <remarks>
    ///     Listed as components, the bare callouts a drawing keys into a graphical legend read as
    ///     equipment named "1" — noise that is also actively misleading. Every label carrying a letter
    ///     (part numbers included) must survive.
    /// </remarks>
    [Fact]
    public async Task VisioContentEmitter_Emit_BareCalloutNumbers_OmittedAndCounted()
    {
        // Arrange: two callout numbers and one real part label
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "P",
                [
                    new VisioShapeModel("1", "1"),
                    new VisioShapeModel("2", "2"),
                    new VisioShapeModel("3", "P-441")
                ],
                [])
        ]);
        var sink = new RecordingSink();

        // Act: emit the drawing
        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: the part number survives, the callouts do not, and the omission is stated
        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("- P-441\n", content, StringComparison.Ordinal);
        Assert.DoesNotContain("- 1\n", content, StringComparison.Ordinal);
        Assert.Contains("> 2 shapes whose text is a bare callout number were omitted.", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an edge whose endpoints are both unidentified is omitted and counted, while every
    ///     edge with at least one meaningful endpoint is kept.
    /// </summary>
    /// <remarks>
    ///     <c>(shape 10) → (shape 11)</c> tells a reader nothing while consuming tokens. Reporting the
    ///     suppressed count keeps the omission visible rather than silent, which is the whole point of
    ///     the honesty contract.
    /// </remarks>
    [Fact]
    public async Task VisioContentEmitter_Emit_EdgeBetweenUnnamedShapes_OmittedAndCounted()
    {
        // Arrange: one edge with a named source and one edge between two anonymous shapes
        var model = new VisioDocumentModel(
        [
            new VisioPageModel(
                "P",
                [
                    new VisioShapeModel("1", "Source"),
                    new VisioShapeModel("2", null),
                    new VisioShapeModel("3", null),
                    new VisioShapeModel("4", null)
                ],
                [new VisioConnectionModel("1", "2"), new VisioConnectionModel("3", "4")])
        ]);
        var sink = new RecordingSink();

        // Act: emit the drawing
        await VisioContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: the meaningful edge survives, the anonymous one is counted, and the convention stays
        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Source \u2192 (shape 2)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("(shape 3) \u2192 (shape 4)", content, StringComparison.Ordinal);
        Assert.Contains("> 1 connection between unnamed shapes was omitted.", content, StringComparison.Ordinal);
        Assert.Contains("Topology endpoint labels:", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds a one-page model exercising every endpoint class at once: a shape with text, a
    ///     text-less shape with a component master, one with a connective master, one with no master,
    ///     and one whose master declares only an invariant name.
    /// </summary>
    /// <returns>The model.</returns>
    /// <remarks>Mirrors the mix a real engineering schematic carries, so one model pins the whole labeling policy.</remarks>
    private static VisioDocumentModel MixedLabelModel() => new(
    [
        new VisioPageModel(
            "Schematic",
            [
                new VisioShapeModel("1", "Inlet Tank", "Tank"),
                new VisioShapeModel("2", null, "Positive displacement"),
                new VisioShapeModel("3", null, "Dynamic connector"),
                new VisioShapeModel("4", null),
                new VisioShapeModel("5", null, "Cyclone 1")
            ],
            [
                new VisioConnectionModel("1", "2"),
                new VisioConnectionModel("2", "3"),
                new VisioConnectionModel("3", "4"),
                new VisioConnectionModel("4", "5")
            ])
    ]);
}
