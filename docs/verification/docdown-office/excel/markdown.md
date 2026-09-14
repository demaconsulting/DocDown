## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, which owns the projection
of the workbook model onto markdown: the content emitter that produces every output the model implies,
and the chart writer that renders a chart's cached series as a table.

### Verification Approach

The Markdown subsystem is verified through unit tests exercising its two units — `ExcelContentEmitter`
and `ExcelChartWriter` — in `Markdown/ExcelContentEmitterTests.cs` and `Markdown/ExcelChartWriterTests.cs`,
plus system-level scenarios in `DocDownExcelTests.cs`, all in `DemaConsulting.DocDown.Excel.Tests`.

Both units are tested against **hand-built models with no workbook behind them**, because the
subsystem's contract is the projection from a model onto markdown; a workbook read is the reader's job,
and mixing the two would obscure which unit was responsible for a defect. The emitter is driven through
a recording sink so the parts, content counts, and short notes it produces can be asserted directly, and
the chart writer is a pure function whose markdown is asserted from a hand-built chart model. The
reporting rule is asserted case by case: an empty workbook is reported through zero-count inventory, an
empty sheet states its own absence of cell content, an unreadable chart records a short note, a chart
with no cached data or a bounded table states that fact in the chart part, and a successfully written
vector image records no vector-only note.

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
merged-range note, writes a chart part per chart, links images inline only when a path was returned,
reports zero counts for features it explicitly looked for but did not find, records a short note only
when an attempted chart or image step could not complete, states an empty worksheet in the sheet part,
keeps page requests silent, and reports the content outline from the model; and when the chart writer
renders a cached series as a table with a leading point-index column, honors sparse indices, escapes
pipes, bounds the plotted points and states what it omitted, and states an unreadable or uncached chart
in the part itself. A missing part, a wrong note, a missing zero count, or a weakened verbatim listing
is a failure.

### Test Scenarios

The per-unit scenarios are given in the `ExcelContentEmitter` and `ExcelChartWriter` unit verification
chapters, each naming the requirement it evidences.
