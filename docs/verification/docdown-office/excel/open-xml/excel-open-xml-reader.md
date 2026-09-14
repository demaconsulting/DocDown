## ExcelOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlReader`, the SDK-to-model
translation.

### Verification Approach

`ExcelOpenXmlReader` is verified through unit tests in `OpenXml/ExcelOpenXmlReaderTests.cs` in
`DemaConsulting.DocDown.Excel.Tests`, exercised against **real generated workbooks** from
`TestData/XlsxFixtures.cs`. The SDK is not mocked, because the reader's contract is the faithful
translation of a genuine `.xlsx` into the model: the verbatim cell values, the formulas alongside their
values, the sheet identity and ordering, the embedded images and their sheet association, and the dropping
of empty cells. Every value, sheet name, and image in the fixtures is synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: SpreadsheetML workbooks generated at test time by `TestData/XlsxFixtures.cs`
- **Filesystem**: none; the reader reads a generated workbook from memory
- **Mocking**: none; the SDK is exercised against real workbooks
- **Isolation**: each test builds its own workbook

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelOpenXmlReader` unit test run passes when a long prose cell is preserved
verbatim at full length; when a numeric cell is read exactly as stored; when a formula cell keeps its
formula, its value, and its address; when a two-sheet workbook returns its sheets in workbook order with
their tab names; when an image workbook yields the embedded image and records its sheet association; and
when an empty sheet yields a sheet with no cells. Any truncated value, reformatted number, dropped formula,
lost sheet name, or lost image association is a failure.

### Test Scenarios

#### A long prose cell is preserved verbatim at full length

**Test**: `ExcelOpenXmlReader_Read_LongProseCell_PreservedVerbatimAtFullLength`

Proves a cell holding a long synthetic prose block reaches the model intact, never truncated. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-PreservesVerbatimValues`.

#### Numeric cells are read verbatim

**Test**: `ExcelOpenXmlReader_Read_NumericCells_ReadVerbatim`

Proves a numeric cell is taken exactly as stored, at full precision. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsNumericValuesVerbatim`.

#### A formula cell keeps its formula, value, and address

**Test**: `ExcelOpenXmlReader_Read_FormulaCell_KeepsFormulaAndValueAndAddress`

Proves a computed cell reaches the model with its formula, cached value, and A1 address. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-KeepsFormulaAndValue`.

#### A two-sheet workbook returns sheets in order with names

**Test**: `ExcelOpenXmlReader_Read_TwoSheetWorkbook_ReturnsSheetsInOrderWithNames`

Proves the worksheets come out in workbook order, each carrying its tab name. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-PreservesSheetIdentity`.

#### An empty sheet yields a sheet with no cells

**Test**: `ExcelOpenXmlReader_Read_EmptySheet_YieldsSheetWithNoCells`

Proves a worksheet with no populated cells yields a sheet with no cells rather than a grid of blanks.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-DropsEmptyCells`.
