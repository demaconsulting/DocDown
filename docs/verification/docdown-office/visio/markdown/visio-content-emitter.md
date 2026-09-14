## VisioContentEmitter Verification Design

This document describes the unit-level verification strategy for `VisioContentEmitter`, the single
emission path from the drawing model to the output contract.

### Verification Approach

`VisioContentEmitter` is verified through unit tests in `Markdown/VisioContentEmitterTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, driving every emission decision from a hand-built
`VisioDocumentModel` with no drawing behind it and capturing the output through a recording sink.
This proves each mapping decision in isolation: the directed topology with shape text, the
shape-id fallback, inline image links resolving on disk,
looked-for content inventory, empty-drawing zero inventory, bare-callout
omission and count, edge-between-unnamed-shapes omission and count, multi-line shape text preserved
inside its item, and the labeling convention in content.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `VisioDocumentModel` instances and synthetic image byte sequences; a
  recording sink captures content, parts, images, notes, and content features
- **Mocking**: none beyond the recording sink; the emitter is pure over the model
- **Isolation**: each test builds its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioContentEmitter` unit test run passes when the emitter writes each
page's name, shape text, and directed topology; omits and counts bare-callout shapes and edges
between unidentified shapes; keeps multi-line shape text intact inside its item; links images only
when a path was returned; reports looked-for counts for pages, labeled shapes, and connections;
writes empty content plus zero-count
inventory for an empty drawing; writes the drawing as one flow or one part per page; and states the
convention in the content only when a label is not authored text. Any silent omission, truncated
text, dangling link, or missing note is a failure.

### Test Scenarios

#### The directed topology is rendered with shape text

**Test**: `VisioContentEmitter_Emit_RendersDirectedTopologyWithShapeText`

Proves each page's name is a heading, its shape text a list, and its connections a
`source -> target` edge list. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-WritesPageName`,
`DocDownVisio-Markdown-VisioContentEmitter-WritesShapeText`, and
`DocDownVisio-Markdown-VisioContentEmitter-RendersDirectedTopology`.

#### Bare callout numbers are omitted and counted

**Test**: `VisioContentEmitter_Emit_BareCalloutNumbers_OmittedAndCounted`

Proves a shape whose text is a bare callout number is dropped from the listing and the count of what
was dropped is stated. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-OmitsBareCalloutNumbers`.

#### Multi-line shape text stays inside its list item

**Test**: `VisioContentEmitter_Emit_MultiLineShapeText_StaysInsideItsListItem`

Proves a shape's multi-line text is kept inside its item and never truncated. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-PreservesMultiLineShapeText`.

#### An edge between unnamed shapes is omitted and counted

**Test**: `VisioContentEmitter_Emit_EdgeBetweenUnnamedShapes_OmittedAndCounted`

Proves an edge whose endpoints are both unidentified is dropped and counted, while an edge with at
least one meaningful endpoint is kept. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-OmitsEdgesBetweenUnnamedShapes`.

#### Inline image links resolve on disk

**Tests**: `VisioContentEmitter_Emit_WithRasterImages_WritesThroughSink`,
`VisioContentEmitter_Emit_ImageLinks_ResolveOnDisk`,
`VisioContentEmitter_Emit_VectorImage_WritesSourceEncodingWithoutNote`

Prove a drawing's images are written through the sink in the encoding the drawing stored them in and
linked from the page that shows them, with links resolving on disk and only when a path was returned.
Evidence for `DocDownVisio-Markdown-VisioContentEmitter-LinksImages`.

#### Looked-for content inventory is reported

**Tests**: `VisioContentEmitter_Emit_NoImages_LeavesNotesEmptyAndReportsInventory`,
`VisioContentEmitter_Emit_NoConnections_ReportsZeroConnectionInventory`

Prove the emitter reports looked-for counts for pages, labeled shapes, and connections, including a
zero connection count when a drawing has no connections. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-ReportsLookedForInventory`.

#### An empty drawing writes empty content and zero-count inventory

**Test**: `VisioContentEmitter_Emit_EmptyDrawing_WritesEmptyContentAndZeroCountInventory`

Proves a drawing with no pages writes empty content and reports zero-count looked-for inventory.
Evidence for `DocDownVisio-Markdown-VisioContentEmitter-ReportsEmptyDrawingInInventory`.

#### The convention is stated in the content only when needed

**Tests**: `VisioContentEmitter_Emit_NonTextLabelsPresent_StatesConventionInContent`,
`VisioContentEmitter_Emit_AllEndpointsCarryText_OmitsConventionFromContent`

Prove the labeling convention is stated in the content when a label is not the shape's own text and
omitted when every endpoint carries authored text. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-StatesConventionInContent`.
