namespace DocDown.Visio.Com;

/// <summary>
/// Page rendering by automating an installed Microsoft Visio over COM. Delegates page names,
/// shape text and connector topology to the managed Open Packaging backend, then rasterizes each
/// page to a PNG in <c>pages/</c> so the spatial arrangement survives. Windows-only and
/// unavailable when Visio is not installed: the backend reports itself unavailable and the
/// topology is still delivered by the managed backend.
/// </summary>
internal static class NamespaceDoc
{
}
