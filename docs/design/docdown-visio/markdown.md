## Markdown Subsystem

![DocDown.Visio Structure](DocDownVisioView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.Visio`. It turns the
backend-neutral drawing model into DocDown content: per-page headings, informative shape text,
directed topology, inline image links, looked-for content inventory, document info, metadata, and
plain notes for attempted steps DocDown could not complete. Its shape labeler decides how topology
endpoints are named from what the drawing actually says about them.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from the extractor | .NET options object | Image size limits |
| `VisioDocumentModel` | Inbound, from the reader | .NET record | The whole drawing to project |

### Design

**The content emitter.** `VisioContentEmitter` is a stateless static class. `EmitAsync` reports
looked-for content inventory for pages, labeled shapes, and connections before any content is
written. When the drawing has no pages, it writes empty content, document info with a zero page
count, and any captured metadata, then returns. Otherwise it writes embedded images first so page
sections can link them, writes content either as one flow or one part per page, writes document info
and metadata, and records a plain note when a caller requested PNG output for embedded images but
this backend had to preserve the source encoding instead.

Each page renders its name as a heading, its informative shape text as a list, its meaningful
connections as a directed `source -> target` list, and the images it shows as inline links only when
the sink returned a path. Bare numeric callouts and edges whose endpoints are both unresolved are
omitted from the content and counted in page prose so the omission remains visible.

**The shape labeler.** `VisioShapeLabeler` uses strict precedence: the shape's own text wins,
otherwise a non-connective master name classifies the shape, otherwise the bare shape id remains.
Type-derived labels are parenthesized with the shape id so they cannot be mistaken for authored
names, and the emitter states the convention in the page content whenever a rendered label is not
plain authored text.

### The inline supporting types (D8)

- **`VisioLabelSource`** and **`VisioShapeLabel`** — the provenance model for topology endpoint
  labels.
- **`NamespaceDoc`** — the namespace documentation type for `Markdown`.

The `VisioLabelSource` enum and `VisioShapeLabel` record are defined alongside
`VisioShapeLabeler` and are documented in that unit design.
