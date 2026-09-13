using System.Globalization;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DemaConsulting.DocDown.Pdf.Tests.TestData;

/// <summary>
///     Builds every PDF the test suite needs, at test time, from PdfPig's document writer.
/// </summary>
/// <remarks>
///     <para>
///         No binary PDF is committed to this repository. Each fixture is synthesized here when the
///         test that needs it runs, which keeps the repository text-only, removes any question about
///         the provenance or licensing of a checked-in sample document, and makes the exact content
///         of every fixture readable in the same place it is asserted against.
///     </para>
///     <para>
///         The two raster payloads are held as base64 constants rather than as files for the same
///         reason, and the JPEG 2000 codestream as a byte array for the same reason again.
///         <see cref="SourceJpegBytes"/> in particular is the load-bearing one: it is the
///         exact byte sequence embedded as a DCTDecode XObject, so a test can compare the extracted
///         file against the true input rather than against the extractor's own output — which is
///         what turns the passthrough provenance claim into a falsifiable assertion instead of a
///         tautology.
///     </para>
///     <para>
///         This helper lives in the PDF test project rather than in the shared test-support project
///         so PdfPig never becomes a dependency of the Core tests. All members are static and pure
///         apart from their allocations, and are safe for concurrent use.
///     </para>
/// </remarks>
public static class PdfFixtures
{
    /// <summary>An 8x8 baseline JPEG, embedded verbatim as a DCTDecode image.</summary>
    /// <remarks>
    ///     Held as base64 so the repository stays text-only while the bytes remain exact. Exposed
    ///     through <see cref="SourceJpegBytes"/> so the byte-identity assertion compares against the
    ///     true source rather than against anything the extractor produced.
    /// </remarks>
    private const string JpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAA0JCgsKCA0LCwsPDg0QFCEVFBISFCgdHhghMCoyMS8qLi00O0tAND"
        + "hHOS0uQllCR05QVFVUMz9dY1xSYktTVFH/2wBDAQ4PDxQRFCcVFSdRNi42UVFRUVFRUVFRUVFRUVFRUVFRUVFR"
        + "UVFRUVFRUVFRUVFRUVFRUVFRUVFRUVFRUVFRUVH/wAARCAAIAAgDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAA"
        + "AAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0Kx"
        + "wRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhI"
        + "WGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP0"
        + "9fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAx"
        + "EEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RV"
        + "VldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyM"
        + "nK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwClpvh/p8n6UUUVhVxVTm3Ly/GVvYLU"
        + "/9k=";

    /// <summary>An 8x8 PNG, embedded through PdfPig's PNG path, which stores it as compressed samples.</summary>
    /// <remarks>
    ///     Deliberately a different encoding from the JPEG fixture: a PDF stores this as a
    ///     Flate-compressed sample buffer rather than as an image file, so recovering it necessarily
    ///     means decoding and re-encoding — the case that must be labeled <c>decodedToPng</c>.
    /// </remarks>
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJ"
        + "cEhZcwAADsMAAA7DAcdvqGQAAACFSURBVChTFcoxEQBBCMDAU4ISlKCEMipQghIM5ee33vcexsN8WA/74Tzch/"
        + "fwvcAIzMAK7MAJ3MCLPyRGYiZWYidO4iZe/qEwCrOwCrtwCrfw6g+N0ZiN1diN07iN138YjMEcrMEenMEdvPnD"
        + "YizmYi324izu4u0fDuMwD+uwD+dwD+/wA4oilEGmIAUwAAAAAElFTkSuQmCC";

    /// <summary>The area every embedded image is drawn into.</summary>
    /// <remarks>Fixed so two runs over the same fixture produce byte-identical documents.</remarks>
    private static readonly PdfRectangle ImagePlacement = new(30, 400, 130, 500);

    /// <summary>
    ///     Gets the exact bytes of the JPEG the DCTDecode fixtures embed.
    /// </summary>
    /// <returns>A fresh copy of the source JPEG bytes.</returns>
    /// <remarks>
    ///     Returns a copy so a test cannot mutate the shared fixture. This is the reference side of
    ///     the byte-identity assertion that makes the passthrough transform claim falsifiable.
    /// </remarks>
    public static byte[] SourceJpegBytes() => Convert.FromBase64String(JpegBase64);

