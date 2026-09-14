## ExcelDrawingTextReader Verification Design

This document describes the unit-level verification strategy for `ExcelDrawingTextReader`, which recovers
the text of the drawing shapes floating over a worksheet.

### Verification Approach

`ExcelDrawingTextReader` carries no dedicated test class; its observable contract is the shape text that
reaches the model and then the sheet, so it is verified **through the emitter scenario** that renders that
text, `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet` in
`Markdown/ExcelContentEmitterTests.cs`, in `DemaConsulting.DocDown.Excel.Tests`. The shape text in the
model is synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a hand-built worksheet model carrying synthetic drawing-shape text
- **Filesystem**: none; the emitter is driven through a recording sink
- **Mocking**: a recording sink
- **Isolation**: each test constructs its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelDrawingTextReader` unit test run passes when a worksheet's drawing-shape
text is recovered and written under the sheet under its own heading, so text that lives in no cell is not
dropped. A dropped annotation or one attributed as cell content is a failure.

### Test Scenarios

#### Shape text is recovered and written under the sheet

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves the text of a callout drawn over a worksheet — text that exists in no cell — is recovered and
written under the sheet under its own heading, so a reader of the cell listing alone still sees it.
Evidence for `DocDownExcel-OpenXml-ExcelDrawingTextReader-CollectsShapeText`.
