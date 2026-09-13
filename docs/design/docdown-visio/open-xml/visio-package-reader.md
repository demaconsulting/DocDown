## VisioPackageReader

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioPackageReader` reads a Visio drawing's Open Packaging container directly with
`System.IO.Packaging` — because `DocumentFormat.OpenXml` has no Visio types — and produces the
backend-neutral `VisioDocumentModel`. Its single responsibility is to recover exactly what makes a
drawing a schematic: each page's name, the text of every shape that carries text, the master each
shape was instantiated from, and the directed connections resolved from the connector records.

### Data Model

`VisioPackageReader` is an `internal static class`. It knows the Visio 2012 main XML namespace, the
Open Packaging relationships namespace, and the conventional part URIs and content types of the
pages and masters collections. It holds a deliberately minimal symbol-font mapping table keyed by
font name so documented glyph recovery is applied only where the drawing's font metadata proves it
is correct. It performs no filesystem I/O; the caller hands it a seekable stream.

### Key Methods

- **`VisioDocumentModel Read(Stream stream)`** — opens the package, finds the pages part, reads the
  masters once, walks each page reading its name and shapes and resolving its connectors into
  directed edges, and returns the model with its pages, shapes, connections, images, and metadata in
  document order.
- **Page reading** (private) — reads each page's name and relationship to a page-contents part,
  reads the shapes and their text, applies documented symbol-font recovery where the run's declared
  font proves it, and reads the master reference so a text-less shape can still be classified.
- **Connector resolution** (private) — resolves each connector's `Connect` records into a directed
  edge between the two shapes, scoped to the page that owns them, yielding one edge per connector
  and handling records that appear in either order.
- **Master reading** (private) — reads `visio/masters/masters.xml` into a name map, falling back to
  the master's universal name where only that is present, and carrying no master name for a shape
  with no master, a dangling reference, or a drawing with no masters part.

### Error Handling

A package that is not a valid Open Packaging container, or a drawing with no pages part, is wrapped
in `VisioExtractionException` so Core can report an unreadable result with the full layout still
written. A page that carries no shapes with recoverable text and no connections is returned as an
empty page rather than an error. A page that repeats a shape id keeps the first declaration rather
than throwing.

### Dependencies

- **DocDown.Core** — `DocumentMetadata`, mapped from the OPC core properties.
- **System.IO.Packaging** — the OPC container reader.
- **System.Xml** — the `XDocument` reader for pages, shapes, connectors, and masters.
- **VisioDocumentModel** — the model records it populates.
- **VisioExtractionException** — the structured unreadable condition it raises.

### Callers

`VisioOpenXmlExtractor.ExtractAsync` calls `Read()` on the buffered drawing stream, and both the
extractor's self-test and the test fixtures build packages with `VisioPackageBuilder` for it to read
back.
