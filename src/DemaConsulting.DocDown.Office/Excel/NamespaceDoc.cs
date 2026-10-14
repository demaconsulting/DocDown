namespace DemaConsulting.DocDown.Excel;

/// <summary>
/// Excel extraction backend. Registers the managed Open XML extractor for <c>.xlsx</c> workbooks
/// via <c>AddExcel</c>, the registration entry point a host calls; the extractor itself,
/// <c>ExcelOpenXmlExtractor</c>, is also public, and the rest of the extraction work lives in
/// <c>DemaConsulting.DocDown.Excel.OpenXml</c> and <c>DemaConsulting.DocDown.Excel.Markdown</c>.
/// There is deliberately no rendering backend, and the legacy binary <c>.xls</c> format is not supported.
/// </summary>
internal static class NamespaceDoc
{
}
