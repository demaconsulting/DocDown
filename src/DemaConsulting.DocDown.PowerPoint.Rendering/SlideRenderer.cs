using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pptx;

namespace DemaConsulting.DocDown.PowerPoint.Rendering;

/// <summary>
///     The single rasterization seam of this package: it rasterizes one PowerPoint slide to a PNG
///     through DemaConsulting.CanvasNet.Pptx, a fully-managed PowerPoint rasterizer.
/// </summary>
/// <remarks>
///     <para>
///         Every CanvasNet.Pptx/CanvasNet call lives here so the rest of the package — and the rest
///         of DocDown — stays decoupled from the exact rasterization API and can be reasoned about
///         without it in view. DocDown's public surface names no CanvasNet type (see
///         <c>PublicApi_AllPublicMembers_ExposeNoCanvasNetTypes</c> in the test suite).
///     </para>
///     <para>
///         <strong>No process-wide lock guards these calls.</strong> CanvasNet.Pptx is a
///         fully-managed library with no shared mutable per-call state: each call opens and
///         disposes its own <see cref="PptxDocument"/> instance. This mirrors
///         <c>DemaConsulting.DocDown.Pdf.Rendering</c>'s own <c>PageRenderer</c>, and the repo's established
///         pattern for a fully-managed backend more generally (see <c>DemaConsulting.DocDown.Pdf</c>'s
///         <c>PdfDocumentExtractor.ProbeAvailability</c>, which is likewise unconditional and
///         lock-free). The one shared mutable state in the dependency graph —
///         <c>SystemFontCatalog</c>'s lazily-built system-font directory scan and its
///         bundled-fallback font cache — is confirmed safe for concurrent use by design: the
///         directory scan is a <see cref="System.Lazy{T}"/> in
///         <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/> mode
///         (concurrent callers block on the single build and share its published result), and the
///         bundled-fallback cache is a dictionary guarded by a dedicated lock around both lookup and
///         population.
///     </para>
/// </remarks>
internal static class SlideRenderer
{
    /// <summary>
    ///     Rasterizes one slide of a PowerPoint presentation to PNG bytes at a given DPI.
    /// </summary>
    /// <param name="pptx">The full bytes of the source PowerPoint presentation.</param>
    /// <param name="slideIndexZeroBased">The zero-based index of the slide to render.</param>
    /// <param name="dpi">The dots-per-inch to render at; higher values trade file size for fidelity.</param>
    /// <returns>The PNG-encoded bytes of the rendered slide.</returns>
    /// <remarks>
    ///     Opens a fresh <see cref="PptxDocument"/> over the supplied bytes, renders the requested
    ///     slide through <c>PptxDocument.Render(int, float, PptxRenderOptions?)</c> (which reads the
    ///     slide's size in EMUs, scales by <paramref name="dpi"/> / 96, and rounds to the nearest
    ///     pixel, preserving the slide's aspect ratio), and encodes the resulting <c>Surface</c> to
    ///     PNG through <c>PngCodec.Save</c>. Any parsing or rasterization fault surfaces here as a
    ///     thrown exception; the caller isolates it per slide and converts it into a plain note, so
    ///     a single unrenderable slide never aborts a whole extraction and never reaches the
    ///     library's caller as an exception.
    /// </remarks>
    public static byte[] Render(byte[] pptx, int slideIndexZeroBased, int dpi)
    {
        ArgumentNullException.ThrowIfNull(pptx);

        using var source = new MemoryStream(pptx, writable: false);
        using var document = PptxDocument.Open(source);
        using var surface = document.Render(slideIndexZeroBased, (float)dpi);
        using var buffer = new MemoryStream();
        PngCodec.Save(surface, buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Reads the number of slides in a PowerPoint presentation.
    /// </summary>
    /// <param name="pptx">The full bytes of the source PowerPoint presentation.</param>
    /// <returns>The slide count reported by the rasterizer.</returns>
    /// <remarks>
    ///     Kept here rather than in the caller so this type stays the package's single
    ///     rasterization seam: the count comes from the same component that will rasterize the
    ///     slides. Any parsing fault surfaces here as a thrown exception; the caller isolates it and
    ///     reports a note rather than failing the extraction.
    /// </remarks>
    public static int GetSlideCount(byte[] pptx)
    {
        ArgumentNullException.ThrowIfNull(pptx);

        using var source = new MemoryStream(pptx, writable: false);
        using var document = PptxDocument.Open(source);
        return document.SlideCount;
    }
}
