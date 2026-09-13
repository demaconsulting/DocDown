## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, the reader-neutral
projection: the content emitter and the shape labeler.

### Verification Approach

The Markdown subsystem is verified through unit tests in `Markdown/VisioContentEmitterTests.cs` and
`Markdown/VisioShapeLabelerTests.cs` in `DemaConsulting.DocDown.Visio.Tests`, plus the diagnostic-code
pinning test in `Markdown/VisioDiagnosticCodesTests.cs`.

Every emission decision is driven from a hand-built `VisioDocumentModel` with no drawing behind it, so each
mapping decision is proved in isolation: the page name heading, the shape-text list, the directed
`source -> target` edge list, the omission and counting of bare-callout shapes and of edges between
unidentified shapes, the multi-line shape text kept inside its list item without truncation, the inline
image links that resolve on disk under both the single-flow and per-part split modes, the vector-image
informational caveat with no images gap, and the empty-drawing counted gap. The shape labeler is asserted
directly for every rendered label form, and the emitter is asserted to state the labeling convention in the
content and to publish the convention and the honest endpoint coverage as diagnostics. The diagnostic-code
set is asserted to be exact and contiguous by a pinning test.

The endpoint-label provenance the shape labeler and the emitter's labeling diagnostics carry **exceeds the
Visio intent**, which asked only for the directed edges; those scenarios are verified here because the
behavior is genuinely tested, and are covered as an additional honesty measure rather than because the
intent called for them.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `VisioDocumentModel` instances and synthetic image byte sequences; a recording
  sink captures the content, parts, images, gaps, diagnostics, and content features
- **Mocking**: none beyond the recording sink; the emitter and labeler are pure over the model
- **Isolation**: each test builds its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the emitter writes each page's name, shape
text, and directed topology; omits and counts bare-callout shapes and edges between unidentified shapes;
keeps multi-line shape text intact inside its item; links images only when a path was returned; records the
vector caveat informationally with no gap; degrades with a counted gap on an empty drawing; states the
convention in the content only when a label is not authored text; publishes the convention and the honest
endpoint coverage; and when the shape labeler renders authored text verbatim, a master type parenthesized
with the shape id, and a bare shape id where neither exists, refusing connective masters. Any silent
omission, truncated text, dangling link, mislabeled endpoint, or drifted diagnostic-code set is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioContentEmitter` and `VisioShapeLabeler` unit verification
chapters, each naming the requirement it evidences.
