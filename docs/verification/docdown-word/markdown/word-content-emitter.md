## WordContentEmitter Verification Design

This document describes the unit-level verification strategy for `WordContentEmitter`, the
model-to-sink emission path.

### Verification Approach

`WordContentEmitter` is verified at two complementary levels. Dedicated unit tests in
`Markdown/WordContentEmitterTests.cs` drive `WordContentEmitter.EmitAsync()` directly from
hand-built `WordDocumentModel` instances through a `RecordingSink`, with no document behind them,
so content-inventory, metadata, and chart-note decisions can be proven in isolation. Integration
tests in `OpenXml/WordOpenXmlExtractorTests.cs` then drive a real extraction and assert what
reaches the sink for the content flow, merged-cell notes, and empty-document inventory.

The two levels answer different questions and neither alone is sufficient. A hand-built-model unit
test proves the emitter internally consistent but not that the extractor routes through it; an
integration test proves the routing but is a coarse instrument for a specific emitter-only
decision. Together they prove the emitter both correct in isolation and actually on the extraction
path.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `WordDocumentModel` instances through a `RecordingSink` for the unit
  tests; generated `.docx` fixtures from `TestData/DocxFixtures.cs` for the integration tests
- **Filesystem**: the unit tests touch no filesystem; per-test `TempScratch` folders receive one
  output folder per integration run
- **Mocking**: a `RecordingSink` substitutes for the write path in the unit tests; the integration
  tests use the real engine, backend, and sink
- **Isolation**: each test constructs its own model or scratch folder and its own engine

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordContentEmitter` unit test run passes when the emitted content reaches
the sink as one continuous flow; when the content inventory reports looked-for
counts, including zero text blocks for an empty document and distinct comment-author counts for
reviewed drafts; when document metadata is handed to the sink exactly once when present and not at
all when absent; and when incomplete steps are surfaced only as short notes for charts, flattened
and merged or nested table structure.

### Test Scenarios

#### The emitter writes content and reports inventory

**Tests**: `WordContentEmitter_Emit_BodyWithComments_ReportsOutlineAndSucceeds`,
`WordOpenXmlExtractor_Extract_EmptyDocument_ProducesZeroCountTextInventory`,
`WordOpenXmlExtractor_Extract_ImageLinks_ResolveOnDisk`

Prove the emitter writes content from the model, reports headings, comments, and distinct comment
authors in the inventory, reports zero text blocks for an empty document, and writes a content flow
whose image links resolve on disk through the real backend. Evidence for
`DocDownWord-Markdown-WordContentEmitter-EmitsModelContent` and
`DocDownWord-Markdown-WordContentEmitter-ReportsContentInventory`.

#### Charts are surfaced as a short note

**Test**: `WordContentEmitter_Emit_ChartsFound_ReportsChartNote`

Proves the emitter reports a note naming the chart count and stating that chart parts were not
read. Evidence for `DocDownWord-Markdown-WordContentEmitter-ReportsChartNote`.

#### Flattened table structure is surfaced as a short note

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsFlatteningNote`

Proves a real extraction reports a note when merged or nested table structure had to be flattened
for markdown. Evidence for `DocDownWord-Markdown-WordContentEmitter-ReportsFlattenedTableNote`.

#### Metadata is handed to the sink once when present

**Tests**: `WordContentEmitter_Emit_WithMetadata_ReportsDocumentMetadataOnce`,
`WordContentEmitter_Emit_NoMetadata_ReportsNoDocumentMetadata`

Prove the emitter reports captured document metadata once when the model carries it and reports
nothing when the model does not. Supporting evidence for the metadata handoff described in the
Markdown subsystem design.
