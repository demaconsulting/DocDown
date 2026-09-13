using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;

/// <summary>
///     Builds every PDF the rendering test suite needs, at test time, from PdfPig's document writer.
/// </summary>
/// <remarks>
///     <para>
///         No binary PDF is committed to this repository. Each fixture is synthesized here when the
///         test that needs it runs, which keeps the repository text-only, removes any question about
///         the provenance or licensing of a checked-in sample document, and makes the exact content
///         of every fixture readable in the same place it is asserted against.
///     </para>
///     <para>
///         These documents are deliberately built the same way the managed PDF test suite builds its
///         fixtures, so the pages this suite rasterizes are the same pages the base backend extracts
///         text from. All members are static and pure apart from their allocations, and are safe for
///         concurrent use.
///     </para>
/// </remarks>
public static class RenderingFixtures
{
    /// <summary>
    ///     Builds a one-page A4 PDF carrying a short line of text and no images.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>The anchor fixture for the single-page render, determinism, and PNG-validity scenarios.</remarks>
    public static byte[] SimpleText()
    {
        using var builder = NewBuilder("DocDown Rendering Simple Text", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Introduction", 18, new PdfPoint(40, 700), font);
        page.AddText("The quick brown fox jumps over the lazy dog.", 12, new PdfPoint(40, 660), font);
        return builder.Build();
    }

    /// <summary>
    ///     Builds a multi-page A4 PDF whose pages carry distinguishable text.
    /// </summary>
    /// <param name="pageCount">The number of pages to build. Must be at least one.</param>
    /// <returns>The bytes of the built document.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageCount"/> is less than one.</exception>
    /// <remarks>
    ///     Each page's text names its own page number, so the page-range scenario can assert that
    ///     only the requested pages were rasterized.
    /// </remarks>
    public static byte[] MultiPage(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);

        using var builder = NewBuilder("DocDown Rendering Multi Page", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var number = 1; number <= pageCount; number++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"Page {number} marker text", 12, new PdfPoint(40, 700), font);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a document builder with document-information metadata populated.
    /// </summary>
    /// <param name="title">The document title to record.</param>
    /// <param name="author">The document author to record.</param>
    /// <returns>A configured <see cref="PdfDocumentBuilder"/> the caller disposes.</returns>
    /// <remarks>Mirrors the managed PDF suite's builder so both suites produce comparable documents.</remarks>
    private static PdfDocumentBuilder NewBuilder(string title, string author)
    {
        var builder = new PdfDocumentBuilder { IncludeDocumentInformation = true };
        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;
        return builder;
    }
}
