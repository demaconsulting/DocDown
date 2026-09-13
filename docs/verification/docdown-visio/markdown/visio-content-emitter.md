## VisioContentEmitter Verification Design

This document describes the unit-level verification strategy for `VisioContentEmitter`, the single emission
path.

### Verification Approach

`VisioContentEmitter` is verified through unit tests in `Markdown/VisioContentEmitterTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, driving every emission decision from a hand-built `VisioDocumentModel`
with no drawing behind it and capturing the output through a recording sink. This proves each mapping
decision in isolation: the directed topology with shape text, the shape-id fallback, the per-part split, the
inline image links resolving on disk under both split modes, the vector-image caveat with no images gap, the
empty-drawing gap, the bare-callout omission and count, the edge-between-unnamed-shapes omission and count,
the multi-line shape text kept inside its item, and the labeling convention and honest endpoint coverage.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `VisioDocumentModel` instances and synthetic image byte sequences; a recording sink
  captures content, parts, images, gaps, diagnostics, and content features
- **Mocking**: none beyond the recording sink; the emitter is pure over the model
- **Isolation**: each test builds its own model and sink

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioContentEmitter` unit test run passes when the emitter writes each page's name,
shape text, and directed topology; omits and counts bare-callout shapes and edges between unidentified
shapes; keeps multi-line shape text intact inside its item; links images only when a path was returned;
records the vector caveat informationally with no gap; degrades with a counted gap on an empty drawing;
writes the drawing as one flow or one part per page; states the convention in the content only when a label
is not authored text; and publishes the convention and the endpoint coverage. Any silent omission, truncated
text, dangling link, or degrading vector caveat is a failure.

### Test Scenarios

#### The directed topology is rendered with shape text

**Test**: `VisioContentEmitter_Emit_RendersDirectedTopologyWithShapeText`

Proves each page's name is a heading, its shape text a list, and its connections a `source -> target` edge
list. Evidence for `DocDownVisio-Markdown-VisioContentEmitter-WritesPageName`,
`DocDownVisio-Markdown-VisioContentEmitter-WritesShapeText`, and
`DocDownVisio-Markdown-VisioContentEmitter-RendersDirectedTopology`.

#### Bare callout numbers are omitted and counted

**Test**: `VisioContentEmitter_Emit_BareCalloutNumbers_OmittedAndCounted`

Proves a shape whose text is a bare callout number is dropped from the listing and the count of what was
dropped is stated. Evidence for `DocDownVisio-Markdown-VisioContentEmitter-OmitsBareCalloutNumbers`.

#### Multi-line shape text stays inside its list item

**Test**: `VisioContentEmitter_Emit_MultiLineShapeText_StaysInsideItsListItem`

Proves a shape's multi-line text is kept inside its item and never truncated. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-PreservesMultiLineShapeText`.

#### An edge between unnamed shapes is omitted and counted

**Test**: `VisioContentEmitter_Emit_EdgeBetweenUnnamedShapes_OmittedAndCounted`

Proves an edge whose endpoints are both unidentified is dropped and counted, while an edge with at least one
meaningful endpoint is kept. Evidence for `DocDownVisio-Markdown-VisioContentEmitter-OmitsEdgesBetweenUnnamedShapes`.

#### Inline image links resolve on disk

**Tests**: `VisioContentEmitter_Emit_AutoImageLinks_ResolveOnDisk`,
`VisioContentEmitter_Emit_PerPartImageLinks_ResolveOnDisk`,
`VisioContentEmitter_Emit_WithRasterImages_WritesThroughSink`,
`VisioContentEmitter_Emit_NoImages_NoImagesGap`

Prove a drawing's images are written through the sink and linked from the page that shows them, with links
resolving on disk under both split modes and only when a path was returned, and no images gap for a drawing
that embeds none. Evidence for `DocDownVisio-Markdown-VisioContentEmitter-LinksImages`.

#### A vector image is written with an informational caveat

**Test**: `VisioContentEmitter_Emit_VectorImage_WritesWithInfoCaveat`

Proves an EMF or WMF metafile is written unchanged and the `VISIO0003` caveat is informational, with no
images gap. Evidence for `DocDownVisio-Markdown-VisioContentEmitter-ReportsVectorImageCaveat`.

#### An empty drawing reports a counted gap

**Test**: `VisioContentEmitter_Emit_EmptyDrawing_ReportsGap`

Proves a drawing with no pages degrades with a counted gap. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-ReportsEmptyDrawingGap`.

#### The drawing splits into per-page parts

**Test**: `VisioContentEmitter_Emit_PerPart_WritesPageParts`

Proves the drawing is written as one part per page under the per-part split mode. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-SplitsLargeDrawings`.

#### The convention is stated in the content only when needed

**Tests**: `VisioContentEmitter_Emit_NonTextLabelsPresent_StatesConventionInContent`,
`VisioContentEmitter_Emit_AllEndpointsCarryText_OmitsConventionFromContent`

Prove the labeling convention is stated in the content when a label is not the shape's own text and omitted
when every endpoint carries authored text. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-StatesConventionInContent`.

#### The convention and endpoint coverage are published

**Tests**: `VisioContentEmitter_Emit_WithConnections_ReportsHonestEndpointCoverage`,
`VisioContentEmitter_Emit_WithConnections_ReportsConventionDiagnostic`,
`VisioContentEmitter_Emit_NoConnections_ReportsNoLabelingDiagnostics`

Prove the convention and the counts of endpoint occurrences (by text, by master type, and unresolved) are
published as diagnostics, and neither is reported for a drawing with no connections. Evidence for
`DocDownVisio-Markdown-VisioContentEmitter-ReportsEndpointCoverage`.
