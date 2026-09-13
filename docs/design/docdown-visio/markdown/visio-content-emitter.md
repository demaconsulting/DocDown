## VisioContentEmitter

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioContentEmitter` is the single emission path of `DocDown.Visio`: it writes a read `VisioDocumentModel`
through the sink as per-page content in document order — the page name, the shape text, and the directed
connector topology — plus its inline images, its diagnostics, and the honest gaps the model and options
imply. The topology is the engineering content, so it is the emitter's central concern.

### Data Model

`VisioContentEmitter` is an `internal static class`. It holds no state; it drives everything from the model
and the options handed to `EmitAsync`. The directed edge is rendered with a Unicode right arrow, and a
shape's multi-line text is kept inside its markdown list item with a continuation indent and a hard line
break.

### Key Methods

- **`ValueTask<bool> EmitAsync(IExtractionSink sink, ExtractionOptions options, VisioDocumentModel model,
  CancellationToken cancellationToken)`** — reports the page count, writes an empty-drawing gap
  (`VISIO0001`) and returns when the drawing has no pages, writes the embedded images first, writes the
  content, reports the document info and metadata, publishes the topology labeling convention and coverage,
  notes each empty page (`VISIO0002`), and reports the image gaps and caveats. Returns whether the run
  degraded.
- **`RenderPage`** (private) — renders one page: its name as a heading, its informative shape text as a
  list, its meaningful directed edges as a `source -> target` list, and the images it shows linked inline.
  Bare-callout shapes and edges between two unidentified shapes are dropped and counted so the omission is
  visible; the labeling convention is stated in the content whenever a label is not the shape's own text.
- **`ReportContentFeatures`** (private) — reports the outline counts (pages, labeled shapes, connections)
  from the model, counting only edges that reach the output.
- **`ReportTopologyLabeling`** (private) — publishes the `VISIO0005` convention diagnostic and the
  `VISIO0006` endpoint coverage counts (by text, by master type, and unresolved), reporting neither when
  the drawing has no connections.
- **`ReportImages`** / **`ReportVectorImageDiagnostic`** / **`ReportSizeSkipGap`** / **`ReportForcePngGap`**
  (private) — report the vector-image caveat (`VISIO0003`, informational, never a gap), a size-skip counted
  gap, and a force-PNG counted gap for the image write accounting.
- **`AsListItem`** / **`Informative`** / **`IndexShapes`** (private) — keep multi-line text inside its item
  without truncation, drop shapes whose text carries no letter, and index a page's shapes by id for
  endpoint resolution.

The force-PNG and size-skip image behavior is inherited from the product-wide embedded-image contract; the
Visio suite exercises the vector-caveat, raster-write, and inline-link paths but has no force-PNG or
size-skip scenario, so those paths are described here as inherited behavior rather than claimed as
Visio-specific evidence. *(See the developer report.)*

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed between pages. The
emitter performs no filesystem I/O of its own; every byte goes through the sink, and a malformed drawing
that repeats a shape id degrades to reporting the first declaration rather than throwing.

### Dependencies

- **DocDown.Core** — the sink, the options, the content-part and gap types, and the diagnostic types.
- **VisioDocumentModel** — the model it projects.
- **VisioShapeLabeler** — the endpoint-label decision and the published convention.
- **VisioDiagnosticCodes** — the `VISIO0001`–`VISIO0006` codes.

### Callers

`VisioOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after reading the model, and the COM backend reaches
it through the same delegated managed extraction, so a drawing's content reads identically whether or not
it was rendered.
