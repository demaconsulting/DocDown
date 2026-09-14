using DemaConsulting.DocDown.Office.Tests.Word.TestData;
using DocDown.Core;
using DocDown.Word.Markdown;
using DocDown.Word.OpenXml;
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
