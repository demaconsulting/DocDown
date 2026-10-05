## ExcelOpenXmlReader Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlReader`, the SDK-to-model
translation.

### Verification Approach

`ExcelOpenXmlReader` is verified through unit tests in `OpenXml/ExcelOpenXmlReaderTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, exercised against **real generated workbooks** from
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

#### A legacy-only comment is read with its author

**Test**: `ExcelOpenXmlReader_Read_LegacyCommentWorkbook_ReadsCommentWithAuthor`

Proves a worksheet whose only comment lives in the legacy comments part reaches the model with its cell
reference, its text, and the author name resolved through that part's own author list. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsLegacyComments`.

#### A threaded-only comment is read with its person resolved

**Test**: `ExcelOpenXmlReader_Read_ThreadedCommentWorkbook_ReadsCommentWithResolvedPerson`

Proves a worksheet whose only comment lives in a threaded comments part reaches the model with its cell
reference, its text, and the display name resolved through the workbook's person part rather than the raw
person identifier. Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsThreadedComments`.

#### A cell carrying both grammars reports the threaded remark once

**Test**: `ExcelOpenXmlReader_Read_ThreadedAndLegacyOnSameCell_KeepsOnlyThreadedComment`

Proves the deduplication rule. The fixture places a threaded comment and a legacy backward-compatibility
entry on the same cell, with deliberately different text so the surviving one is identifiable. Exactly one
comment reaches the model, and it is the threaded one. This is what stops a reader from seeing one remark
twice and taking it as two reviewers raising the same point. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-DeduplicatesThreadedAndLegacyComments`.

#### Comments stay with the worksheet that carries them

**Test**: `ExcelOpenXmlReader_Read_MultiSheetCommentWorkbook_AttributesCommentsToOwningSheet`

Proves a two-sheet workbook with a different comment and author on each sheet reports each remark against
the sheet and cell that carries it, so attribution is proven rather than assumed from a single-sheet case.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-AttributesCommentsToSheet`.

#### A workbook with no comment parts yields no comments

**Test**: `ExcelOpenXmlReader_Read_WorkbookWithoutComments_YieldsNoComments`

Proves the ordinary case — a workbook nobody reviewed — yields sheets with no comments rather than an
error. Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-AttributesCommentsToSheet`.
