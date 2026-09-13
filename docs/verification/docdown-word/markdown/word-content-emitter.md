## WordContentEmitter Verification Design

This document describes the unit-level verification strategy for `WordContentEmitter`, the
model-to-sink emission path.

### Verification Approach

`WordContentEmitter` is verified at two complementary levels. Dedicated **unit** tests in
`Markdown/WordContentEmitterTests.cs` drive `WordContentEmitter.EmitAsync` directly from hand-built
`WordDocumentModel` instances through a `RecordingSink`, with no document behind them — the same
model-only pattern the sibling Excel, PowerPoint, and Visio content emitters use — so a mapping,
outline, gap, or metadata decision can be proven in isolation. **Integration** tests in
`OpenXml/WordOpenXmlExtractorTests.cs` then drive a real extraction and assert what reaches the sink,
proving the extractor really routes through the emitter rather than writing content around it. Both
projects live in `DemaConsulting.DocDown.Word.Tests`.

The two levels answer different questions and neither alone is sufficient. A hand-built-model unit test
proves the emitter internally consistent but not that the extractor uses it; an integration test proves
the routing but is a coarse instrument for a specific reporting decision. Together they prove the
emitter both correct in isolation and actually on the extraction path.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `WordDocumentModel` instances through a `RecordingSink` for the unit tests;
  a generated `.docx` multi-heading fixture and a generated `.docx` merged-cells
  fixture from `TestData/DocxFixtures.cs` for the integration tests
- **Filesystem**: the unit tests touch no filesystem; per-test `TempScratch` folders receive one
  output folder per integration run
- **Mocking**: a `RecordingSink` substitutes for the write path in the unit tests; the integration
  tests use the real engine, backend, and sink
- **Isolation**: each test constructs its own model or scratch folder and its own engine

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordContentEmitter` unit test run passes when a per-part extraction
produces the split content the model implies; and when the model's structural findings are reported
through the emitter as diagnostics whose codes match the pinned contract and as counted,
reason-bearing gaps that name the affected target, with the outcome degraded where the shortfall
warrants. Missing or unsplit content, a missing diagnostic, an uncounted gap, or a clean outcome for
a document that required flattening is a failure.

### Test Scenarios

#### The emitter writes the model's content in the requested split form

**Tests**: `WordContentEmitter_Emit_BodyWithComments_ReportsOutlineAndSucceeds`,
`WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Proves the emitter, driven only by a hand-built model, writes the content and reports the content
outline (headings, comments, and distinct comment authors) that makes the `## Comments` section
discoverable, and that a per-part extraction through the real backend produces a `parts/` folder with
one file per top-level heading — so the content reaching the sink is the content the model implies,
driven by the model alone and actually routed through the emitter. Evidence for
`DocDownWord-Markdown-WordContentEmitter-EmitsModelContent`.

#### Diagnostics and counted gaps reach the output through the emitter

**Tests**: `WordContentEmitter_Emit_ChartsFound_ReportsCountedGapAndDegrades`,
`WordContentEmitter_Emit_RenderPagesRequested_ReportsPagesGapAndDegrades`,
`WordOpenXmlExtractor_Extract_MergedCells_ReportsCountedStructuralGap`

Proves the emitter reports the model's structural findings as counted, degrading gaps: embedded charts
the backend does not read surface as `WORD0010` with a counted `Text`/`Unavailable` gap, a page-render
request surfaces as a `Pages` gap that never meets silence, and — through a real extraction — a
flattened-cell finding surfaces as `WORD0005` with a `PartiallyExtracted` structural gap while the
contract verifier reports no violations, so the reported gaps match what is on disk. Evidence for
`DocDownWord-Markdown-WordContentEmitter-ReportsGapsAndDiagnostics`.
