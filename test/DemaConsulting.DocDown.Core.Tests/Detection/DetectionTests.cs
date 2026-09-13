using System.Text;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Detection;

/// <summary>
///     Subsystem-integration tests for the Detection subsystem, exercising
///     <see cref="FormatSniffer"/> at the subsystem boundary.
/// </summary>
/// <remarks>
///     These tests use only Detection units. Each is named for the subsystem requirement it
///     evidences: format identification (extension-primary, with a content-signature fallback),
///     detection-basis reporting, unknown-format reporting, and non-destructive inspection.
/// </remarks>
public class DetectionTests
{
    /// <summary>
    ///     Proves the sniffer identifies a PDF from its file name (FormatIdentification).
    /// </summary>
    [Fact]
    public void Detection_FormatIdentification_PdfSignature_IdentifiesPdf()
    {
        // Arrange: a stream whose leading bytes carry the PDF signature, named as a PDF
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nbody"));

        // Act: sniff the format
        var detection = FormatSniffer.Detect(stream, "document.pdf");

        // Assert: the PDF format is identified
        Assert.Equal(DocumentFormat.Pdf, detection.Format);
    }

    /// <summary>
    ///     Proves the sniffer identifies HTML from its content signature when unnamed (FormatIdentification).
    /// </summary>
    [Fact]
    public void Detection_FormatIdentification_HtmlSignature_IdentifiesHtml()
    {
        // Arrange: a stream whose content opens with an HTML doctype
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>hi</body></html>"));

        // Act: sniff the format with no file name, so the content fallback is the only evidence
        var detection = FormatSniffer.Detect(stream, null);

        // Assert: the HTML format is identified from the content
        Assert.Equal(DocumentFormat.Html, detection.Format);
    }

    /// <summary>
    ///     Proves the sniffer identifies plain text from the file extension (FormatIdentification).
    /// </summary>
    [Fact]
    public void Detection_FormatIdentification_TextExtension_IdentifiesText()
    {
        // Arrange: plain content with no signature but a text file name
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("just some plain notes"));

        // Act: sniff the format using the primary extension signal
        var detection = FormatSniffer.Detect(stream, "notes.txt");

        // Assert: the text format is identified from the extension
        Assert.Equal(DocumentFormat.Text, detection.Format);
    }

    /// <summary>
    ///     Proves a Word document is recognized by its extension without opening the file (FormatIdentification).
    /// </summary>
    [Fact]
    public void Detection_FormatIdentification_DocxExtension_IdentifiesDocxByExtension()
    {
        // Arrange: arbitrary bytes that are not a package at all, named as a Word document
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not really a package"));

        // Act: sniff the format; detection never opens the document to check
        var detection = FormatSniffer.Detect(stream, "report.docx");

        // Assert: docx is named from the extension alone, at the extension basis
        Assert.Equal(DocumentFormat.Docx, detection.Format);
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
    }

    /// <summary>
    ///     Proves the sniffer reports the extension basis when the name identified the format (DetectionBasisReported).
    /// </summary>
    [Fact]
    public void Detection_DetectionBasisReported_PdfExtension_ReportsExtensionBasis()
    {
        // Arrange: a stream carrying the PDF signature and a matching name
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nbody"));

        // Act: sniff the format
        var detection = FormatSniffer.Detect(stream, "document.pdf");

        // Assert: the recorded basis is the trusted extension, at the documented confidence
        Assert.Equal(DetectionBasis.Extension, detection.Basis);
        Assert.Equal(0.9, detection.Confidence);
    }

    /// <summary>
    ///     Proves the sniffer reports the content-signature basis when the name yielded nothing (DetectionBasisReported).
    /// </summary>
    [Fact]
    public void Detection_DetectionBasisReported_PdfSignature_ReportsContentSignatureBasis()
    {
        // Arrange: a stream carrying the PDF signature behind an unrecognized name
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nbody"));

        // Act: sniff the format
        var detection = FormatSniffer.Detect(stream, "document.bin");

        // Assert: the basis records that the bytes, not the name, identified the format
        Assert.Equal(DetectionBasis.ContentSignature, detection.Basis);
        Assert.Equal(1.0, detection.Confidence);
    }

    /// <summary>
    ///     Proves the sniffer reports an unrecognized document as unknown (UnknownFormatReported).
    /// </summary>
    [Fact]
    public void Detection_UnknownFormatReported_UnrecognizedContent_ReportsUnknown()
    {
        // Arrange: bytes with no signature and a file name with no known extension
        using var stream = new MemoryStream([0x00, 0x11, 0x22, 0x33, 0x44, 0x55]);

        // Act: sniff the unrecognized content
        var detection = FormatSniffer.Detect(stream, "mystery.dat");

        // Assert: the format is reported as unknown with zero confidence
        Assert.True(detection.Format.IsUnknown);
        Assert.Equal(0.0, detection.Confidence);
    }

    /// <summary>
    ///     Proves the sniffer restores the stream position after inspecting it (NonDestructiveInspection).
    /// </summary>
    [Fact]
    public void Detection_NonDestructiveInspection_SeekableStream_RestoresStreamPosition()
    {
        // Arrange: a seekable stream positioned partway through, with an unrecognized name so
        // the content-signature fallback actually reads the bytes
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nbody"));
        stream.Position = 3;

        // Act: sniff the format, which must read from and then restore the stream
        var detection = FormatSniffer.Detect(stream, "document.bin");

        // Assert: the format was read from the content and the original position is restored
        Assert.Equal(DocumentFormat.Pdf, detection.Format);
        Assert.Equal(3, stream.Position);
    }
}
