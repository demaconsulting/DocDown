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
cells, its merged ranges, its image references, its charts, its shape texts, and its reviewer
comments. Each `ExcelCommentModel` carries the A1 cell reference the remark annotates, the resolved
author name (or `null` when the workbook names nobody), and the comment text taken whole.

### Comment deduplication rule

A worksheet can carry comments in two grammars at once. The legacy grammar lives in
`WorksheetPart.WorksheetCommentsPart` and names its author by index into that part's own author list.
The modern threaded grammar lives in `WorksheetPart.WorksheetThreadedCommentsParts` and names its
author by a person identifier resolved through the workbook's person part. A spreadsheet application
that writes a threaded comment normally *also* writes a legacy entry on the same cell, so that an
older reader still sees something there. Reporting both would show one remark twice, which a reader
would take as two reviewers raising the same point.

The implemented rule is positional, not textual:

> Prefer the threaded comment. Keep a legacy comment only where no threaded comment exists for that
> cell reference.

A textual rule — recognizing the backward-compatibility placeholder by its wording — was deliberately
**not** implemented. The placeholder text is not fixed: it varies by application version and locale,
and no genuine application-generated fixture was available to confirm any single pattern. Guessing at
it could either fail to suppress a real placeholder or suppress a real reviewer comment that happened
to resemble one. The positional rule cannot double-count and cannot discard an unpaired remark, which
is the property that matters.

Ordering follows the legacy part's cell order where one exists, because that part lists every
commented cell in sheet order; a thread is substituted in place of the legacy entry it pairs with.
Cells commented only in a threaded part follow afterward, in the order the threaded parts declare
them. Cell references are compared case-insensitively. Replies within a thread keep their declared
order, because a conversation read out of order misstates who answered whom.

### Key Methods

- **`ExcelWorkbookModel Read(Stream stream)`** — opens the package read-only; loads the shared-string
  table; loads the workbook's person display names; resolves the embedded images through
  `ExcelOpenXmlImageReader`; then walks the workbook's sheet
  list in order, resolving each worksheet part, reading its cells, merged ranges, images, charts (through
  `ExcelChartReader`), shape texts (through `ExcelDrawingTextReader`), and reviewer comments; and maps
  the package core
  properties to the metadata. Precondition: a readable, seekable stream. Postcondition: a model whose
  worksheets are in workbook (tab) order.
- **`LoadPersonNames`** (private) — reads the workbook's person parts up front so every threaded comment
  resolves its author without re-walking the package. A workbook with no person part yields an empty map,
  and every threaded comment is then reported unattributed rather than attributed to a guess.
- **`ReadComments`** (private) — reads both comment grammars for a worksheet and applies the
  deduplication rule described above, returning one ordered list keyed by cell reference.
- **`ReadLegacyComments`** (private) — reads `WorksheetCommentsPart`, resolving each comment's author
  index against the part's own author list. An index naming no author yields no attribution rather than a
  guess; a comment with neither text nor a cell reference is dropped because it cites nothing.
- **`ReadThreadedComments`** (private) — reads every `WorksheetThreadedCommentsPart` in document order,
  resolving each comment's person identifier against the loaded person names. An unresolved identifier
  yields no attribution rather than a raw identifier that would read as a name without being one.
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
