## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, which owns the
reader-neutral document model and its projection onto markdown: the model renderer, the GFM table
writer, and the content emitter that produces every output the model implies.

### Verification Approach

The Markdown subsystem is verified through unit tests exercising its three units —
`WordMarkdownWriter`, `WordTableWriter`, and `WordContentEmitter` — in
`Markdown/WordMarkdownWriterTests.cs`, `Markdown/WordTableWriterTests.cs`, and integration tests in
`OpenXml/WordOpenXmlExtractorTests.cs` that drive the emitter through the backend, all in
`DemaConsulting.DocDown.Word.Tests`.

The writer units are tested against **hand-built models with no document behind them**, because the
subsystem's contract is the projection from a model onto markdown; a document read is the reader's
job, and mixing the two would obscure which unit was responsible for a defect. The content emitter,
by contrast, is tested by driving a real extraction and asserting the content, diagnostics, and
counted gaps the model implies — because the emitter's promise is about what reaches the sink, and
a substitute sink would satisfy less than the real thing. The `WordDiagnosticCodes` table is pinned
by a reflection-driven contract test so a renumbering fails the build rather than silently changing
what downstream consumers branch on.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `WordDocumentModel` instances for the writer units; generated `.docx`
  documents from `TestData/DocxFixtures.cs` for the shared-emission scenarios
- **Filesystem**: none for the writer units; the shared-emission scenarios use per-test
  `TempScratch` folders
- **Mocking**: none; the writers are exercised directly and the emitter is driven through real
  backends
- **Isolation**: each test constructs its own model or its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the writer renders every block kind
the model carries — headings at their level, ordered and bulleted list items with the correct
markers and nesting, escaped inline text, bold, italic, and link runs, image links whose target is
the path the sink allocated, comments under a Comments section, footnotes as references and a
Footnotes section, and the Document Control section placed after the title heading and before the
body; when the table writer emits a header-and-delimiter GFM table, pads short rows, escapes pipes
and converts newlines within a cell, skips an empty table, and returns the count of merged and
nested cells it flattened; when the emitter writes the model's content in the requested split form
and reports the model's diagnostics and counted gaps; and when the
diagnostic-code table matches its pinned contract exactly.

### Test Scenarios

The subsystem's behavior is verified by the unit-level scenarios below and by the shared-emission
integration scenarios; the full per-scenario detail is given in the unit chapters.

#### The model renderer projects every block kind onto markdown

**Test**: `WordMarkdownWriter_Write_Headings_RendersHashLevels`

Proves the heading-level mapping directly. The full per-block detail — lists, escaping, inline
formatting, image links, comments, footnotes, and Document Control placement — is given in the
*WordMarkdownWriter Verification Design*. Evidence for `DocDownWord-Markdown-ModelRendering`.

#### The table writer emits GFM and counts flattened cells

**Test**: `WordTableWriter_Write_FullTable_ProducesGfmWithHeaderAndAlignment`

Proves the header row, the delimiter row, and the body all appear as GFM. The short-row padding,
cell escaping, empty-table skip, and merged-and-nested flatten count are given in the
*WordTableWriter Verification Design*. Evidence for `DocDownWord-Markdown-TableRendering`.

#### The emitter writes the model's content through the sink

**Test**: `WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1`

Proves the emitter writes the model's content through the sink in the requested split form, driven
only by what the model carries. Evidence for `DocDownWord-Markdown-ModelEmission`.

#### The diagnostic-code table is pinned

**Test**: `WordDiagnosticCodes_Table_MatchesPinnedContract`

Proves the codes a consumer branches on — `WORD0001` through `WORD0009`, with their names — are
exactly the pinned contract. This is a supporting scenario whose evidence sits at system level
under `DocDownWord-DiagnosticCodeStability`.
