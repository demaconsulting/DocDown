using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Word.Markdown;

namespace DemaConsulting.DocDown.Word.Tests.Markdown;

/// <summary>
///     Unit tests for <see cref="WordContentEmitter"/>, exercising the content outline, the
///     extraction-note reporting, and the metadata hand-off directly from hand-built models with no
///     document behind them.
/// </summary>
public class WordContentEmitterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a minimal body-only model with the supplied blocks and comments, leaving every
    ///     count at rest so a test only sets what it cares about.
    /// </summary>
    /// <param name="body">The body blocks.</param>
    /// <param name="comments">The document comments.</param>
    /// <param name="chartsFound">The number of embedded charts to declare.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/> for none.</param>
    /// <returns>The constructed model.</returns>
    private static WordDocumentModel Model(
        IReadOnlyList<WordBlock> body,
        IReadOnlyList<WordComment>? comments = null,
        int chartsFound = 0,
        DocumentMetadata? metadata = null) =>
        new(
            body,
            DocumentControl: [],
            Comments: comments ?? [],
            Footnotes: [],
            Title: "Draft Specification",
            Author: "Author Name",
            ProducerPageCount: null,
            TrackedChangeCount: 0,
            HeaderFooterPartsFound: 0,
            HeaderFooterPartsEmpty: 0,
            HeaderFooterPartsPageFurniture: 0,
            EmptyTablesSkipped: 0,
            ChartsFound: chartsFound,
            Metadata: metadata);

    /// <summary>
    ///     Proves a body carrying a heading and author-attributed comments writes content and reports
    ///     the outline features — headings, comments, and distinct comment authors — that make the
    ///     <c>## Comments</c> section discoverable, and reports no incomplete-step notes.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_BodyWithComments_ReportsOutlineAndSucceeds()
    {
        var model = Model(
            body:
            [
                new WordBlock(WordBlockKind.Heading, [new WordInline("Overview")], HeadingLevel: 1),
                new WordBlock(WordBlockKind.Paragraph, [new WordInline("First paragraph of the draft.")])
            ],
            comments:
            [
                new WordComment("Reviewer One", [new WordInline("Please clarify the scope.")]),
                new WordComment("Reviewer Two", [new WordInline("Agreed with the above.")])
            ]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Single(sink.ContentWrites);
        Assert.Single(sink.DocumentInfos);
        Assert.Empty(sink.Notes);
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "headings", Count: 1 });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "comments", Count: 2 });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "distinct comment authors", Count: 2 });
    }

    /// <summary>
    ///     Proves an unattributed comment counts toward the comment total but not toward the distinct
    ///     comment authors, because the document names nobody for it.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_UnattributedComment_NotCountedAsAuthor()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            comments:
            [
                new WordComment("Reviewer One", [new WordInline("A note.")]),
                new WordComment(null, [new WordInline("An anonymous note.")])
            ]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "comments", Count: 2 });
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "distinct comment authors", Count: 1 });
    }

    /// <summary>
    ///     Proves embedded charts the backend does not read are reported as a plain note, so the
    ///     chart's data never vanishes silently.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_ChartsFound_ReportsChartNote()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            chartsFound: 2);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var note = Assert.Single(sink.Notes);
        Assert.Contains("2 charts", note.Message, StringComparison.Ordinal);
        Assert.Contains("does not read chart parts", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the document's self-reported metadata is handed to the sink once when the model
    ///     carries it, so <c>metadata.json</c> receives the document's own claims.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_WithMetadata_ReportsDocumentMetadataOnce()
    {
        var metadata = new DocumentMetadata(
            [new DocumentMetadataField("title", "Draft Specification", MetadataProvenance.OpcCoreProperties)],
            AbsentInteresting: ["subject"]);
        var model = Model([new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])], metadata: metadata);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Same(metadata, Assert.Single(sink.DocumentMetadata));
    }

    /// <summary>
    ///     Proves a model that captured no metadata reports none, so <c>metadata.json</c> is left to
    ///     record the sparseness itself rather than receiving an invented record.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_NoMetadata_ReportsNoDocumentMetadata()
    {
        var model = Model([new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Empty(sink.DocumentMetadata);
    }

    /// <summary>
    ///     Proves the image hint always claims a byte-for-byte passthrough with unknown pixel
    ///     dimensions, because an Open XML image part stores a complete file and <c>wp:extent</c> is a
    ///     display size, not a pixel count.
    /// </summary>
    [Fact]
    public void WordContentEmitter_HintFor_ImageRef_ClaimsPassthroughWithUnknownDimensions()
    {
        var image = new WordImageRef(
            [1, 2, 3, 4], "image/png", PreferredName: "diagram", SourceRef: "/word/media/image1.png",
            Description: "System diagram", DescriptionSource: "description");

        var hint = WordContentEmitter.HintFor(image);

        Assert.Equal(ImageTransform.Passthrough, hint.Transform);
        Assert.Null(hint.WidthPx);
        Assert.Null(hint.HeightPx);
        Assert.Equal("/word/media/image1.png", hint.SourceRef);
        Assert.Equal("System diagram", hint.Description);
    }
}
