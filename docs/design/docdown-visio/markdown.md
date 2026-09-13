## Markdown Subsystem

![DocDown.Visio Structure](DocDownVisioView.svg)

### Overview

The Markdown subsystem is the reader-neutral projection of `DocDown.Visio`: it turns the backend-neutral
drawing model into the DocDown output. Its content emitter walks the model through the sink, writing one
section per page carrying the page name, the shape text, and — the headline capability — the directed
connector topology rendered as a readable `source -> target` edge list, plus the inline images, the
diagnostics, and the honest gaps. Its shape labeler decides how each topology endpoint is named, from what
the drawing actually says about it and nothing more.

Every mapping decision that differentiates a Visio extraction lives here, driven only by the model, so
each decision is testable from a hand-built model with no drawing behind it and the same path serves both
backends. A list of disconnected strings is not a schematic, so the topology is the emitter's central
concern; nothing is omitted silently, so an empty drawing is a counted gap, an empty page is noted
informationally, a bare-callout shape is dropped and counted, and an edge between two unidentified shapes
is dropped and counted.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from the extractor | .NET options object | Split mode, image output, and image limits |
| `VisioDocumentModel` | Inbound, from the reader | .NET record | The whole drawing to project |

### Design

**The content emitter.** `VisioContentEmitter` is a stateless static class whose `EmitAsync` reports the
page count, writes an empty-drawing gap (`VISIO0001`) and returns when the drawing has no pages, writes
the embedded images first so each page can link its pictures inline, writes the content as one flow or one
part per page honoring the split mode, reports the document info and metadata, publishes the topology
labeling convention and endpoint coverage, notes each empty page (`VISIO0002`) informationally, and
reports the image gaps and caveats. Each page renders its name as a heading, its informative shape text as
a list (multi-line text kept inside its item and never truncated, bare callout numbers dropped and
counted), its meaningful directed edges as a `source -> target` list (edges between two unidentified
shapes dropped and counted), and the images it shows linked inline only when the sink returned a path.

**The shape labeler.** `VisioShapeLabeler` decides an endpoint's label with a strict precedence: the
shape's own text always wins and is recorded as authored text; failing that, the master the shape was
instantiated from classifies it, rendered parenthesized and paired with the shape id so a type is never
mistaken for a name and two shapes of the same type stay distinguishable; a master describing connective
geometry — a dynamic connector, a bare line — is refused, because naming an endpoint after the wire would
report the stroke as the equipment; where neither fact exists the bare shape id remains, recorded as
unresolved, because an honest gap beats a plausible guess. It publishes the one-sentence convention that
describes every rendered form so a reader — human or machine — can decode a parenthesized label without
reverse-engineering it.

*(The endpoint-label provenance the shape labeler carries — authored text vs master type vs bare id, the
published convention, and the endpoint coverage counts — exceeds the Visio intent, which asked only for
the directed source-to-target edges. It is documented and covered here because it is genuinely tested; see
the developer report.)*

### The inline supporting types (D8)

- **`VisioDiagnosticCodes`** — the `VISIO` diagnostic-code constants (`VISIO0001`–`VISIO0006`), an
  internal contiguous, table-pinned set. It is a published output value, not an API consumers program
  against, and is reviewed in this subsystem set alongside its pinning test.
- **`NamespaceDoc`** — the namespace documentation type for the `Markdown` namespace.

The `VisioLabelSource` enum and the `VisioShapeLabel` record that carry an endpoint label's provenance are
defined alongside `VisioShapeLabeler` and are documented in its unit design.
