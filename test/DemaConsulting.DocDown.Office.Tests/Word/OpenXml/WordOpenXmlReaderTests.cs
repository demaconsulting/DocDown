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
