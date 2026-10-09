using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.DocDown.Visio.Rendering;

/// <summary>
///     The single rasterization seam of this package: it rasterizes one Visio page to a PNG
///     through DemaConsulting.CanvasNet.Vsdx, a fully-managed Visio rasterizer.
/// </summary>
/// <remarks>
///     <para>
///         Every CanvasNet.Vsdx/CanvasNet call lives here so the rest of the package — and the rest
///         of DocDown — stays decoupled from the exact rasterization API and can be reasoned about
///         without it in view. DocDown's public surface names no CanvasNet type (see
///         <c>PublicApi_AllPublicMembers_ExposeNoCanvasNetTypes</c> in the test suite).
///     </para>
///     <para>
///         <strong>No process-wide lock guards these calls.</strong> CanvasNet.Vsdx is a
///         fully-managed library with no shared mutable per-call state: each call opens and
///         disposes its own <see cref="VsdxDocument"/> instance. This mirrors
///         <c>DemaConsulting.DocDown.PowerPoint.Rendering</c>'s own <c>SlideRenderer</c> and
///         <c>DemaConsulting.DocDown.Pdf.Rendering</c>'s own <c>PageRenderer</c>, and the repo's
///         established pattern for a fully-managed backend more generally (see
///         <c>DemaConsulting.DocDown.Pdf</c>'s <c>PdfDocumentExtractor.ProbeAvailability</c>, which
///         is likewise unconditional and lock-free).
///     </para>
/// </remarks>
internal static class PageRenderer
{
    /// <summary>
    ///     Rasterizes one page of a Visio drawing to PNG bytes at a given DPI.
    /// </summary>
    /// <param name="vsdx">The full bytes of the source Visio drawing.</param>
    /// <param name="pageIndexZeroBased">The zero-based index of the page to render.</param>
    /// <param name="dpi">The dots-per-inch to render at; higher values trade file size for fidelity.</param>
    /// <returns>The PNG-encoded bytes of the rendered page.</returns>
    /// <remarks>
    ///     Opens a fresh <see cref="VsdxDocument"/> over the supplied bytes, renders the requested
    ///     page through <c>VsdxDocument.Render(int, int, VsdxRenderOptions?)</c> (which reads the
    ///     page's size in EMUs, scales by <paramref name="dpi"/> / 96, and rounds to the nearest
    ///     pixel, preserving the page's aspect ratio), and encodes the resulting <c>Surface</c> to
    ///     PNG through <c>PngCodec.Save</c>. The DPI overload deliberately accepts <paramref name="dpi"/>
    ///     as an <see cref="int"/>, not a <see cref="float"/>, unlike
    ///     <c>DemaConsulting.CanvasNet.Pptx.PptxDocument</c>'s own float-DPI overload — this is a
    ///     documented API deviation in CanvasNet.Vsdx, passed through unchanged here rather than cast.
    ///     Any parsing or rasterization fault surfaces here as a thrown exception; the caller
    ///     isolates it per page and converts it into a plain note, so a single unrenderable page
    ///     never aborts a whole extraction and never reaches the library's caller as an exception.
    /// </remarks>
    public static byte[] Render(byte[] vsdx, int pageIndexZeroBased, int dpi)
    {
        ArgumentNullException.ThrowIfNull(vsdx);

        using var source = new MemoryStream(vsdx, writable: false);
        using var document = VsdxDocument.Open(source);
        using var surface = document.Render(pageIndexZeroBased, dpi);
        using var buffer = new MemoryStream();
        PngCodec.Save(surface, buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Reads the number of pages in a Visio drawing.
    /// </summary>
    /// <param name="vsdx">The full bytes of the source Visio drawing.</param>
    /// <returns>The page count reported by the rasterizer.</returns>
    /// <remarks>
    ///     Kept here rather than in the caller so this type stays the package's single
    ///     rasterization seam: the count comes from the same component that will rasterize the
    ///     pages. Any parsing fault surfaces here as a thrown exception; the caller isolates it and
    ///     reports a note rather than failing the extraction.
    /// </remarks>
    public static int GetPageCount(byte[] vsdx)
    {
        ArgumentNullException.ThrowIfNull(vsdx);

        using var source = new MemoryStream(vsdx, writable: false);
        using var document = VsdxDocument.Open(source);
        return document.PageCount;
    }
}
