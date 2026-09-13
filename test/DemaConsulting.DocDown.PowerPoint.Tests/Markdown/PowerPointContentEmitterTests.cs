using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.PowerPoint.Markdown;
using DocDown.PowerPoint.OpenXml;

namespace DemaConsulting.DocDown.PowerPoint.Tests.Markdown;

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
    ///     Proves per-slide parts are written under the per-part split mode.
    /// </summary>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_PerPart_WritesSlideParts()
    {
        var model = new PowerPointDeckModel(
        [
            new PowerPointSlideModel(1, "A", ["a"], "n1"),
            new PowerPointSlideModel(2, "B", ["b"], "n2")
        ]);
        var sink = new RecordingSink();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false, ContentSplit = ContentSplitMode.PerPart };

        await PowerPointContentEmitter.EmitAsync(sink, options, model, Ct);

        Assert.Equal(2, sink.Parts.Count);
        Assert.All(sink.Parts, part => Assert.Equal(ContentPartKind.Slide, part.Part.Kind));
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
    ///     Proves that under <c>--split per-part</c> every inline image link a slide emits resolves on
    ///     disk from its own <c>parts/*.md</c> file, exercising the real sink and content writer.
    /// </summary>
    /// <remarks>
    ///     PerPart routes each slide into <c>parts/</c>, where the extractor's root-relative
    ///     <c>images/…</c> link would dangle without the write-path rewrite. This drives the real
    ///     <see cref="ExtractionSink"/> + <see cref="ContentWriter"/> over a temp scratch folder and
    ///     resolves each link against its containing file, catching exactly the defect string
    ///     assertions missed.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_PerPartImageLinks_ResolveOnDisk()
    {
        // Act: emit an image-bearing deck through the real sink and finalize as per-part files
        var resolved = await EmitAndResolveLinksAsync(ContentSplitMode.PerPart);

        // Assert: at least one image link was checked and all of them resolved on disk
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Proves that under the default Auto layout the single-flow <c>content.md</c> image links
    ///     resolve on disk from the scratch root.
    /// </summary>
    /// <remarks>
    ///     Auto single-flows the deck into the root <c>content.md</c>, so its links must remain
    ///     root-relative and resolve unchanged; this guards that the fix does not disturb the
    ///     already-correct root layout.
    /// </remarks>
    [Fact]
    public async Task PowerPointContentEmitter_Emit_AutoImageLinks_ResolveOnDisk()
    {
        // Act: emit through the real sink and finalize in the default Auto layout
        var resolved = await EmitAndResolveLinksAsync(ContentSplitMode.Auto);

        // Assert: the root content.md image link resolved on disk
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Emits an image-bearing deck through a real sink and content writer, then asserts every
    ///     inline image link resolves on disk.
    /// </summary>
    /// <param name="split">The content split mode to emit and finalize under.</param>
    /// <returns>The number of local resource links that were checked and resolved.</returns>
    /// <remarks>
    ///     Uses a real <see cref="ExtractionSink"/> over a temporary scratch folder (not the recording
    ///     sink) so the image bytes and part files actually reach disk and link resolution is genuine.
    /// </remarks>
    private static async Task<int> EmitAndResolveLinksAsync(ContentSplitMode split)
    {
        using var temp = new TempScratch();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true, ContentSplit = split };
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        var image = new EmbeddedImage([1, 2, 3, 4], "image/png",
            SourceRef: "/ppt/media/image1.png", AltText: "Company logo", SourcePages: [1]);
        var slide = new PowerPointSlideModel(1, "Illustrated", ["Body"], null,
            [new PowerPointSlideImageRef("/ppt/media/image1.png", "Company logo")]);
        var model = new PowerPointDeckModel([slide], [image]);

        await PowerPointContentEmitter.EmitAsync(sink, options, model, Ct);
        await ContentWriter.WriteAsync(sink, split, "Deck", Ct);

        return MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(folder.AbsolutePath);
    }

}
