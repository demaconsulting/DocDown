## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, which owns the projection
of the workbook model onto markdown: the content emitter that produces every output the model implies,
and the chart writer that renders a chart's cached series as a table.

### Verification Approach

The Markdown subsystem is verified through unit tests exercising its two units — `ExcelContentEmitter`
and `ExcelChartWriter` — in `Markdown/ExcelContentEmitterTests.cs` and `Markdown/ExcelChartWriterTests.cs`,
plus system-level scenarios in `DocDownExcelTests.cs`, all in `DemaConsulting.DocDown.Excel.Tests`.

Both units are tested against **hand-built models with no workbook behind them**, because the subsystem's
contract is the projection from a model onto markdown; a workbook read is the reader's job, and mixing the
two would obscure which unit was responsible for a defect. The emitter is driven through a recording sink
so the parts, diagnostics, and counted gaps it produces can be asserted directly, and the chart writer is
a pure function whose markdown and accounting are asserted from a hand-built chart model. The
gap-versus-diagnostic policy is the subsystem's whole honesty and is asserted case by case: an empty
workbook degrades, an empty sheet does not, a vector caveat is informational, and an unreadable, uncached,
or bounded chart is a counted gap.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `ExcelWorkbookModel` and `ExcelChartModel` instances; every value, sheet name,
  and series in them is synthetic
- **Filesystem**: none — the emitter is driven through a recording sink and the chart writer is pure
- **Mocking**: a recording sink captures the emitter's output; no other substitute is used
- **Isolation**: each test constructs its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the emitter writes a titled sheet part
per worksheet, renders the verbatim listing and the additive grid table with its table-only elision and
merged-range note, writes a chart part per chart, links images inline only when a path was returned, and
reports the empty-workbook, empty-sheet, vector-caveat, and chart gaps with the correct
degrade-versus-inform outcome; and when the chart writer renders a cached series as a table with a leading
point-index column, honors sparse indices, escapes pipes, bounds the plotted points and states what it
dropped, and states an unreadable or uncached chart in the part itself. A missing part, a wrong outcome, a
weakened verbatim listing, or an uncounted gap is a failure.

### Test Scenarios

The per-unit scenarios are given in the `ExcelContentEmitter` and `ExcelChartWriter` unit verification
chapters, each naming the requirement it evidences.
