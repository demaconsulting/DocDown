## VisioShapeLabeler

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioShapeLabeler` decides how a shape is named in the rendered topology, from what the drawing actually
says about it and nothing more. Its single responsibility is to turn a connector endpoint's shape id into
a truthful label whose provenance is carried in the data, so a reader can tell an authored name from a
classification from an admission of ignorance.

*(This endpoint-label provenance exceeds the Visio intent, which asked only for the directed
source-to-target edges. It is documented and covered because it is genuinely tested; see the developer
report.)*

### Data Model

`VisioShapeLabeler` is an `internal static class`. Alongside it are the `VisioLabelSource` enum (`Text`,
`Type`, `Unresolved`) and the `VisioShapeLabel` record (the display string and its source), which carry the
provenance of every label so the distinction is in the data rather than inferred from the rendered string.
Two static tables classify masters: a set of line-drawing master names, and the substring "connector" that
marks a connective master.

### Key Methods

- **`VisioShapeLabel Label(string id, IReadOnlyDictionary<string, VisioShapeModel> shapes)`** — the shape's
  own text when it carries any (source `Text`); otherwise its master name, parenthesized with the shape id
  (`(3-way Plug Valve, shape 34)`, source `Type`), when the master describes a component; otherwise the
  bare shape-id label (`(shape 96)`, source `Unresolved`). The precedence is strict: authored text always
  outranks type, and type always outranks the id fallback.
- **`bool IsConnectiveMaster(string name)`** (private) — decides whether a master name describes connective
  geometry (a connector or a bare line) rather than a component, so such a master is refused as an endpoint
  label and the honest id fallback is kept.
- **`Convention`** (constant) — the one-sentence statement of the labeling convention, published verbatim
  into both the markdown and the manifest diagnostic so the two can never drift apart.

### Error Handling

`Label` is pure and total: an endpoint naming a shape absent from the map degrades to the id fallback
rather than throwing. Null arguments are rejected with `ArgumentNullException`.

### Dependencies

- **VisioDocumentModel** — the `VisioShapeModel` whose text and master name it reads.
- **System.Globalization** — the invariant-culture formatting of the parenthesized labels.

### Callers

`VisioContentEmitter` calls `Label` for every connector endpoint when rendering a page's topology, when
counting the meaningful edges for the content outline, and when reporting the endpoint coverage; it emits
the published `Convention` into the content and the manifest.
