## ExcelOpenXmlReader

![DocDown.Excel Structure](ExcelView.svg)

### Purpose

`ExcelOpenXmlReader` is the one place that touches the Open XML spreadsheet object model. Its single
responsibility is to turn an `.xlsx` package into the backend-neutral `ExcelWorkbookModel`, preserving
every cell's value and formula verbatim and each sheet's identity and addressing, so every downstream
decision is made once against a model a test can build by hand.

### Data Model

`ExcelOpenXmlReader` is an `internal static class`, stateless and reusable. It produces an
`ExcelWorkbookModel`: the worksheets in workbook order, the deduplicated embedded images, and the workbook
metadata mapped from the OPC core properties. Each `ExcelSheetModel` carries its tab name, its non-empty
cells, its merged ranges, its image references, its charts, and its shape texts.

### Key Methods

- **`ExcelWorkbookModel Read(Stream stream)`** — opens the package read-only; loads the shared-string
  table; resolves the embedded images through `ExcelOpenXmlImageReader`; then walks the workbook's sheet
  list in order, resolving each worksheet part, reading its cells, merged ranges, images, charts (through
  `ExcelChartReader`), and shape texts (through `ExcelDrawingTextReader`); and maps the package core
  properties to the metadata. Precondition: a readable, seekable stream. Postcondition: a model whose
  worksheets are in workbook (tab) order.
- **`LoadSharedStrings`** (private) — reads the shared-string table up front so every string-typed cell
  resolves its text without re-walking the part, taking the whole inner text so rich-text runs concatenate
  into the complete value.
- **`ReadCells`** (private) — keeps only cells that carry a value or a formula, in row-major order, so a
  sparse sheet yields only its real content.
- **`ResolveValue`** (private) — resolves a cell to its verbatim text following the cell's data type: a
  shared-string or inline-string cell to its full text, a boolean to `TRUE`/`FALSE`, and every other type
  (number, date serial, error) exactly as stored so no precision is lost. Never truncated.
- **`ReadMergedRanges`** (private) — reads the declared merged ranges in document order, skipping a blank
  reference, so the emitter can note them beside a grid table a GFM table cannot span.

### Error Handling

A package it cannot open (`OpenXmlPackageException`, `FileFormatException`) — an encrypted, malformed, or
non-spreadsheet file — and a missing workbook part are wrapped in an `ExcelExtractionException` whose
message names the condition, so Core sees a structured failure rather than a raw SDK error and still
writes the full layout. Every other fault propagates to Core for the same treatment. A null stream is
rejected with `ArgumentNullException`.

### Dependencies

- **DocDown.Core** — `DocumentMetadata` and the OPC metadata mapping helpers.
- **DocumentFormat.OpenXml** (OTS) — `SpreadsheetDocument`, the spreadsheet element types, and the package
  parts.
- **ExcelOpenXmlImageReader**, **ExcelChartReader**, **ExcelDrawingTextReader** — the three focused
  readers it delegates the drawing layer to.
- **ExcelExtractionException** — the exception it raises for an unreadable package.

### Callers

`ExcelOpenXmlExtractor.ExtractAsync` calls `Read` over the buffered workbook bytes. The self-test round
trip also reads the embedded workbook. Nothing else calls it directly.