    /// <summary>
    ///     Gets the exact bytes of the JPEG 2000 codestream the JPXDecode fixture embeds.
    /// </summary>
    /// <returns>A fresh copy of the source JPEG 2000 bytes.</returns>
    /// <remarks>
    ///     Returns a copy so a test cannot mutate the shared fixture. Exposed for the same reason as
    ///     <see cref="SourceJpegBytes"/>: the JPEG 2000 route is a passthrough, and a passthrough
    ///     claim is only falsifiable if the written file can be compared against the true input.
    /// </remarks>
    public static byte[] SourceJpeg2000Bytes() => (byte[])Jpeg2000Codestream.Clone();

    /// <summary>The JPEG 2000 codestream embedded as a JPXDecode image.</summary>
    /// <remarks>
    ///     A short sequence carrying the JPEG 2000 signature box and the start of a codestream. It is
    ///     a genuine JPEG 2000 file header rather than a decodable image, which is exactly the
    ///     condition under test: the bytes are a file this extractor can write out truthfully, but not
    ///     one it can decode without a decoder this library deliberately does not take.
    /// </remarks>
    private static readonly byte[] Jpeg2000Codestream =
        [0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A, 0xFF, 0x4F, 0xFF, 0x51];

    /// <summary>
    ///     Builds a one-page PDF carrying a short paragraph of text and no images.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>The anchor fixture for the layout, determinism, and platform scenarios.</remarks>
    public static byte[] SimpleText()
    {
        using var builder = NewBuilder("DocDown Simple Text", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Introduction", 18, new PdfPoint(40, 700), font);
        page.AddText("The quick brown fox jumps over the lazy dog.", 12, new PdfPoint(40, 660), font);
        return builder.Build();
    }

    /// <summary>
    ///     Builds a multi-page PDF whose pages carry distinguishable text.
    /// </summary>
    /// <param name="pageCount">The number of pages to build. Must be at least one.</param>
    /// <returns>The bytes of the built document.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageCount"/> is less than one.</exception>
    /// <remarks>
    ///     Each page's text names its own page number so a test can assert both the page count and
    ///     that content appears in reading order across pages.
    /// </remarks>
    public static byte[] MultiPage(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);

        using var builder = NewBuilder("DocDown Multi Page", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var number = 1; number <= pageCount; number++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"Page {number} marker text", 12, new PdfPoint(40, 700), font);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Builds a one-page PDF embedding the source JPEG as a DCTDecode image, plus text.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     The passthrough case: the embedded stream is the JPEG file itself, so an honest extractor
    ///     writes those exact bytes out again and labels the write a passthrough.
    /// </remarks>
    public static byte[] WithEmbeddedJpeg()
    {
        using var builder = NewBuilder("DocDown Embedded Jpeg", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("A document with a photograph.", 12, new PdfPoint(40, 700), font);
        page.AddJpeg(Convert.FromBase64String(JpegBase64), ImagePlacement);
        return builder.Build();
    }

    /// <summary>
    ///     Builds a one-page PDF embedding the source PNG as compressed samples, plus text.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     The decode-and-re-encode case: the embedded stream is a sample buffer rather than an image
    ///     file, so producing a usable artifact requires re-encoding and must be labeled as such.
    /// </remarks>
    public static byte[] WithEmbeddedPng()
    {
        using var builder = NewBuilder("DocDown Embedded Png", "DocDown Test Suite");
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("A document with a chart.", 12, new PdfPoint(40, 700), font);
        page.AddPng(Convert.FromBase64String(PngBase64), ImagePlacement);
        return builder.Build();
    }

    /// <summary>
    ///     Builds a one-page PDF carrying an image and no text at all.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     Models the scanned document: the page is entirely graphical, so there is no text layer for
    ///     any text extractor to find. This is a common case rather than an edge case, and the
    ///     extractor must degrade with an explanation instead of writing a silently empty document.
    /// </remarks>
    public static byte[] NoTextLayer()
    {
        using var builder = NewBuilder("DocDown Scanned", "DocDown Test Suite");
        var page = builder.AddPage(PageSize.A4);
        page.AddJpeg(Convert.FromBase64String(JpegBase64), new PdfRectangle(20, 20, 570, 800));
        return builder.Build();
    }

    /// <summary>
    ///     Builds a valid PDF that declares no pages.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     A document with no pages parses cleanly and yields nothing, which must complete with an
    ///     explanation rather than fail or throw.
    /// </remarks>
    public static byte[] ZeroPage()
    {
        using var builder = NewBuilder("DocDown Empty", "DocDown Test Suite");
        return builder.Build();
    }

    /// <summary>
    ///     Builds a byte sequence that begins like a PDF but is structurally broken.
    /// </summary>
    /// <returns>The bytes of the malformed document.</returns>
    /// <remarks>
    ///     Truncating a real document after its header leaves a file that is recognizably a PDF by
    ///     both extension and magic bytes yet has no cross-reference table, which is the realistic
    ///     shape of a corrupted download. The extractor must produce a structured failure from it,
    ///     never an escaping exception.
    /// </remarks>
    public static byte[] Malformed()
    {
        var valid = SimpleText();
        return valid[..Math.Min(valid.Length, 256)];
    }

    /// <summary>
    ///     Assembles a minimal PDF whose trailer declares an encryption handler.
    /// </summary>
    /// <returns>The bytes of the protected document.</returns>
    /// <remarks>
    ///     PdfPig's document writer cannot produce an encrypted document, so this fixture is
    ///     assembled byte by byte instead — still entirely in code, so nothing binary is committed.
    ///     The trailer names a public-key security handler the parser does not implement, which is
    ///     exactly the situation a password-protected or rights-managed PDF puts a reader in: the file
    ///     is structurally valid but its content cannot be reached. The extractor must turn that into
    ///     a structured failure with the full layout still written, never an escaping exception. Cross
    ///     reference offsets are computed here rather than hard-coded so the fixture stays correct if
    ///     its objects are ever edited.
    /// </remarks>
    public static byte[] Encrypted()
    {
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n",
            "4 0 obj\n<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s5 /V 4 /R 4 /Length 128 >>\nendobj\n"
        };

        // Lay the body out first, recording where each object starts, because the cross-reference
        // table must give byte offsets that are only known once the body is complete
        var document = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objects.Length];
        for (var index = 0; index < objects.Length; index++)
        {
            offsets[index] = document.Length;
            document.Append(objects[index]);
        }

        // Emit the cross-reference table, then the trailer that points the reader at the handler
        var startXref = document.Length;
        document.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        document.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            document.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        document.Append("trailer\n<< /Size ").Append(objects.Length + 1)
            .Append(" /Root 1 0 R /Encrypt 4 0 R /ID [<0102030405060708090A0B0C0D0E0F10> ")
            .Append("<0102030405060708090A0B0C0D0E0F10>] >>\nstartxref\n")
            .Append(startXref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(document.ToString());
    }

    /// <summary>
    ///     Assembles a one-page PDF holding one decodable JPEG image and one JPEG 2000 image.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     PdfPig's document writer cannot embed a JPEG 2000 image, so this fixture is assembled byte
    ///     by byte — still entirely in code, so nothing binary is committed. The mixture is
    ///     deliberate: one image whose stored bytes are a JPEG file and one whose stored bytes are a
    ///     JPEG 2000 codestream. Both are written out verbatim, but only the second carries the
    ///     readability caveat, which is what makes the caveat's precision checkable rather than
    ///     assumed.
    /// </remarks>
    public static byte[] WithJpeg2000Image()
    {
        var jpeg = Convert.FromBase64String(JpegBase64);
        return TwoImageDocument(jpeg, "DCTDecode", SourceJpeg2000Bytes(), "JPXDecode");
    }

    /// <summary>
    ///     Assembles a one-page PDF holding one decodable JPEG image and one JBIG2 image.
    /// </summary>
    /// <returns>The bytes of the built document.</returns>
    /// <remarks>
    ///     The undecodable case, kept distinct from the JPEG 2000 one. A JBIG2 stream is an embedded
    ///     segment with no container: it can be neither decoded here nor written out under any
    ///     extension that would describe it, so it is the encoding that must produce a counted loss.
    ///     The mixture with a deliverable image is deliberate, because a document where everything
    ///     failed would not prove the extractor reports a partial success precisely.
    /// </remarks>
    public static byte[] WithUndecodableImage()
    {
        var jpeg = Convert.FromBase64String(JpegBase64);

        // A short JBIG2 generic-region segment header. It is identifiable as JBIG2 so the gap can
        // name the encoding, and it carries no container, so no extension could make it viewable.
        var jbig2 = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x26, 0xA0, 0x00, 0x00, 0x00, 0x00 };

        return TwoImageDocument(jpeg, "DCTDecode", jbig2, "JBIG2Decode");
    }

