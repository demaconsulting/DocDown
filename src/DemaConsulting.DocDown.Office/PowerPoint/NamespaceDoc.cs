namespace DocDown.PowerPoint;

/// <summary>
/// PowerPoint extraction backend. Registers both the managed Open XML extractor and the COM
/// automation extractor for <c>.pptx</c> decks via a single <c>AddPowerPoint</c> call; this is
/// the whole public surface of the package. Slide text, titles and speaker notes always come from
/// the managed backend in <c>DocDown.PowerPoint.OpenXml</c>; rendered slides come from
/// <c>DocDown.PowerPoint.Com</c> when Microsoft PowerPoint is installed.
/// </summary>
internal static class NamespaceDoc
{
}
