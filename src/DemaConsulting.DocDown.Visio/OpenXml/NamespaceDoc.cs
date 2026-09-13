namespace DocDown.Visio.OpenXml;

/// <summary>
/// Open Packaging extraction for <c>.vsdx</c> and <c>.vsdm</c>. Reads every page name, the text
/// of every shape, and — the headline capability — resolves each connector into a real directed
/// edge so a drawing arrives as a schematic rather than a bag of strings, all without requiring
/// Microsoft Visio. It renders no pages; spatial layout needs the COM backend.
/// </summary>
internal static class NamespaceDoc
{
}
