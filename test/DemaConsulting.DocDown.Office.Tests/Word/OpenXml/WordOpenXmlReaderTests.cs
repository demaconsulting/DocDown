using System.Reflection;
using System.Text;
using DemaConsulting.DocDown.Office.Tests.Word.TestData;
using DocDown.Word.Markdown;
using DocDown.Word.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Word.OpenXml;

/// <summary>
///     Unit tests for <see cref="WordOpenXmlReader"/>, reading documents synthesized at test time.
/// </summary>
public class WordOpenXmlReaderTests
{
    /// <summary>
    ///     Proves headings, lists, and a real table become structured blocks.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks()
    {
        var model = Read(DocxFixtures.CleanDocument());

        Assert.Contains(model.Body, block => block.Kind == WordBlockKind.Heading && block.HeadingLevel == 1);
        Assert.Contains(model.Body, block => block is { Kind: WordBlockKind.ListItem, List.Ordered: false });
        var table = Assert.Single(model.Body, block => block.Kind == WordBlockKind.Table).Table!;
        Assert.True(table.FirstRowIsHeader);
        Assert.Equal(3, table.Rows.Count);
    }

    /// <summary>
    ///     Proves a header carrying a revision and classification produces a document-control section.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_HeaderWithRevisionAndClassification_ProducesDocumentControlSection()
    {
        var model = Read(DocxFixtures.EngineeringStyleDocument());

        var section = Assert.Single(model.DocumentControl, control => control.Label == "Header");
        Assert.Contains("Revision 2.1", BlocksText(section.Blocks), StringComparison.Ordinal);
        Assert.Contains("CONFIDENTIAL", BlocksText(section.Blocks), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a footer carrying only page-number fields is omitted and recorded as a furniture
    ///     count, without emitting a document-control section.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_FooterWithOnlyPageNumberFields_OmitsAsFurniture()
    {
        var model = Read(DocxFixtures.DocumentWithPageNumberFooterOnly());

        Assert.Empty(model.DocumentControl);
        Assert.Equal(1, model.HeaderFooterPartsPageFurniture);
        Assert.Equal(0, model.HeaderFooterPartsEmpty);
    }

    /// <summary>
    ///     Proves an identical header referenced by two sections is emitted once.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_IdenticalHeaderAcrossSections_EmittedOnce()
    {
        var model = Read(DocxFixtures.DocumentWithIdenticalHeaderAcrossSections());

        Assert.Single(model.DocumentControl);
        Assert.True(model.HeaderFooterPartsFound >= 2, "both section references should be found");
    }

    /// <summary>
    ///     Proves tracked changes are rendered in the accepted view and counted.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_TrackedChanges_RendersAcceptedViewWithDiagnostic()
    {
        var model = Read(DocxFixtures.DocumentWithTrackedChanges());

        var text = BlocksText(model.Body);
        Assert.Contains("inserted-text", text, StringComparison.Ordinal);
        Assert.DoesNotContain("removed-text", text, StringComparison.Ordinal);
        Assert.Equal(2, model.TrackedChangeCount);
    }

    /// <summary>
    ///     Proves a header row marked <c>w:tblHeader w:val="true"</c> — the spelling Word itself
    ///     writes, and the wider ST_OnOff spelling the Open XML SDK's typed accessor rejects — is
    ///     still recognized as a header row.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_HeaderRowValTrue_RecognizedAsHeaderRow()
    {
        var model = Read(DocxFixtures.DocumentWithTrueSpelledHeaderRow());

        var table = Assert.Single(model.Body, block => block.Kind == WordBlockKind.Table).Table!;
        Assert.True(table.FirstRowIsHeader);
    }

    /// <summary>
    ///     Proves a header row marked with a value outside the six ST_OnOff spellings is treated
    ///     as <see langword="false"/> rather than silently misreported as a header row.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_HeaderRowValMalformed_NotRecognizedAsHeaderRow()
    {
        var model = Read(DocxFixtures.DocumentWithMalformedHeaderRowValue());

        var table = Assert.Single(model.Body, block => block.Kind == WordBlockKind.Table).Table!;
        Assert.False(table.FirstRowIsHeader);
    }

    /// <summary>
    ///     Proves a password-protected document is detected and rejected with a clear exception.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_PasswordProtected_ThrowsWordExtractionException()
    {
        using var stream = new MemoryStream(DocxFixtures.PasswordProtected());

        var exception = Assert.Throws<WordExtractionException>(() => new WordOpenXmlReader().Read(stream));
        Assert.Contains("password-protected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves a comment is collected with its author and text.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_Comment_CollectsAuthorAndText()
    {
        var model = Read(DocxFixtures.DocumentWithComment());

        var comment = Assert.Single(model.Comments);
        Assert.Equal("Reviewer", comment.Author);
        Assert.Contains("clarify", string.Concat(comment.Content.Select(inline => inline.Text)), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a comment carrying no runs never enters the model, while a real comment in the
    ///     same document is unaffected.
    /// </summary>
    /// <remarks>
    ///     The model-level invariant every backend upholds is that a comment without text never
    ///     reaches it, so the one place downstream that can report a comment never has to decide
    ///     what an empty remark means. Asserting the surviving comment too keeps the guard from
    ///     being satisfied by dropping everything.
    /// </remarks>
    [Fact]
    public void WordOpenXmlReader_Read_EmptyComment_IsDroppedAndRealCommentSurvives()
    {
        var model = Read(DocxFixtures.DocumentWithEmptyAndRealComments());

        var comment = Assert.Single(model.Comments);
        Assert.Equal("Reviewer", comment.Author);

        // Nothing was lost, so nothing is counted as unreadable
        Assert.Equal(0, model.CommentsWithUnreadableContent);
    }

    /// <summary>
    ///     Proves a comment whose only content is a picture is dropped but counted, so the shortfall
    ///     can be reported rather than passing in silence.
    /// </summary>
    /// <remarks>
    ///     This is the case that separates "nothing was there" from "something was there that could
    ///     not be rendered": the reviewer did leave a remark, so its absence from the artifact is an
    ///     incomplete extraction step rather than a fact about the document.
    /// </remarks>
    [Fact]
    public void WordOpenXmlReader_Read_ImageOnlyComment_IsDroppedAndCounted()
    {
        var model = Read(DocxFixtures.DocumentWithImageOnlyComment());

        Assert.Empty(model.Comments);
        Assert.Equal(1, model.CommentsWithUnreadableContent);
    }

    /// <summary>
    ///     Proves a comment anchored over a run with range markers resolves a location hint naming
    ///     the nearest preceding heading and quoting the anchored text.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_AnchoredComment_LocationNamesHeadingAndSnippet()
    {
        var model = Read(DocxFixtures.DocumentWithComment());

        var comment = Assert.Single(model.Comments);
        Assert.Equal("§Overview — \"Body with a comment anchor.\"", comment.Location);
    }

    /// <summary>
    ///     Proves each comment's location resolves from its own anchor, and that a comment the
    ///     document anchors nowhere degrades to no location rather than borrowing another's.
    /// </summary>
    /// <remarks>
    ///     The three comments in the fixture are anchored three different ways — a bracketed range,
    ///     a bare reference point, and no markers at all — so a reader that resolved anchors by
    ///     position rather than by id, or that invented a location for the unanchored comment, fails
    ///     here.
    /// </remarks>
    [Fact]
    public void WordOpenXmlReader_Read_MultipleComments_LocationsResolveIndependently()
    {
        var model = Read(DocxFixtures.DocumentWithAnchoredComments());

        Assert.Equal(3, model.Comments.Count);

        // A bracketed range yields the heading and an ellipsized snippet of the anchored run
        Assert.StartsWith("§Scope — \"The quick brown fox", model.Comments[0].Location, StringComparison.Ordinal);
        Assert.EndsWith("…\"", model.Comments[0].Location, StringComparison.Ordinal);

        // A bare reference point brackets no text, so the hint degrades to the heading alone
        Assert.Equal("§Limitations", model.Comments[1].Location);

        // No anchor markers at all: no location is claimed
        Assert.Null(model.Comments[2].Location);
    }

    /// <summary>
    ///     Proves the anchor's bounded snippet accumulator cannot be grown past its stated limit by a
    ///     single large run, or by several runs appended across calls.
    /// </summary>
    /// <remarks>
    ///     The accumulator is a private implementation detail with no behavior-preserving way to
    ///     observe the bug from the public <see cref="WordDocumentModel.Comments"/> surface, because
    ///     <c>Snippet()</c> re-truncates to the same bound on render regardless of how much the
    ///     accumulator over-grew internally. The bound itself — not only the final rendered text — is
    ///     what the requirement states, so this test reaches the private
    ///     <c>WordOpenXmlReader.CommentAnchor</c> type through reflection, following this codebase's
    ///     existing precedent for asserting an internal invariant that the public surface cannot
    ///     exercise directly.
    /// </remarks>
    [Fact]
    public void WordOpenXmlReader_CommentAnchorAppendText_BoundsAccumulatorRegardlessOfRunSize()
    {
        // Arrange: construct the private CommentAnchor type via reflection
        var anchorType = typeof(WordOpenXmlReader).GetNestedType("CommentAnchor", BindingFlags.NonPublic)!;
        var constructor = anchorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var anchor = constructor.Invoke([null]);
        var appendText = anchorType.GetMethod("AppendText", BindingFlags.Instance | BindingFlags.Public)!;
        var textField = anchorType.GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Act: a single run far longer than the bound, then a second run that would extend it further
        appendText.Invoke(anchor, [new string('x', 500)]);
        appendText.Invoke(anchor, [new string('y', 50)]);

        // Assert: the accumulator never exceeds SnippetMaxLength (60) plus the one lookahead character
        var accumulated = (StringBuilder)textField.GetValue(anchor)!;
        Assert.Equal(61, accumulated.Length);
    }

    /// <summary>
    ///     Proves the producer-reported page count is read from the extended properties.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ProducerPageCount_FromExtendedProperties()
    {
        var model = Read(DocxFixtures.DocumentWithPageCount());

        Assert.Equal(7, model.ProducerPageCount);
    }

    /// <summary>
    ///     Proves the title and author metadata are surfaced from the core properties.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_Metadata_TitleAndAuthorSurfaced()
    {
        var model = Read(DocxFixtures.CleanDocument());

        Assert.Equal("Quarterly Report", model.Title);
        Assert.Equal("DocDown Test Suite", model.Author);
    }

    /// <summary>
    ///     Reads a document's bytes into the model.
    /// </summary>
    /// <param name="bytes">The document bytes.</param>
    /// <returns>The model.</returns>
    private static WordDocumentModel Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new WordOpenXmlReader().Read(stream);
    }

    /// <summary>
    ///     Proves an authored description becomes the image's naming hint, alt text, and manifest
    ///     description, with a <c>description</c> provenance.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithDescription_UsesDescriptionForNameAltAndManifest()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(description: "A wiring diagram"));

        var image = SingleImage(model);
        Assert.Equal("A wiring diagram", image.PreferredName);
        Assert.Equal("A wiring diagram", image.AltText);
        Assert.Equal("A wiring diagram", image.Description);
        Assert.Equal("description", image.DescriptionSource);
    }

    /// <summary>
    ///     Proves the title is used when there is no description.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithTitleOnly_UsesTitleAsDescriptive()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(title: "Assembly overview"));

        var image = SingleImage(model);
        Assert.Equal("Assembly overview", image.AltText);
        Assert.Equal("title", image.DescriptionSource);
    }

    /// <summary>
    ///     Proves a caption's figure number is read structurally from its <c>SEQ</c> field.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithCaption_ReadsSeqNumberStructurally()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(withCaption: true));

        var image = SingleImage(model);
        Assert.Equal("Figure 1: Widget assembly", image.AltText);
        Assert.Equal("caption", image.DescriptionSource);
    }

    /// <summary>
    ///     Proves a meaningful object name is used, while the auto-generated <c>wp:docPr/@name</c> is
    ///     never read.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithObjectName_UsesPictureNameNotDocPrName()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(pictureName: "blueprint-level-2"));

        var image = SingleImage(model);
        Assert.Equal("blueprint-level-2", image.AltText);
        Assert.Equal("pictureName", image.DescriptionSource);
    }

    /// <summary>
    ///     Proves an image with only a nearby heading is named from the heading but keeps neutral alt
    ///     text, recording the heading in the manifest with its source.
    /// </summary>
    /// <remarks>
    ///     This is the honesty case: the heading helps name the file, but presenting it as alt text
    ///     would imply a description the document never gave, so the alt text is left absent.
    /// </remarks>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithOnlyHeading_NamesFromHeadingButKeepsNeutralAlt()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(pictureName: "Picture 1", heading: "References"));

        var image = SingleImage(model);
        Assert.Equal("References", image.PreferredName);
        Assert.Null(image.AltText);
        Assert.Equal("References", image.Description);
        Assert.Equal("heading", image.DescriptionSource);
    }

