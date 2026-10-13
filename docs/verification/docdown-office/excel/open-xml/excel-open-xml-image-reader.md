## ExcelOpenXmlImageReader Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlImageReader`, which resolves
a workbook's embedded images to their bytes and sheet association.

### Verification Approach

`ExcelOpenXmlImageReader` is verified in two ways. Its dedicated test class,
`OpenXml/ExcelOpenXmlImageReaderTests.cs`, drives `Collect` directly over workbooks with pictures on one
or more sheets. The reader that drives it is also verified in `OpenXml/ExcelOpenXmlReaderTests.cs`, with
the extraction-level effect additionally exercised in `DocDownExcelTests.cs` and the inline-link effect in
`Markdown/ExcelContentEmitterTests.cs`, all in `DemaConsulting.DocDown.Office.Tests`. The workbooks are
generated fixtures from `TestData/XlsxFixtures.cs` whose images are synthetic.

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
that references it; and when one image part shown on two sheets is yielded once with both sheets recorded.
Any dropped image, re-encoded bytes, duplicated shared image, or lost sheet association is a failure.

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

#### Images on separate sheets are attributed to their own sheets

**Tests**: `ExcelOpenXmlImageReader_Collect_TwoSheetsWithPictures_AttributesEachImageToItsSheet`,
`ExcelOpenXmlImageReader_Collect_NoDrawingsOrNullPart_YieldsNothingOrThrows`

Prove a two-sheet workbook yields each image once with its own tab index and per-sheet reference, and a
workbook without drawings yields nothing while a null part is rejected. Additional evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlImageReader-RecordsSheetAssociation`.

#### An image part shared by two sheets is yielded once

**Test**: `ExcelOpenXmlImageReader_Collect_ImagePartSharedByTwoSheets_YieldsOneImageWithBothSheets`

Proves two worksheets whose drawings relate to the same image part yield one image whose source pages are
sheets 1 and 2, and that each sheet keeps its own inline reference to it. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlImageReader-DeduplicatesSharedParts`.
