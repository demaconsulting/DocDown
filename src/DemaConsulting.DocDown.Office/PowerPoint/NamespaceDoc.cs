namespace DemaConsulting.DocDown.PowerPoint;

/// <summary>
/// PowerPoint extraction backend. Registers the managed Open XML extractor for <c>.pptx</c>
/// decks via a single <c>AddPowerPoint</c> call; this is the whole public surface of the
/// package. Slide text, titles and speaker notes always come from the managed backend in
/// <c>DemaConsulting.DocDown.PowerPoint.OpenXml</c>; rendered slide images are provided separately by the
/// opt-in <c>DemaConsulting.DocDown.PowerPoint.Rendering</c> package.
/// </summary>
internal static class NamespaceDoc
{
}