    /// <summary>
    ///     Proves an image with no text sources falls back to the media name with no description.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_ImageWithNoSources_FallsBackToMediaNameWithoutDescription()
    {
        var model = Read(DocxFixtures.DocumentWithConfiguredImage(pictureName: "Picture 1"));

        var image = SingleImage(model);
        Assert.NotNull(image.PreferredName);
        Assert.Contains("image", image.PreferredName, StringComparison.OrdinalIgnoreCase);
        Assert.Null(image.AltText);
        Assert.Null(image.Description);
        Assert.Null(image.DescriptionSource);
    }

    /// <summary>
    ///     Selects the single image reference from a model's body.
    /// </summary>
    /// <param name="model">The model to inspect.</param>
    /// <returns>The single image reference.</returns>
    private static WordImageRef SingleImage(WordDocumentModel model) =>
        Assert.Single(model.Body
            .Where(block => block is { Kind: WordBlockKind.Image, Image: not null })
            .Select(block => block.Image!));

    /// <summary>
    ///     Concatenates the visible text of a block sequence.
    /// </summary>
    /// <param name="blocks">The blocks to flatten.</param>
    /// <returns>The concatenated text.</returns>
    private static string BlocksText(IEnumerable<WordBlock> blocks) =>
        string.Join(" ", blocks
            .Where(block => block.Inlines is not null)
            .Select(block => string.Concat(block.Inlines!.Select(inline => inline.Text))));
}
