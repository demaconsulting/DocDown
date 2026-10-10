namespace DemaConsulting.DocDown.Visio;

/// <summary>
/// Visio extraction backend. Registers the managed Open Packaging extractor for <c>.vsdx</c> and
/// <c>.vsdm</c> drawings via a single <c>AddVisio</c> call; this is the whole public surface of the
/// package. Page names, shape text and the directed connector topology always come from the
/// managed backend in <c>DemaConsulting.DocDown.Visio.OpenXml</c>; rendered page images are
/// provided separately by the opt-in <c>DemaConsulting.DocDown.Visio.Rendering</c> package.
/// </summary>
internal static class NamespaceDoc
{
}