    /// <summary>
    ///     Assembles a one-page PDF carrying exactly two image XObjects with the given filters.
    /// </summary>
    /// <param name="first">The stored bytes of the first image.</param>
    /// <param name="firstFilter">The PDF filter name declared for the first image.</param>
    /// <param name="second">The stored bytes of the second image.</param>
    /// <param name="secondFilter">The PDF filter name declared for the second image.</param>
    /// <returns>The assembled document bytes.</returns>
    /// <remarks>
    ///     Shared by the fixtures that mix a deliverable image with an awkward one, so the two differ
    ///     only in the encoding under test rather than in incidental document structure — which is
    ///     what lets their differing outcomes be attributed to the encoding. Pure apart from the
    ///     allocation.
    /// </remarks>
    private static byte[] TwoImageDocument(byte[] first, string firstFilter, byte[] second, string secondFilter)
    {
        var content = "q 100 0 0 100 30 400 cm /Im1 Do Q\nq 100 0 0 100 30 250 cm /Im2 Do Q\n"u8.ToArray();

        var body = new List<(string Header, byte[]? Stream)>
        {
            ("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n", null),
            ("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n", null),
            ("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 6 0 R "
             + "/Resources << /XObject << /Im1 4 0 R /Im2 5 0 R >> >> >>\nendobj\n", null),
            ($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB "
             + $"/BitsPerComponent 8 /Filter /{firstFilter} /Length {first.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n", first),
            ($"5 0 obj\n<< /Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB "
             + $"/BitsPerComponent 8 /Filter /{secondFilter} /Length {second.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n", second),
            ($"6 0 obj\n<< /Length {content.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n", content)
        };

        return Assemble(body);
    }

