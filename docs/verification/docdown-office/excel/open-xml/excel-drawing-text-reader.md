## ExcelDrawingTextReader Verification Design

This document describes the unit-level verification strategy for `ExcelDrawingTextReader`, which recovers
the text of the drawing shapes floating over a worksheet.

### Verification Approach

`ExcelDrawingTextReader` is verified by `OpenXml/ExcelDrawingTextReaderTests.cs`, which opens a workbook
synthesized at test time and calls `Collect` on its real worksheet part. The emitter test
`ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet` in `Markdown/ExcelContentEmitterTests.cs` supplies the
downstream half: shape text present in the model is written under the sheet.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a workbook from `TestData/XlsxFixtures.cs` whose drawing holds shapes with line breaks, an empty
  shape, and a grouped shape; and a hand-built model for the emitter test
- **Filesystem**: none; the workbook is opened from a `MemoryStream`
- **Mocking**: none for the reader; a recording sink for the emitter test
- **Isolation**: each test opens its own package

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelDrawingTextReader` unit test run passes when a worksheet's drawing-shape text
is recovered in drawing order with line breaks read as spaces, empty shapes skipped, and grouped shapes
read, when a missing part or drawing yields nothing, and when text in the model is written under the sheet.
A dropped annotation, fused words, or text attributed as cell content is a failure.

### Test Scenarios

#### Shapes are read from a real drawing part

**Tests**: `ExcelDrawingTextReader_Collect_RealDrawing_ReadsShapesInOrderWithBreaksAsSpaces`,
`ExcelDrawingTextReader_Collect_NoPartOrNoDrawing_ReturnsEmpty`

Prove two paragraphs and a line break read as `Bearing retainer Part LX-4120`, an empty shape contributes
nothing, a grouped shape is read, and a missing part or drawing yields no text. Evidence for
`DocDownExcel-OpenXml-ExcelDrawingTextReader-CollectsShapeText`.

#### Shape text in the model is written under the sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves the text of a callout carried by a hand-built model is written under the sheet under its own
heading. Evidence for `DocDownExcel-OpenXml-ExcelDrawingTextReader-CollectsShapeText`.
