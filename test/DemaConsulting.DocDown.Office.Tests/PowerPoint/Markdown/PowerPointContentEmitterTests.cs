using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.PowerPoint.Markdown;
using DocDown.PowerPoint.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint.Markdown;

/// <summary>
///     Unit tests for <see cref="PowerPointContentEmitter"/>, exercising the inventory and
///     plain-language note policy from hand-built models with no deck behind them.
/// </summary>
public class PowerPointContentEmitterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves an image the slide references is linked inline at its point of occurrence, using the
    ///     path the sink returned — following Word's convention.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_SlideImage_LinksInlineFromContent()
    {
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/ppt/media/image1.png", AltText: "Company logo", SourcePages: [1]);
        var slide = new PowerPointSlideModel(1, "Illustrated", ["Body"], null,
            [new PowerPointSlideImageRef("/ppt/media/image1.png", "Company logo")]);
        var model = new PowerPointDeckModel([slide], [image]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = true }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("![Company logo](images/0001-image.png)", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a deck carrying modern persona comments raises a note naming how many there are and
    ///     how many slides they sit on, so unread commentary is visible rather than silently absent.
    /// </summary>
    /// <remarks>
    ///     Without the note, a deck reviewed entirely in current PowerPoint produces output identical
    ///     to a deck nobody reviewed, and a reader has no way to learn that commentary exists. The
    ///     counts are asserted rather than just the presence of a note because a note that says
    ///     "some comments" would not tell a reader whether to go back to the source deck.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_ModernComments_ReportsNoteNamingTheCounts()
    {
        // Arrange: a deck whose two unread modern comments sit on a single slide
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(1, "Overview", ["First bullet"], null, [], [])],
            [],
            null,
            ModernCommentCount: 2,
            ModernCommentSlideCount: 1);
        var sink = new RecordingSink();

        // Act: emit the deck
        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: the note names both counts and says where the comments did not go
        var note = Assert.Single(sink.Notes, candidate => candidate.Message.Contains("modern", StringComparison.Ordinal));
        Assert.Contains("2 modern", note.Message, StringComparison.Ordinal);
        Assert.Contains("1 slide", note.Message, StringComparison.Ordinal);
        Assert.Contains("review-comments.md", note.Message, StringComparison.Ordinal);
        Assert.Empty(sink.ReviewComments);
    }

    /// <summary>
    ///     Proves a deck with no modern comments raises no note about them, so the note means
    ///     something when it does appear.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_NoModernComments_ReportsNoNote()
    {
        // Arrange: an ordinary deck with a legacy comment and no modern ones
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "Overview", ["First bullet"], null, [],
                [new PowerPointCommentModel("Dana Reyes", "Opening is too broad.")])
        ]);
        var sink = new RecordingSink();

        // Act: emit the deck
        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: nothing is said about commentary the deck does not carry
        Assert.DoesNotContain(sink.Notes, candidate => candidate.Message.Contains("modern", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves the content inventory names the deck's comments and how many distinct people wrote
    ///     them, so <c>summary.txt</c> can show a deck carries reviewer commentary at all.
    /// </summary>
    /// <remarks>
    ///     Comments leave <c>content.md</c> entirely, so without an inventory entry the summary gives
    ///     a reader no sign that a separate review-comments artifact exists. The unattributed comment
    ///     is excluded from the author count because the deck names nobody for it.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_SlideComments_InventoriesCommentsAndAuthors()
    {
        // Arrange: three comments across two slides, two by the same person and one unattributed
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "Overview", ["First bullet"], null, [],
            [
                new PowerPointCommentModel("Dana Reyes", "Opening is too broad."),
                new PowerPointCommentModel(null, "Cite the source for this figure.")
            ]),
            new PowerPointSlideModel(2, "Details", ["Detail line"], null, [],
                [new PowerPointCommentModel("Dana Reyes", "Tighten this claim.")])
        ]);
        var sink = new RecordingSink();

        // Act: emit the deck
        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: every comment is counted, but only the named author contributes to the author count
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "comments", Count: 3, LookedFor: true });
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature is { Label: "distinct comment authors", Count: 1, LookedFor: true });
    }

    /// <summary>
    ///     Proves each slide comment reaches the sink as a review comment located by the slide's
    ///     1-based ordinal, and that none of its text reaches the content flow.
    /// </summary>
    /// <remarks>
    ///     A reviewer's remark is commentary about the deck, not part of it; asserting both halves
    ///     together is what makes that separation falsifiable.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_SlideComments_ReportsSlideQualifiedReviewComments()
    {
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "Overview", ["First bullet"], null, [],
                [new PowerPointCommentModel("Dana Reyes", "Opening is too broad.")]),
            new PowerPointSlideModel(2, "Details", ["Detail line"], null, [],
                [new PowerPointCommentModel(null, "Cite the source for this figure.")])
        ]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Collection(
            sink.ReviewComments,
            first =>
            {
                Assert.Equal("Dana Reyes", first.Author);
                Assert.Equal("Opening is too broad.", first.Body);
                Assert.Equal("Slide 1", first.Location);
            },
            second =>
            {
                Assert.Null(second.Author);
                Assert.Equal("Cite the source for this figure.", second.Body);
                Assert.Equal("Slide 2", second.Location);
            });

        var content = Assert.Single(sink.ContentWrites);
        Assert.DoesNotContain("Opening is too broad", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a deck carrying no comments reports none, so the dedicated artifact is written only
    ///     where there is something to put in it.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_DeckWithoutComments_ReportsNoReviewComments()
    {
        var model = new PowerPointDeckModel([new PowerPointSlideModel(1, "Overview", ["Body"], null)]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Empty(sink.ReviewComments);
    }

    /// <summary>
    ///     Proves the deck's structure is reported as content features, including the speaker notes
    ///     that no rendered slide image can supply.
    /// </summary>
    /// <remarks>
    ///     A character count cannot tell a paste-in reader that the notes — the half of a deck that a
    ///     render loses — are present in the extracted text. The counts come from the model the
    ///     reader built, not from scanning rendered markdown.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_Deck_ReportsContentFeaturesIncludingSpeakerNotes()
    {
        // Arrange: a two-slide deck where only one slide carries notes and only one carries a title
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "Agenda", ["Body"], "Remember to mention the schedule."),
            new PowerPointSlideModel(2, null, ["More"], null)
        ]);
        var sink = new RecordingSink();

        // Act: emit the deck
        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        // Assert: the counts describe exactly what the model carried
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "slides" && feature.Count == 2);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "slide titles" && feature.Count == 1);
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "sets of speaker notes" && feature.Count == 1);
    }

    /// <summary>
    ///     Proves a slide references an image but leaves no dangling link when images are suppressed,
    ///     because the sink returned no path.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_ImagesSuppressed_NoDanglingLink()
    {
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/ppt/media/image1.png", AltText: "Company logo", SourcePages: [1]);
        var slide = new PowerPointSlideModel(1, "Illustrated", ["Body"], null,
            [new PowerPointSlideImageRef("/ppt/media/image1.png", "Company logo")]);
        var model = new PowerPointDeckModel([slide], [image]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.DoesNotContain("![", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves slide titles, body text, and speaker notes are all written to the content flow.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_WritesTitleBodyAndNotes()
    {
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(1, "Overview", ["A bullet"], "The narration.")]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Overview", content, StringComparison.Ordinal);
        Assert.Contains("A bullet", content, StringComparison.Ordinal);
        Assert.Contains("The narration.", content, StringComparison.Ordinal);
        Assert.Contains("Speaker notes", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a deck whose slides carry notes reports the notes in the content outline and
    ///     records no extraction note about that ordinary document content.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_NotesPresent_ReportsSpeakerNotesFeature()
    {
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(1, "T", ["b"], "notes here")]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Contains(
            sink.ContentFeatures,
            feature => feature is { Label: "sets of speaker notes", Count: 1, LookedFor: true });
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a deck with no speaker notes states the whole-deck absence as a counted zero in
    ///     the content outline — a fact about the deck — and records no extraction note, so a
    ///     notes-less deck never reads as incomplete.
    /// </summary>
    /// <remarks>
    ///     The zero is what lets a reader tell "we read every notes slide and there are none" from
    ///     "notes are not something this backend counts"; the retired PPTX0002 diagnostic said the
    ///     same thing as a judgement about the deck's content, which is not DocDown's role.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_NoNotes_ReportsZeroNotesFeatureWithoutNote()
    {
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "A", ["a"], null),
            new PowerPointSlideModel(2, "B", ["b"], null)
        ]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Contains(
            sink.ContentFeatures,
            feature => feature is { Label: "sets of speaker notes", Count: 0, LookedFor: true });
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves a default extraction of a deck that embeds no images records zero inline images
    ///     and stays silent about the absence.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_NoImages_ReportsZeroInlineImagesWithoutNote()
    {
        var model = new PowerPointDeckModel([new PowerPointSlideModel(1, "T", ["b"], "n")]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "inline images" && feature.Count == 0);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves the deck's embedded images are written through the sink as passthroughs and the
    ///     inline-image inventory reflects every linked raster image.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_WithRasterImages_WritesThroughSink()
    {
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(
                1, "T", ["b"], "n",
                [
                    new PowerPointSlideImageRef("/ppt/media/image1.png", "logo"),
                    new PowerPointSlideImageRef("/ppt/media/image2.jpeg", "chart")
                ])],
            [
                new EmbeddedImage([1, 2, 3], "image/png", "logo", "/ppt/media/image1.png"),
                new EmbeddedImage([4, 5, 6], "image/jpeg", "chart", "/ppt/media/image2.jpeg")
            ]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Equal(2, sink.Images.Count);
        Assert.All(sink.Images, image => Assert.Equal(ImageTransform.Passthrough, image.Hint.Transform));
        Assert.Contains(sink.ContentFeatures, feature => feature.Label == "inline images" && feature.Count == 2);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an EMF vector metafile is written unchanged with no vector-only extraction note,
    ///     because the bytes were produced exactly as stored.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_VectorImage_WritesWithoutNote()
    {
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(
                1, "T", ["b"], "n",
                [new PowerPointSlideImageRef("/ppt/media/image1.emf", "schematic")])],
            [new EmbeddedImage([1, 2, 3], "image/x-emf", "schematic", "/ppt/media/image1.emf")]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Single(sink.Images);
        Assert.Equal(ImageTransform.Passthrough, sink.Images[0].Hint.Transform);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves that when embedded images are disabled the backend writes none and reports no images
    ///     gap of its own — the engine records the deliberate suppression.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_ImagesDisabled_WritesNone()
    {
        var model = new PowerPointDeckModel(
            [new PowerPointSlideModel(1, "T", ["b"], "n")],
            [new EmbeddedImage([1, 2, 3], "image/png", "logo", "/ppt/media/image1.png")]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(
            sink, new ExtractionOptions { IncludeEmbeddedImages = false }, model, Ct);

        Assert.Empty(sink.Images);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an empty deck is emitted as empty content plus zero-count inventory, so the
    ///     absence is described as document content rather than an extraction failure.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_EmptyDeck_WritesEmptyContentAndZeroInventory()
    {
        var model = new PowerPointDeckModel([]);
        var sink = new RecordingSink();

        await PowerPointContentEmitter.EmitAsync(sink, new ExtractionOptions(), model, Ct);

        Assert.Equal(string.Empty, Assert.Single(sink.ContentWrites));
        var info = Assert.Single(sink.DocumentInfos);
        Assert.Equal(0, info.PageCount);
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "slides", Count: 0, LookedFor: true });
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature is { Label: "sets of speaker notes", Count: 0, LookedFor: true });
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves the single-flow <c>content.md</c> image links resolve on disk from the scratch root.
    /// </summary>
    /// <remarks>
    ///     A deck single-flows into the root <c>content.md</c>, so its links must remain
    ///     root-relative and resolve unchanged.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_ImageLinks_ResolveOnDisk()
    {
        // Act: emit through the real sink and finalize the content document
        var resolved = await EmitAndResolveLinksAsync();

        // Assert: the root content.md image link resolved on disk
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Emits an image-bearing deck through a real sink and content writer, then asserts every
    ///     inline image link resolves on disk.
    /// </summary>
    /// <returns>The number of local resource links that were checked and resolved.</returns>
    /// <remarks>
    ///     Uses a real <see cref="ExtractionSink"/> over a temporary scratch folder (not the recording
    ///     sink) so the image bytes and part files actually reach disk and link resolution is genuine.
    /// </remarks>
    private static async Task<int> EmitAndResolveLinksAsync()
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true };
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/ppt/media/image1.png", AltText: "Company logo", SourcePages: [1]);
        var slide = new PowerPointSlideModel(1, "Illustrated", ["Body"], null,
            [new PowerPointSlideImageRef("/ppt/media/image1.png", "Company logo")]);
        var model = new PowerPointDeckModel([slide], [image]);

        await PowerPointContentEmitter.EmitAsync(sink, options, model, Ct);
        await ContentWriter.WriteAsync(sink, "Deck", Ct);

        return MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(folder.AbsolutePath);
    }

}