    /// <summary>
    ///     Assembles a PDF from pre-formatted objects, computing the cross-reference offsets.
    /// </summary>
    /// <param name="body">Each object's header text and optional stream payload, in object order.</param>
    /// <returns>The assembled document bytes.</returns>
    /// <remarks>
    ///     Offsets are computed from the bytes actually written rather than hard-coded, so a fixture
    ///     stays valid when its objects are edited — a hand-maintained offset table is precisely the
    ///     kind of detail that silently rots and turns a real assertion into a parse error. Pure apart
    ///     from the allocation.
    /// </remarks>
    private static byte[] Assemble(IReadOnlyList<(string Header, byte[]? Stream)> body)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes("%PDF-1.4\n"));

        var offsets = new int[body.Count];
        for (var index = 0; index < body.Count; index++)
        {
            offsets[index] = bytes.Count;
            bytes.AddRange(Encoding.ASCII.GetBytes(body[index].Header));
            if (body[index].Stream is { } payload)
            {
                bytes.AddRange(payload);
                bytes.AddRange(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));
            }
        }

        var startXref = bytes.Count;
        var trailer = new StringBuilder();
        trailer.Append("xref\n0 ").Append(body.Count + 1).Append('\n').Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            trailer.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        trailer.Append("trailer\n<< /Size ").Append(body.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(startXref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        bytes.AddRange(Encoding.ASCII.GetBytes(trailer.ToString()));
        return [.. bytes];
    }

    /// <summary>
    ///     Reads back a built document so a fixture's own shape can be asserted.
    /// </summary>
    /// <param name="bytes">The document bytes to open. Must not be null.</param>
    /// <returns>The opened document; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Lets a unit test drive the text and image extractors directly against a fixture's pages
    ///     without going through the engine, which is how the unit-level scenarios stay scoped to
    ///     their own unit.
    /// </remarks>
    public static PdfDocument Open(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });
    }

    /// <summary>
    ///     Creates a document builder with fixed metadata.
    /// </summary>
    /// <param name="title">The document title to record.</param>
    /// <param name="author">The document author to record.</param>
    /// <returns>The configured builder; the caller disposes it.</returns>
    /// <remarks>
    ///     Every fixture declares a title and author so the metadata capability has something real to
    ///     report, and so the two values are stable across runs for the determinism scenario.
    /// </remarks>
    private static PdfDocumentBuilder NewBuilder(string title, string author)
    {
        var builder = new PdfDocumentBuilder { IncludeDocumentInformation = true };
        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;
        return builder;
    }
}
