using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pdf;

namespace DocDown.Pdf.Rendering;

/// <summary>
///     The single rasterization seam of this package: it rasterizes one PDF page to a PNG through
///     DemaConsulting.CanvasNet.Pdf, a fully-managed PDF rasterizer.
/// </summary>
/// <remarks>
///     <para>
///         Every CanvasNet.Pdf/CanvasNet call lives here so the rest of the package — and the rest
///         of DocDown — stays decoupled from the exact rasterization API and can be reasoned about
///         without it in view. DocDown's public surface names no CanvasNet type (see
///         <c>PublicApi_AllPublicMembers_ExposeNoCanvasNetTypes</c> in the test suite).
///     </para>
///     <para>
///         <strong>No process-wide lock guards these calls.</strong> Unlike the PDFium-backed
///         renderer this package previously used, CanvasNet.Pdf is a fully-managed library with no
///         shared mutable per-call state: each call opens and disposes its own
///         <see cref="PdfDocument"/> instance. This mirrors the repo's established pattern for a
///         fully-managed backend (see
///         <c>DocDown.Pdf</c>'s <c>PdfDocumentExtractor.ProbeAvailability</c>, which is likewise
///         unconditional and lock-free). The one shared mutable state in the dependency graph —
///         <c>SystemFontCatalog</c>'s lazily-built system-font directory scan and its bundled-fallback
///         font cache — is confirmed safe for concurrent use by design: the directory scan is a
///         <see cref="System.Lazy{T}"/> in <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/>
///         mode (concurrent callers block on the single build and share its published result), and
///         the bundled-fallback cache is a dictionary guarded by a dedicated lock around both lookup
///         and population. The concurrent-render test in this package's test suite is the regression
///         guard for this behavior.
///     </para>
/// </remarks>
internal static class PageRenderer
{
    /// <summary>
    ///     Rasterizes one page of a PDF to PNG bytes at a given DPI.
    /// </summary>
    /// <param name="pdf">The full bytes of the source PDF.</param>
    /// <param name="pageIndexZeroBased">The zero-based index of the page to render.</param>
    /// <param name="dpi">The dots-per-inch to render at; higher values trade file size for fidelity.</param>
    /// <returns>The PNG-encoded bytes of the rendered page.</returns>
    /// <remarks>
    ///     Opens a fresh <see cref="PdfDocument"/> over the supplied bytes, renders the requested
    ///     page through <c>PdfDocument.Render(int, float)</c> (which reads the page's
    ///     rotation-adjusted size in points via <c>PdfDocument.GetPageInfo</c>, scales by
    ///     <paramref name="dpi"/> / 72, and rounds to the nearest pixel — exactly the DPI semantics
    ///     this method has always documented), and encodes the resulting <c>Surface</c> to PNG
    ///     through <c>PngCodec.Save</c>. Any parsing or rasterization fault surfaces here as a
    ///     thrown exception; the caller isolates it per page and converts it into a plain note, so a
    ///     single unrenderable page never aborts a whole extraction and never reaches the library's
    ///     caller as an exception.
    /// </remarks>
    public static byte[] Render(byte[] pdf, int pageIndexZeroBased, int dpi)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password: null);
        using var surface = document.Render(pageIndexZeroBased, (float)dpi);
        using var buffer = new MemoryStream();
        PngCodec.Save(surface, buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Reads the number of pages in a PDF.
    /// </summary>
    /// <param name="pdf">The full bytes of the source PDF.</param>
    /// <returns>The page count reported by the rasterizer.</returns>
    /// <remarks>
    ///     Kept here rather than in the caller so this type stays the package's single rasterization
    ///     seam: the count comes from the same component that will rasterize the pages. Any parsing
    ///     fault surfaces here as a thrown exception; the caller isolates it and reports a note
    ///     rather than failing the extraction.
    /// </remarks>
    public static int GetPageCount(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password: null);
        return document.PageCount;
    }
}
