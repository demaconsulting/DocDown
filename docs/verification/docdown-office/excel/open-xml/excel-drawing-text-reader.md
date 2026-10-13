## ExcelDrawingTextReader Verification Design

This document describes the unit-level verification strategy for `ExcelDrawingTextReader`, which recovers
the text of the drawing shapes floating over a worksheet.

### Verification Approach

`ExcelDrawingTextReader` carries no dedicated test class, and no current test feeds it a real drawing part.
The linked test, `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet` in
`Markdown/ExcelContentEmitterTests.cs`, drives the emitter with a hand-built model whose shape text is
synthetic, so it verifies how the emitter renders shape text and does not execute `Collect`. Coverage of the
reader itself is therefore currently indirect; a later change adds tests that read a real workbook's
drawing shapes. The test link is retained until then.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a hand-built worksheet model carrying synthetic drawing-shape text
- **Filesystem**: none; the emitter is driven through a recording sink
- **Mocking**: a recording sink
- **Isolation**: each test constructs its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelDrawingTextReader` unit test run passes when a worksheet's drawing-shape
text is recovered in drawing order and written under the sheet under its own heading. The linked scenario
establishes only the downstream half: text present in the model is written under the sheet. A dropped
annotation or one attributed as cell content is a failure.

### Test Scenarios

#### Shape text in the model is written under the sheet (indirect evidence)

**Test**: `ExcelContentEmitter_Emit_ShapeText_WritesUnderTheSheet`

Proves the text of a callout carried by a hand-built model — text that exists in no cell — is written under
the sheet under its own heading. It does not prove that `ExcelDrawingTextReader.Collect` recovers that text
from a real drawing part. Linked as indirect evidence for
`DocDownExcel-OpenXml-ExcelDrawingTextReader-CollectsShapeText`.
