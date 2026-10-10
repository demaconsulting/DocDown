namespace DemaConsulting.DocDown.PowerPoint.Rendering;

/// <summary>
/// Optional PowerPoint slide rasterization. Registered with <c>AddPowerPointRendering</c>
/// alongside <c>AddOffice</c> (or <c>AddPowerPoint</c>), it delegates text, embedded-image and
/// metadata extraction to the managed PowerPoint backend and additionally writes one PNG per
/// requested slide to <c>pages/</c> using the fully-managed DemaConsulting.CanvasNet.Pptx
/// rasterizer. Reference this package only when you need rendered slides.
/// </summary>
internal static class NamespaceDoc
{
}
