using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Word.Markdown;

namespace DemaConsulting.DocDown.Office.Tests.Word.Markdown;

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
    /// <param name="commentsWithUnreadableContent">The number of comments carrying only a picture or ink.</param>
    /// <returns>The constructed model.</returns>
    private static WordDocumentModel Model(
        IReadOnlyList<WordBlock> body,
        IReadOnlyList<WordComment>? comments = null,
        int chartsFound = 0,
        DocumentMetadata? metadata = null,
        int commentsWithUnreadableContent = 0) =>
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
            Metadata: metadata,
            CommentsWithUnreadableContent: commentsWithUnreadableContent);

    /// <summary>
    ///     Proves a body carrying a heading and author-attributed comments writes content and reports
    ///     the outline features — headings, comments, and distinct comment authors — that make the
    ///     reviewer commentary discoverable, and reports no incomplete-step notes.
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
    ///     Proves each comment reaches the sink as a review comment carrying its author, body as the
    ///     document records it, and location hint, and that none of that text reaches the content flow.
    /// </summary>
    /// <remarks>
    ///     This is the behavior that moved: a reviewer's remarks belong in the dedicated
    ///     review-comments artifact, not interleaved with the author's own words in
    ///     <c>content.md</c>. Asserting both halves together is what makes the move falsifiable.
    ///     The body is asserted literal, and asserted free of backslashes, because Core carries it
    ///     verbatim into <c>manifest.json</c>, whose contract is the text as the document records
    ///     it; markdown escaping belongs to the writer that emits markdown, not to this reporting
    ///     step. An earlier assertion here locked in the escaped form and with it the defect.
    /// </remarks>
    [Fact]
    public async Task WordContentEmitter_Emit_BodyWithComments_ReportsReviewComments()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("First paragraph of the draft.")])],
            comments:
            [
                new WordComment("Reviewer One", [new WordInline("Please clarify the scope.")], "§Scope — \"the draft\""),
                new WordComment("Reviewer Two", [new WordInline("Agreed with the above.")], "§Limitations")
            ]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Collection(
            sink.ReviewComments,
            first =>
            {
                Assert.Equal("Reviewer One", first.Author);
                Assert.Equal("Please clarify the scope.", first.Body);
                Assert.DoesNotContain("\\", first.Body, StringComparison.Ordinal);
                Assert.Equal("§Scope — \"the draft\"", first.Location);
            },
            second =>
            {
                Assert.Equal("Reviewer Two", second.Author);
                Assert.Equal("Agreed with the above.", second.Body);
                Assert.Equal("§Limitations", second.Location);
            });

        var content = Assert.Single(sink.ContentWrites);
        Assert.DoesNotContain("## Comments", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Please clarify the scope", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a comment body carrying markdown-significant characters reaches the sink exactly as
    ///     the document records it, with no escaping applied.
    /// </summary>
    /// <remarks>
    ///     A reviewer who wrote <c>*urgent*</c> wrote those asterisks. The manifest must record them,
    ///     and the review-comments writer — not this unit — decides what markdown needs escaping when
    ///     it writes the artifact.
    /// </remarks>
    [Fact]
    public async Task WordContentEmitter_Emit_CommentWithMarkdownCharacters_ReportsBodyUnescaped()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            comments: [new WordComment("Reviewer One", [new WordInline("This is *urgent* [see §4].")])]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var reported = Assert.Single(sink.ReviewComments);
        Assert.Equal("This is *urgent* [see §4].", reported.Body);
    }

    /// <summary>
    ///     Proves a comment whose runs reduce to whitespace is skipped rather than reported, and that
    ///     emitting such a model does not throw.
    /// </summary>
    /// <remarks>
    ///     The reader already drops these, so this is the second of two layers guarding the same
    ///     invariant. It is worth having and worth testing here because this unit is the only caller
    ///     of <c>ReportReviewComment</c> for Word, that method rejects a blank body by contract, and
    ///     a thrown exception from this point once turned a readable document into a failed
    ///     extraction with no <c>content.md</c> at all.
    /// </remarks>
    [Fact]
    public async Task WordContentEmitter_Emit_BlankComment_IsSkippedWithoutThrowing()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            comments:
            [
                new WordComment("Silent Reviewer", [new WordInline("   ")]),
                new WordComment("Reviewer One", [new WordInline("A real remark.")])
            ]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var reported = Assert.Single(sink.ReviewComments);
        Assert.Equal("Reviewer One", reported.Author);
        Assert.Single(sink.ContentWrites);
    }

    /// <summary>
    ///     Proves the emitter states, as a plain note, that comments carrying only a picture or ink
    ///     left no text in the review-comments artifact, and stays silent when there are none.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_ImageOnlyComments_ReportsNote()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            commentsWithUnreadableContent: 2);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var note = Assert.Single(sink.Notes);
        Assert.Contains("only a picture or ink", note.Message, StringComparison.Ordinal);
        Assert.Contains("2 comments", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a comment the document anchors nowhere is reported with a stated-unknown location
    ///     rather than an invented one.
    /// </summary>
    [Fact]
    public async Task WordContentEmitter_Emit_CommentWithoutLocation_ReportsLocationUnknown()
    {
        var model = Model(
            body: [new WordBlock(WordBlockKind.Paragraph, [new WordInline("Body text.")])],
            comments: [new WordComment("Reviewer One", [new WordInline("A note.")])]);
        var sink = new RecordingSink();

        await WordContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        var reported = Assert.Single(sink.ReviewComments);
        Assert.Equal("(location unknown)", reported.Location);
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
