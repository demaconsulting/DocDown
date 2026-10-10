using DemaConsulting.DocDown.Office.Tests.PowerPoint.TestData;
using DemaConsulting.DocDown.PowerPoint.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint.OpenXml;

/// <summary>
///     Unit tests for <see cref="PowerPointOpenXmlReader"/>, exercising slide order, titles, body
///     text, and speaker-note reading against decks synthesized at test time.
/// </summary>
public class PowerPointOpenXmlReaderTests
{
    /// <summary>
    ///     Proves the reader resolves an embedded picture to an image part carrying its bytes, media
    ///     type, and the authored description as its naming source.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithImage_ExtractsEmbeddedImage()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithImage());

        var model = PowerPointOpenXmlReader.Read(stream);

        var image = Assert.Single(model.Images);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(8, image.Bytes.Length);
        Assert.Equal("Company logo", image.Description);
    }

    /// <summary>
    ///     Proves the reader records the slide that references an embedded image in its referrer set
    ///     and exposes the image as a per-slide reference for inline linking — never a template flag or
    ///     a fabricated orphan for a real slide picture.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithImage_RecordsSlideAssociation()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithImage());

        var model = PowerPointOpenXmlReader.Read(stream);

        var image = Assert.Single(model.Images);
        Assert.Equal([1], image.SourcePages);
        Assert.Equal(1, image.SourcePage);
        Assert.False(image.ReferencedByTemplate);

        // The slide exposes the image as an inline reference keyed by the same part URI
        var slide = Assert.Single(model.Slides);
        var reference = Assert.Single(slide.Images);
        Assert.Equal(image.SourceRef, reference.SourceRef);
    }

    /// <summary>
    ///     Proves the reader returns slides in presentation order with their 1-based ordinals.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithNotes_ReturnsSlidesInOrder()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithNotes());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Equal(2, model.Slides.Count);
        Assert.Equal(1, model.Slides[0].Ordinal);
        Assert.Equal(2, model.Slides[1].Ordinal);
        Assert.Equal("Overview", model.Slides[0].Title);
        Assert.Equal("Details", model.Slides[1].Title);
    }

    /// <summary>
    ///     Proves the reader extracts a slide's body text lines, separate from its title.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_Slide_ExtractsBodyTextLines()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithNotes());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Contains("First bullet", model.Slides[0].TextLines);
        Assert.Contains("Second bullet", model.Slides[0].TextLines);
        Assert.DoesNotContain("Overview", model.Slides[0].TextLines);
    }

    /// <summary>
    ///     Proves the reader extracts the speaker notes that never appear in any render.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_Slide_ExtractsSpeakerNotes()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithNotes());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Equal("Say this out loud on slide one.", model.Slides[0].Notes);
        Assert.Equal("Remember the caveat on slide two.", model.Slides[1].Notes);
    }

    /// <summary>
    ///     Proves a deck with no notes yields slides whose notes are null.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithoutNotes_YieldsNullNotes()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithoutNotes());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.All(model.Slides, slide => Assert.Null(slide.Notes));
    }

    /// <summary>
    ///     Proves a legacy slide comment is read with its author resolved through the presentation's
    ///     comment-author list, rather than left as the bare identifier the comment carries.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithComment_ReadsCommentWithResolvedAuthor()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithComment());

        var model = PowerPointOpenXmlReader.Read(stream);

        var comment = Assert.Single(Assert.Single(model.Slides).Comments);
        Assert.Equal("Dana Reyes", comment.Author);
        Assert.Equal("Tighten this claim before the review board sees it.", comment.Text);
    }

    /// <summary>
    ///     Proves a deck whose only comments are modern persona comments reports no comments but does
    ///     count them, so an unread remark is never mistaken for an absent one.
    /// </summary>
    /// <remarks>
    ///     Reading modern comments is outside this feature's scope, because resolving a persona to a
    ///     name needs the person list and a validation pass neither of which this unit has. Saying
    ///     nothing at all would be the worse failure: a reviewer who wrote three comments in current
    ///     PowerPoint would see an output indistinguishable from one for a deck nobody reviewed. So
    ///     the reader counts what it will not read, and the emitter turns that count into a note.
    /// </remarks>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithModernComments_CountsThemWithoutReadingThem()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithModernComments());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Empty(Assert.Single(model.Slides).Comments);
        Assert.Equal(1, model.ModernCommentCount);
        Assert.Equal(1, model.ModernCommentSlideCount);
    }

    /// <summary>
    ///     Proves a deck with only legacy comments reports no modern comments, so the note that
    ///     announces unread commentary stays silent when there is none.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithComment_CountsNoModernComments()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithComment());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Equal(0, model.ModernCommentCount);
        Assert.Equal(0, model.ModernCommentSlideCount);
    }

    /// <summary>
    ///     Proves each comment stays with the slide that carries it, so a remark on one slide is
    ///     never reported against another.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithCommentsOnTwoSlides_AttributesCommentsToOwningSlide()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithCommentsOnTwoSlides());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.Equal(2, model.Slides.Count);

        var first = Assert.Single(model.Slides[0].Comments);
        Assert.Equal(1, model.Slides[0].Ordinal);
        Assert.Equal("Dana Reyes", first.Author);
        Assert.Equal("Opening is too broad.", first.Text);

        var second = Assert.Single(model.Slides[1].Comments);
        Assert.Equal(2, model.Slides[1].Ordinal);
        Assert.Equal("Sam Whitfield", second.Author);
        Assert.Equal("Cite the source for this figure.", second.Text);
    }

    /// <summary>
    ///     Proves a deck with no comments part yields slides carrying no comments rather than an
    ///     error. This is also the shape a deck whose comments are all modern persona comments takes,
    ///     which this reader states plainly it does not read.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlReader_Read_DeckWithoutComments_YieldsNoComments()
    {
        using var stream = new MemoryStream(PptxFixtures.DeckWithNotes());

        var model = PowerPointOpenXmlReader.Read(stream);

        Assert.All(model.Slides, slide => Assert.Empty(slide.Comments));
    }
}
