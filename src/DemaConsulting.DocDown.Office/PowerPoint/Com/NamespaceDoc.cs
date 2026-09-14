namespace DocDown.PowerPoint.Com;

/// <summary>
/// Slide rendering by automating an installed Microsoft PowerPoint over COM. Delegates text,
/// titles and speaker notes to the managed Open XML backend, then rasterizes each slide to a PNG
/// in <c>pages/</c>. Windows-only and unavailable when PowerPoint is not installed: the backend
/// reports itself unavailable and extraction falls back to the managed backend's text.
/// </summary>
internal static class NamespaceDoc
{
}
