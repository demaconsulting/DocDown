## VisioContentEmitter

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioContentEmitter` is the single emission path of `DocDown.Visio`. Its single responsibility is
to turn a read `VisioDocumentModel` into per-page content, looked-for content inventory, image
links, document info, metadata, and plain notes, all written through the sink. The topology is the
engineering content, so the emitter centers the output on readable directed connections.

### Data Model

`VisioContentEmitter` is an `internal static class`. It holds no state; it drives everything from
the model and the options handed to `EmitAsync`. Named constants pin the arrow used between
connected shapes and the markdown formatting needed to keep multi-line shape text inside one list
item.

### Key Methods

- **`ValueTask EmitAsync(IExtractionSink sink, ExtractionOptions options, VisioDocumentModel model,
  CancellationToken cancellationToken)`** — reports looked-for inventory for pages, labeled shapes,
  and connections; writes empty content immediately when the drawing has no pages; otherwise writes
  images first, writes page content as one flow or one part per page, writes document info and any
  captured metadata, and records a plain note when a requested PNG output could not be honored for
  embedded images.
- **`WriteContentAsync`** (private) — writes the drawing as one content flow, each page under its
  own heading.
- **`RenderPage`** (private) — renders one page: its name as a heading, informative shape text as a
  list, meaningful directed edges as a `source -> target` list, the labeling convention when needed,
  omitted-callout and omitted-edge counts, and inline images whose paths were returned by the sink.
- **`ReportContentFeatures`** (private) — reports looked-for counts for pages, labeled shapes, and
  connections, counting only the edges that reach the output.
- **`AsListItem`** / **`Informative`** / **`IndexShapes`** (private) — preserve multi-line text,
  decide whether a shape text is informative, and build a page-local shape map for endpoint
  resolution.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed between pages.
The emitter performs no filesystem I/O of its own; every byte goes through the sink. A malformed
page that repeats a shape id keeps the first declaration rather than aborting an otherwise complete
extraction.

### Dependencies

- **DocDown.Core** — the sink, options, content-part types, `DocumentInfo`, `ContentFeature`,
  `EmbeddedImageWriter`, and `ExtractionNote`.
- **VisioDocumentModel** — the model it projects.
- **VisioShapeLabeler** — the endpoint-label decision and published convention.

### Callers

`VisioOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after reading the model, and the COM backend
reaches it through the same delegated managed extraction, so a drawing's content reads identically
whether or not it was rendered.
