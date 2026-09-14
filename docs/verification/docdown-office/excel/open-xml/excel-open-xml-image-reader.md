## ExcelOpenXmlImageReader Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlImageReader`, which resolves
a workbook's embedded images to their bytes and sheet association.

### Verification Approach

`ExcelOpenXmlImageReader` carries no dedicated test class; its observable contract is what reaches the
model, so it is verified **through the reader that drives it** in `OpenXml/ExcelOpenXmlReaderTests.cs`,
with the extraction-level effect additionally exercised in `DocDownExcelTests.cs` and the inline-link
effect in `Markdown/ExcelContentEmitterTests.cs`, all in `DemaConsulting.DocDown.Office.Tests`. The
workbooks are real generated fixtures from `TestData/XlsxFixtures.cs` whose images are synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: generated workbooks that embed one or more images on named worksheets
- **Filesystem**: none for the reader scenarios; a per-test `TempScratch` folder for the extraction-level
  scenario
- **Mocking**: none; the SDK is exercised against real workbooks
- **Isolation**: each test builds its own workbook

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelOpenXmlImageReader` unit test run passes when an image workbook yields the
embedded image with its stored bytes, and when the image records the 1-based tab index of the worksheet
that references it. Any dropped image, re-encoded bytes, or lost sheet association is a failure.

### Test Scenarios

#### An image workbook yields the embedded image

**Test**: `ExcelOpenXmlReader_Read_ImageWorkbook_ExtractsEmbeddedImage`

Proves an embedded image is resolved from the worksheet's drawing and yielded with its stored bytes.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlImageReader-YieldsEmbeddedImages`.

#### The image records its sheet association

**Test**: `ExcelOpenXmlReader_Read_ImageWorkbook_RecordsSheetAssociation`

Proves the yielded image records the tab index of the worksheet that references it, so the sheet
association survives extraction. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlImageReader-RecordsSheetAssociation`.
