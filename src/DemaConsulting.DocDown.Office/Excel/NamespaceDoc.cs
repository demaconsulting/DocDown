namespace DocDown.Excel;

/// <summary>
/// Excel extraction backend. Registers the managed Open XML extractor for <c>.xlsx</c> workbooks
/// via <c>AddExcel</c>; this is the whole public surface of the package, and the extraction work
/// itself lives in <c>DocDown.Excel.OpenXml</c>. There is deliberately no rendering backend, and
/// the legacy binary <c>.xls</c> format is not supported.
/// </summary>
internal static class NamespaceDoc
{
}
