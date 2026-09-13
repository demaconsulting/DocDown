using DemaConsulting.DocDown.Pdf.Tests.TestData;
using UglyToad.PdfPig.Content;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for the PDF text extractor, proving reading order, the prohibition on raw page
///     text, image-link placement, page markers, and the honest no-glyph signal.
/// </summary>
/// <remarks>
///     Text extraction is heuristic, so these tests assert properties of the markdown — expected
///     substrings, the relative order of known markers, the presence of links and page boundaries —
///     rather than golden output, which would be brittle against harmless segmentation changes.
///     The unit is driven directly against pages from the generated fixtures.
/// </remarks>
public class PdfTextExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a single page's text is rendered as markdown paragraphs (ReadingOrder).
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_SingleTextPage_RendersParagraphsInReadingOrder()
    {
        // Arrange: a one-page document with a heading line above a body line
        using var document = PdfFixtures.Open(PdfFixtures.SimpleText());
        var pages = document.GetPages().ToList();

        // Act: render the page
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, "Simple", [], Ct);

        // Assert: both lines are present and the upper one comes first, as a reader would read them
        Assert.True(result.AnyGlyphs);
        var heading = result.Markdown.IndexOf("Introduction", StringComparison.Ordinal);
        var body = result.Markdown.IndexOf("quick brown fox", StringComparison.Ordinal);
        Assert.True(heading >= 0 && body > heading, "the page text is missing or out of reading order");
    }

    /// <summary>
    ///     Proves the extractor does not simply emit PdfPig's raw page text (AvoidsRawPageText).
    /// </summary>
    /// <remarks>
    ///     PdfPig's own documentation warns against <c>Page.Text</c> because it is content-stream draw
    ///     order, not reading order, and it concatenates glyphs with no word or paragraph boundaries.
    ///     Asserting that the output differs from it — while still containing the same words — is what
    ///     makes the prohibition testable rather than merely stated in a design document.
    /// </remarks>
    [Fact]
    public void PdfTextExtractor_Extract_AnyPage_ProducesStructureRawPageTextDoesNot()
    {
        // Arrange: a page whose raw text is a single undifferentiated run
        using var document = PdfFixtures.Open(PdfFixtures.SimpleText());
        var pages = document.GetPages().ToList();
        var rawPageText = pages[0].Text;

        // Act: render the page through the structured pipeline
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, [], Ct);

        // Assert: the output is not the raw concatenation, yet carries the same words
        Assert.NotEqual(rawPageText, result.Markdown);
        Assert.DoesNotContain(rawPageText, result.Markdown, StringComparison.Ordinal);
        Assert.Contains("Introduction", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("quick brown fox", result.Markdown, StringComparison.Ordinal);

        // Assert: and it carries word separation the raw run lacks
        Assert.Contains(' ', result.Markdown);
    }

    /// <summary>
    ///     Proves every page is marked so a passage can be traced back to its source page.
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_MultiplePages_EmitsAPageMarkerPerPage()
    {
        // Arrange: a three-page document
        using var document = PdfFixtures.Open(PdfFixtures.MultiPage(3));
        var pages = document.GetPages().ToList();

        // Act: render every page
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, [], Ct);

        // Assert: each page contributes its own marker, in order
        Assert.Contains("<!-- docdown:page 1 -->", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("<!-- docdown:page 2 -->", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("<!-- docdown:page 3 -->", result.Markdown, StringComparison.Ordinal);
        Assert.True(result.Markdown.IndexOf("page 1", StringComparison.Ordinal)
            < result.Markdown.IndexOf("page 3", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves the document title heads the output when the PDF declares one.
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_DocumentTitle_HeadsTheMarkdown()
    {
        // Arrange: a single page and a declared document title
        using var document = PdfFixtures.Open(PdfFixtures.SimpleText());
        var pages = document.GetPages().ToList();

        // Act: render with the title supplied
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, "Quarterly Report", [], Ct);

        // Assert: the title is the document's first heading, so content.md is self-identifying
        Assert.StartsWith("# Quarterly Report", result.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves image links are substituted under the page they came from (LinksImages).
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_ExtractedImages_LinksThemUnderTheirOwnPage()
    {
        // Arrange: a two-page document and one image attributed to the second page
        using var document = PdfFixtures.Open(PdfFixtures.MultiPage(2));
        var pages = document.GetPages().ToList();
        var images = new[] { new global::DocDown.Pdf.PdfExtractedImage(2, "images/0001-figure.png", "page 2 image 1") };

        // Act: render with the image supplied
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, images, Ct);

        // Assert: the link uses the path the sink allocated, and sits after its own page's marker
        Assert.Contains("![page 2 image 1](images/0001-figure.png)", result.Markdown, StringComparison.Ordinal);
        var pageTwoMarker = result.Markdown.IndexOf("<!-- docdown:page 2 -->", StringComparison.Ordinal);
        var link = result.Markdown.IndexOf("images/0001-figure.png", StringComparison.Ordinal);
        Assert.True(pageTwoMarker >= 0 && link > pageTwoMarker, "the image link is not under its own page");
    }

    /// <summary>
    ///     Proves a page with no glyphs is reported honestly rather than silently emptied (ReportsNoTextLayer).
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_PageWithNoGlyphs_ReportsNoGlyphsPresent()
    {
        // Arrange: a purely graphical page
        using var document = PdfFixtures.Open(PdfFixtures.NoTextLayer());
        var pages = document.GetPages().ToList();

        // Act: render the page
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, [], Ct);

        // Assert: the absence of a text layer is signalled as a fact the caller can explain
        Assert.False(result.AnyGlyphs);
        Assert.DoesNotContain("quick brown fox", result.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an untagged document degrades to plain paragraphs rather than guessed headings.
    /// </summary>
    /// <remarks>
    ///     Untagged PDFs are the overwhelmingly common case. Guessing headings from font size would
    ///     produce a confident structure the document never declared, which is worse for a downstream
    ///     consumer than a flat one — so the fallback is deliberate, and asserted.
    /// </remarks>
    [Fact]
    public void PdfTextExtractor_Extract_UntaggedDocument_EmitsParagraphsWithoutInventedHeadings()
    {
        // Arrange: an untagged document, confirmed to carry no marked-content structure
        using var document = PdfFixtures.Open(PdfFixtures.SimpleText());
        var pages = document.GetPages().ToList();
        Assert.Empty(pages[0].GetMarkedContents());

        // Act: render without a document title, so any heading present must have been inferred
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, [], Ct);

        // Assert: the text is present but no heading was invented for it
        Assert.Contains("Introduction", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("# Introduction", result.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an empty page list yields empty output without throwing (boundary).
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_NoPages_ProducesNoGlyphsAndNoMarkers()
    {
        // Arrange: no pages at all, as a zero-page document produces
        var pages = Array.Empty<Page>();

        // Act: render nothing
        var result = global::DocDown.Pdf.PdfTextExtractor.Extract(pages, null, [], Ct);

        // Assert: the result is empty and honest rather than an exception
        Assert.False(result.AnyGlyphs);
        Assert.Equal(string.Empty, result.Markdown);
    }

    /// <summary>
    ///     Proves a null page list is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public void PdfTextExtractor_Extract_NullPages_ThrowsArgumentNullException()
    {
        // Act + Assert: the pages are the unit's only input and are mandatory
        Assert.Throws<ArgumentNullException>(
            () => global::DocDown.Pdf.PdfTextExtractor.Extract(null!, null, [], Ct));
    }
}
