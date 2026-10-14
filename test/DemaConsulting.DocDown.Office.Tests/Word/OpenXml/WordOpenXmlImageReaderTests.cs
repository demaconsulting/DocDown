using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Office.Tests.Word.TestData;
using DemaConsulting.DocDown.Word.Markdown;
using DemaConsulting.DocDown.Word.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace DemaConsulting.DocDown.Office.Tests.Word.OpenXml;

/// <summary>
///     Unit tests for <see cref="WordOpenXmlImageReader"/>, covering image provenance.
/// </summary>
public class WordOpenXmlImageReaderTests
{
    /// <summary>
    ///     Proves a PNG image is resolved as a passthrough with unknown pixel dimensions.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions()
    {
        using var document = Open(DocxFixtures.DocumentWithImage());

        var (image, hint) = ResolveFirstImage(document);

        Assert.Equal(ImageTransform.Passthrough, hint.Transform);
        Assert.Null(hint.WidthPx);
        Assert.Null(hint.HeightPx);
        Assert.Equal("image/png", hint.MediaType);
        Assert.Equal(DocxFixtures.PngBytes(), image.Bytes);
    }

    /// <summary>
    ///     Proves an EMF image reports a vector media type so the caller can caveat it.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Read_Emf_ReportsVectorMediaType()
    {
        using var document = Open(DocxFixtures.DocumentWithVectorImage());

        var (_, hint) = ResolveFirstImage(document);

        Assert.Contains("emf", hint.MediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves a null container or null candidate list is rejected.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Resolve_NullArguments_Throw()
    {
        using var document = Open(DocxFixtures.DocumentWithImage());
        var mainPart = document.MainDocumentPart!;

        Assert.Throws<ArgumentNullException>(() => WordOpenXmlImageReader.Resolve(null!, "rId1", []));
        Assert.Throws<ArgumentNullException>(() => WordOpenXmlImageReader.Resolve(mainPart, "rId1", null!));
    }

    /// <summary>
    ///     Proves a missing relationship id, or one that names a non-image part, yields no reference.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Resolve_MissingOrNonImageRelationship_ReturnsNull()
    {
        using var document = Open(DocxFixtures.DocumentWithHeaderLogo());
        var mainPart = document.MainDocumentPart!;
        var headerId = mainPart.GetIdOfPart(mainPart.HeaderParts.First());

        Assert.Null(WordOpenXmlImageReader.Resolve(mainPart, null, []));
        Assert.Null(WordOpenXmlImageReader.Resolve(mainPart, string.Empty, []));
        Assert.Null(WordOpenXmlImageReader.Resolve(mainPart, headerId, []));
    }

    /// <summary>
    ///     Proves an image scoped to a header part resolves against that part, not the main part.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Resolve_HeaderScopedImage_ResolvesAgainstHeaderPart()
    {
        using var document = Open(DocxFixtures.DocumentWithHeaderLogo());
        var headerPart = document.MainDocumentPart!.HeaderParts.First();
        var relationshipId = headerPart.GetIdOfPart(headerPart.ImageParts.First());

        var image = WordOpenXmlImageReader.Resolve(headerPart, relationshipId, []);

        Assert.NotNull(image);
        Assert.Equal(DocxFixtures.PngBytes(), image.Bytes);
    }

    /// <summary>
    ///     Proves a descriptive candidate outranks the media name and becomes alt text and description,
    ///     while a contextual candidate names the file but is never asserted as alt text.
    /// </summary>
    [Fact]
    public void WordOpenXmlImageReader_Resolve_Candidates_RankAndGateAltText()
    {
        using var document = Open(DocxFixtures.DocumentWithImage());
        var mainPart = document.MainDocumentPart!;
        var relationshipId = mainPart.GetIdOfPart(mainPart.ImageParts.First());

        var described = WordOpenXmlImageReader.Resolve(
            mainPart, relationshipId, [new ImageTextCandidate("Wiring", ImageTextSource.Description)]);
        var contextual = WordOpenXmlImageReader.Resolve(
            mainPart, relationshipId, [new ImageTextCandidate("Scope", ImageTextSource.Heading)]);
        var bare = WordOpenXmlImageReader.Resolve(mainPart, relationshipId, []);

        Assert.Equal("Wiring", described!.AltText);
        Assert.Equal("description", described.DescriptionSource);
        Assert.Equal("Scope", contextual!.PreferredName);
        Assert.Null(contextual.AltText);
        Assert.Equal("heading", contextual.DescriptionSource);
        Assert.NotNull(bare!.PreferredName);
        Assert.Null(bare.Description);
        Assert.Equal(mainPart.ImageParts.First().Uri.ToString(), bare.SourceRef);
    }

    /// <summary>
    ///     Resolves the document's first image part through the production path.
    /// </summary>
    /// <param name="document">The opened document.</param>
    /// <returns>The resolved reference and the hint the emitter would add it with.</returns>
    /// <remarks>
    ///     Goes through <c>Resolve</c> and <c>HintFor</c> — the same two calls the reader and the
    ///     emitter make — so the provenance asserted here is the provenance a real extraction
    ///     produces, not a parallel enumeration maintained only for tests.
    /// </remarks>
    private static (WordImageRef Image, ImageHint Hint) ResolveFirstImage(WordprocessingDocument document)
    {
        var mainPart = document.MainDocumentPart!;
        var relationshipId = mainPart.GetIdOfPart(mainPart.ImageParts.First());
        var image = WordOpenXmlImageReader.Resolve(mainPart, relationshipId, []);
        Assert.NotNull(image);
        return (image, WordContentEmitter.HintFor(image));
    }

    /// <summary>
    ///     Opens a document from its bytes.
    /// </summary>
    /// <param name="bytes">The document bytes.</param>
    /// <returns>The opened document.</returns>
    private static WordprocessingDocument Open(byte[] bytes) =>
        WordprocessingDocument.Open(new MemoryStream(bytes), false);
}
