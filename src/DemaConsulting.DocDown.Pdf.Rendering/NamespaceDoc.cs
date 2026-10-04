namespace DocDown.Pdf.Rendering;

/// <summary>
/// Optional PDF page rasterization. Registered with <c>AddPdfRendering</c> alongside
/// <c>AddPdf</c>, it delegates text, embedded-image and metadata extraction to the managed PDF
/// backend and additionally writes one PNG per requested page to <c>pages/</c> using the
/// fully-managed DemaConsulting.CanvasNet.Pdf rasterizer. Reference this package only when you
/// need rendered pages.
/// </summary>
internal static class NamespaceDoc
{
}
