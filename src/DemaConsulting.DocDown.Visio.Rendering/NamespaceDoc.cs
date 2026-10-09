namespace DemaConsulting.DocDown.Visio.Rendering;

/// <summary>
/// Optional Visio page rasterization. Registered with <c>AddVisioRendering</c>
/// alongside <c>AddOffice</c> (or <c>AddVisio</c>), it delegates shape text, embedded-image and
/// metadata extraction to the managed Visio backend and additionally writes one PNG per
/// requested page to <c>pages/</c> using the fully-managed DemaConsulting.CanvasNet.Vsdx
/// rasterizer. Reference this package only when you need rendered pages.
/// </summary>
internal static class NamespaceDoc
{
}
