## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, which owns the
reader-neutral document model and its projection onto markdown: the model renderer, the GFM table
writer, and the content emitter that produces every output the model implies.

### Verification Approach

The Markdown subsystem is verified through unit tests exercising its three units —
`WordMarkdownWriter`, `WordTableWriter`, and `WordContentEmitter` — in
`Markdown/WordMarkdownWriterTests.cs`, `Markdown/WordTableWriterTests.cs`, and
`Markdown/WordContentEmitterTests.cs`, together with integration tests in
`OpenXml/WordOpenXmlExtractorTests.cs` that drive the emitter through the real backend.

The writer units are tested against hand-built models with no document behind them, because the
subsystem's contract is the projection from a model onto markdown. The content emitter is tested at
two levels: directly through a `RecordingSink` for inventory, metadata, and note decisions, and
through real extractions for split-output and end-to-end note behavior.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `WordDocumentModel` instances for the writer units; generated `.docx`
  documents from `TestData/DocxFixtures.cs` for integration scenarios
- **Filesystem**: none for the writer unit tests; the integration scenarios use per-test
  `TempScratch` folders
- **Mocking**: none for the writers; `RecordingSink` for direct emitter tests; the integration
  scenarios use the real engine and backend
- **Isolation**: each test constructs its own model or scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the writer renders every block kind
the model carries; when the table writer emits a header-and-delimiter GFM table, pads short rows,
escapes pipes, converts newlines within a cell, skips an empty table, and returns the count of
merged and nested cells it flattened; and when the emitter writes the model's content in the
requested split form, reports the content inventory, reports metadata once when present, and emits
only the notes implied by incomplete steps in the model or options.

### Test Scenarios

The subsystem's behavior is verified by the unit-level scenarios below and by the shared-emission
integration scenarios; the full per-scenario detail is given in the unit chapters.

#### The model renderer projects every block kind onto markdown

**Test**: `WordMarkdownWriter_Write_Headings_RendersHashLevels`

Proves the heading-level mapping directly. The full per-block detail — lists, escaping, inline
formatting, image links, comments, footnotes, and Document Control placement — is given in the
*WordMarkdownWriter Verification Design*. Evidence for `DocDownWord-Markdown-ModelRendering`.

#### The table writer emits GFM and reports its flattened-cell count

**Test**: `WordTableWriter_Write_FullTable_ProducesGfmWithHeaderAndAlignment`

Proves the header row, delimiter row, and body all appear as GFM. The short-row padding, cell
escaping, empty-table behavior, and merged-and-nested flatten count are given in the
*WordTableWriter Verification Design*. Evidence for `DocDownWord-Markdown-TableRendering`.

#### The emitter writes content and inventory through one path

**Tests**: `WordContentEmitter_Emit_BodyWithComments_ReportsOutlineAndSucceeds`,
`WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Prove the emitter, driven by a hand-built model, writes content and reports the inventory features
that make headings and comments discoverable, and that a real extraction writes split `parts/*.md`
output when requested. Evidence for `DocDownWord-Markdown-ModelEmission`.

#### Embedded charts are surfaced as a short note

**Test**: `WordContentEmitter_Emit_ChartsFound_ReportsChartNote`

Proves the emitter reports a plain note when the model says the document embedded charts whose
chart parts were not read. Evidence for
`DocDownWord-Markdown-WordContentEmitter-ReportsChartNote`.
