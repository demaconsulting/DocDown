## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, the reader-neutral
projection: the content emitter and the shape labeler.

### Verification Approach

The Markdown subsystem is verified through unit tests in `Markdown/VisioContentEmitterTests.cs` and
`Markdown/VisioShapeLabelerTests.cs` in `DemaConsulting.DocDown.Visio.Tests`.

Every emission decision is driven from a hand-built `VisioDocumentModel` with no drawing behind it,
so each mapping decision is proved in isolation: the page name heading, shape-text list, directed
`source -> target` edge list, omission and counting of bare-callout shapes and edges between
unidentified shapes, multi-line shape text kept inside its list item, inline image links that
resolve on disk under both single-flow and per-part split modes, looked-for content inventory,
force-PNG note reporting, empty-drawing zero inventory, and the labeling convention in content. The
shape labeler is asserted directly for every rendered label form.

The endpoint-label provenance the shape labeler carries exceeds the original Visio intent, which
asked only for the directed edges; those scenarios are verified here because the behavior is
genuinely tested.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `VisioDocumentModel` instances and synthetic image byte sequences; a
  recording sink captures content, parts, images, notes, and content features
- **Mocking**: none beyond the recording sink; the emitter and labeler are pure over the model
- **Isolation**: each test builds its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the emitter writes each page's
name, shape text, and directed topology; omits and counts bare-callout shapes and edges between
unidentified shapes; keeps multi-line shape text intact inside its item; links images only when a
path was returned; reports looked-for counts for pages, labeled shapes, and connections; records a
plain note for an unhonored force-PNG request; writes empty content plus zero-count inventory for an
empty drawing; and states the labeling convention in content only when needed. The shape labeler
must render authored text verbatim, a master type parenthesized with the shape id, and a bare shape
id where neither exists, refusing connective masters. Any silent omission, truncated text, dangling
link, or mislabeled endpoint is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioContentEmitter` and `VisioShapeLabeler` unit
verification chapters, each naming the requirement it evidences.
