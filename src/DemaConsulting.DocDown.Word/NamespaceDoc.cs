namespace DocDown.Word;

/// <summary>
/// Word extraction backend. Registers the managed Open XML extractor for <c>.docx</c> documents
/// via <c>AddWord</c>; this is the whole public surface of the package, and the extraction work
/// itself lives in <c>DocDown.Word.OpenXml</c>. The legacy binary <c>.doc</c> format is not
/// supported.
/// </summary>
internal static class NamespaceDoc
{
}
