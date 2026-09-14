namespace DocDown.Visio;

/// <summary>
/// Visio extraction backend. Registers both the managed Open Packaging extractor and the COM
/// automation extractor for <c>.vsdx</c> and <c>.vsdm</c> drawings via a single <c>AddVisio</c>
/// call; this is the whole public surface of the package. Page names, shape text and the directed
/// connector topology always come from the managed backend in <c>DocDown.Visio.OpenXml</c>;
/// rendered pages come from <c>DocDown.Visio.Com</c> when Microsoft Visio is installed.
/// </summary>
internal static class NamespaceDoc
{
}
