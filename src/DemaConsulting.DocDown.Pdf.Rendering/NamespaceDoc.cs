namespace DocDown.Pdf.Rendering;

/// <summary>
/// Optional PDF page rasterization. Registered with <c>AddPdfRendering</c> alongside
/// <c>AddPdf</c>, it delegates text, embedded-image and metadata extraction to the managed PDF
/// backend and additionally writes one PNG per requested page to <c>pages/</c> using PDFium.
/// Reference this package only when you need rendered pages; it is the sole part of the PDF
/// capability that pulls in native binaries.
/// </summary>
internal static class NamespaceDoc
{
}
