## WordContentEmitter Verification Design

This document describes the unit-level verification strategy for `WordContentEmitter`, the
model-to-sink emission path.

### Verification Approach

`WordContentEmitter` is verified through integration tests that drive a real extraction and assert
what reaches the sink: the content in the requested split form, and the diagnostics and counted gaps
the model records. The tests live in `OpenXml/WordOpenXmlExtractorTests.cs` in
`DemaConsulting.DocDown.Word.Tests`.

Driving a real extraction is deliberate. A test that only asserted the emitter's output against a
hand-built model and a substitute sink would prove the emitter internally consistent but leave open
whether the extractor really routes through it: a defect that wrote content around the emitter would
still satisfy such a test. Asserting the files and reports a real extraction produces proves the
property directly.

The gap-and-diagnostic scenario is asserted against a real extraction with merged cells: the
subject is the emitter's reporting of what the model records, and a synthesized model would not
exercise the flow the extractor is expected to take.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a generated `.docx` multi-heading fixture and a generated `.docx` merged-cells
  fixture from `TestData/DocxFixtures.cs`
- **Filesystem**: per-test `TempScratch` folders receive one output folder per run
- **Mocking**: none; the real engine, the real backend, and the real sink are used
- **Isolation**: each test constructs its own scratch folder and its own engine

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordContentEmitter` unit test run passes when a per-part extraction
produces the split content the model implies; and when the model's structural findings are reported
through the emitter as diagnostics whose codes match the pinned contract and as counted,
reason-bearing gaps that name the affected target, with the outcome degraded where the shortfall
warrants. Missing or unsplit content, a missing diagnostic, an uncounted gap, or a clean outcome for
a document that required flattening is a failure.

### Test Scenarios

#### The emitter writes the model's content in the requested split form

**Test**: `WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Proves a per-part request produces a `parts/` folder with one file per top-level heading, so the
content reaching the sink is the content the model implies under the requested split mode, driven by
the model alone. Evidence for `DocDownWord-Markdown-WordContentEmitter-EmitsModelContent`.

#### Diagnostics and counted gaps reach the output through the emitter

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsCountedStructuralGap`

Proves the model's flattened-cell finding surfaces as `WORD0005` in the diagnostics collection and
as a structural gap of `PartiallyExtracted` scope whose affected count is at least one; the
contract verifier reports no violations, so the reported gap matches what is on disk. Evidence for
`DocDownWord-Markdown-WordContentEmitter-ReportsGapsAndDiagnostics`.
