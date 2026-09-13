using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DemaConsulting.DocDown.Tool.Tests.TestData;

/// <summary>
///     Builds the PDF documents the tool tests drive the CLI against, at test time, from PdfPig's
///     document writer.
/// </summary>
/// <remarks>
///     No binary PDF is committed to this repository. The tool tests need only a single, valid,
///     text-bearing PDF to exercise the end-to-end extraction path, so this helper is deliberately
///     minimal — the exhaustive fixture set lives in the PDF test project. All members are static
///     and safe for concurrent use.
/// </remarks>
internal static class ToolPdfFixtures
{
    /// <summary>
    ///     Builds a one-page PDF carrying a short paragraph of text and no images.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    public static byte[] SimpleText()
    {
        var builder = new PdfDocumentBuilder { IncludeDocumentInformation = true };
        builder.DocumentInformation.Title = "DocDown Tool Fixture";
        builder.DocumentInformation.Author = "DocDown Test Suite";
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Introduction", 18, new PdfPoint(40, 700), font);
        page.AddText("The quick brown fox jumps over the lazy dog.", 12, new PdfPoint(40, 660), font);
        return builder.Build();
    }
}
