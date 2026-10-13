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
when an empty sheet yields a sheet with no cells; when inline-string cells resolve to their full text,
including rich-text runs; when error and date-serial cells are read as stored; when the package's core
properties are mapped into the model's metadata; when bytes that are not a package, a null stream, and a
package with no workbook part are each rejected; when a legacy-only comment is read with its author, a
threaded-only comment is read with its person resolved, a cell carrying both grammars keeps only the
threaded comment, a multi-reply thread keeps every reply in order, a threaded-only cell is appended
after the legacy-ordered cells, comments stay with their owning sheet, and a workbook with no comment parts
yields no comments, and a comment whose legacy author index or threaded person identifier names nobody is
reported with no author and its text intact. Any truncated value, reformatted number, dropped formula,
lost sheet name, lost image association, or lost, duplicated, or misattributed comment is a failure.

### Test Scenarios

#### An embedded image is extracted and its sheet association recorded

**Tests**: `ExcelOpenXmlReader_Read_ImageWorkbook_ExtractsEmbeddedImage` and
`ExcelOpenXmlReader_Read_ImageWorkbook_RecordsSheetAssociation`

Prove an image workbook yields its embedded image through `ExcelOpenXmlImageReader` and records which
worksheet references it. These tests carry no requirement link of their own; they are the reader-level
evidence for the image reader's behavior. Multi-sheet image association is covered in the image reader's own
test class.

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

#### A multi-reply thread keeps every reply, in order

**Test**: `ExcelOpenXmlReader_Read_MultiReplyThread_KeepsEveryReplyInOrder`

Proves a cell whose threaded conversation carries three turns yields all three, with their authors,
in the order they were written. A rule that kept only a thread's first turn would discard the replies
that usually carry the resolution, and a single-comment fixture cannot tell the two behaviors apart;
reply order is asserted because a conversation read out of sequence reverses who answered whom.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsThreadedComments`.

#### A threaded-only cell is appended after the legacy-ordered cells

**Test**: `ExcelOpenXmlReader_Read_ThreadedOnlyCell_IsAppendedAfterLegacyOrderedCells`

Proves a cell commented only in the threaded part appears after the cells the legacy part orders,
and that a cell carrying both keeps the position its legacy entry held. The whole reference sequence
is pinned rather than only the trailing cell, because the fixture's threaded-only cell sorts last
anyway and an assertion on it alone could pass by luck. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-DeduplicatesThreadedAndLegacyComments`.

#### Typed cells resolve and merged ranges are carried

**Test**: `ExcelOpenXmlReader_Read_TypedAndMergedWorkbook_ResolvesTypesAndCarriesMergedRanges`

Proves shared-string, boolean, and decimal cells resolve to their text, `TRUE` or `FALSE`, and verbatim
value, and that declared merged ranges are carried in order while a blank range is skipped. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsNumericValuesVerbatim` and
`DocDownExcel-OpenXml-ExcelOpenXmlReader-CarriesMergedRanges`.

#### Error and date-serial cells are read as stored

**Test**: `ExcelOpenXmlReader_Read_ErrorAndDateSerialCells_ReadAsStored`

Proves two error cells (`#DIV/0!` and `#N/A`) and a date-serial cell (`45292.5`) reach the model exactly as
stored. Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsNumericValuesVerbatim`.

#### Inline-string cells resolve to their full text

**Test**: `ExcelOpenXmlReader_Read_InlineStringCells_ResolveToFullText`

Proves a plain inline-string cell and one split into two rich-text runs resolve to `Plain inline text` and
`Rich text`. Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsInlineStrings`.

#### Core properties are mapped into metadata

**Test**: `ExcelOpenXmlReader_Read_CorePropertiesWorkbook_MapsMetadata`

Proves a package declaring a creator, a title, and a creation date yields metadata fields `creator`,
`title`, and `created` (as `2024-03-05T10:30:00Z`), every field carrying core-properties provenance.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-MapsCoreProperties`.

#### Unresolved authors yield no attribution

**Test**: `ExcelOpenXmlReader_Read_UnresolvedAuthors_YieldNullAuthorAndKeepText`

Proves a legacy comment citing an author index the part does not list, and a threaded comment citing a
person identifier the person part does not list, are both reported with a null author and their text intact.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsLegacyComments` and
`DocDownExcel-OpenXml-ExcelOpenXmlReader-ReadsThreadedComments`.

#### An invalid package is rejected with a typed exception

**Test**: `ExcelOpenXmlReader_Read_InvalidPackage_ThrowsTypedException`

Proves bytes that are not an Open XML package raise `ExcelExtractionException`. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-RejectsInvalidPackage`.

#### A null stream is rejected

**Test**: `ExcelOpenXmlReader_Read_NullStream_ThrowsArgumentNullException`

Proves a null stream raises `ArgumentNullException`. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-RejectsInvalidPackage`.

#### A package with no workbook part is rejected

**Test**: `ExcelOpenXmlReader_Read_PackageWithoutWorkbookPart_ThrowsTypedException`

Proves a valid package with no workbook part raises `ExcelExtractionException` whose message says the
workbook contains no workbook part. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlReader-RejectsMissingWorkbookPart`.
