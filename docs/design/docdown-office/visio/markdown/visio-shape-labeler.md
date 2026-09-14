## VisioShapeLabeler

![DocDown.Visio Structure](VisioView.svg)

### Purpose

`VisioShapeLabeler` decides how a shape is named in the rendered topology, from what the drawing
actually says about it and nothing more. Its single responsibility is to turn a connector
endpoint's shape id into a truthful label whose provenance is carried in the data, so a reader can
tell an authored name from a classification from an unresolved fallback.

*(This endpoint-label provenance exceeds the Visio intent, which asked only for the directed
source-to-target edges. It is documented and covered because it is genuinely tested.)*

### Data Model

`VisioShapeLabeler` is an `internal static class`. Alongside it are the `VisioLabelSource` enum
(`Text`, `Type`, `Unresolved`) and the `VisioShapeLabel` record (the display string and its
source), which carry the provenance of every label so the distinction is in the data rather than
inferred from the rendered string. Two static tables classify masters: a set of line-drawing
master names and the substring `connector`, which marks a connective master.

### Key Methods

- **`VisioShapeLabel Label(string id, IReadOnlyDictionary<string, VisioShapeModel> shapes)`** —
  returns the shape's own text when it carries any, otherwise a non-connective master name
  parenthesized with the shape id, otherwise the bare shape-id label. The precedence is strict:
  authored text outranks type, and type outranks the unresolved fallback.
- **`bool IsConnectiveMaster(string name)`** (private) — decides whether a master name describes
  connective geometry rather than a component, so such a master is refused as an endpoint label.
- **`Convention`** (constant) — the one-sentence statement of the labeling convention, published
  verbatim into page content whenever a page uses a non-authored endpoint label.

### Error Handling

`Label()` is pure and total: an endpoint naming a shape absent from the map falls back to the
shape-id label rather than throwing. Null arguments are rejected with `ArgumentNullException`.

### Dependencies

- **VisioDocumentModel** — the `VisioShapeModel` whose text and master name it reads.
- **System.Globalization** — invariant-culture formatting of parenthesized labels.

### Callers

`VisioContentEmitter` calls `Label()` for every connector endpoint when rendering topology and when
counting the connections that reach the output. It emits `Convention` into the content whenever a
page uses a type-derived or shape-id label.
