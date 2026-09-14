## OpenXml Subsystem

![DocDown.Visio Structure](DocDownVisioView.svg)

### Overview

The OpenXml subsystem is the guaranteed managed content path of `DocDown.Visio`. It is the
extractor the engine selects for ordinary Visio extraction, the package reader that turns a Visio
Open Packaging container into the backend-neutral drawing model, and the image reader that resolves
embedded image bytes and page association. Because `DocumentFormat.OpenXml` has no Visio types, the
subsystem reads the package directly with `System.IO.Packaging` and ships no native asset.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from engine | .NET interface | `Available()` without I/O |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| Source document | Inbound | `.vsdx` / `.vsdm` byte stream | Buffered because the package reader must seek |
| `System.IO.Packaging` | Outbound | OPC container API | Reads parts and relationships directly |

### Design

**The extractor.** `VisioOpenXmlExtractor` identifies `Vsdx` and `Vsdm`, carries priority `10`,
and leaves page rendering applicable for Visio drawings even though this backend does not render.
`ProbeAvailability()` returns `Available()` because the backend is fully managed. `ExtractAsync`
records the managed-path environment facts, buffers the source into memory, reads the model,
delegates emission to `VisioContentEmitter`, and returns `Produced` on normal completion.
`GetSelfTestCases()` contributes a topology round-trip and a rendering-skip case.

**The reader.** `VisioPackageReader` opens the package, reads `visio/pages/pages.xml` for each
page's name and page part, reads each page part's shapes and text, resolves connector `Connect`
records into directed edges, reads masters so a text-less shape can still be classified by its
master type, and maps documented symbol-font glyphs when the drawing's font metadata proves the
mapping is correct. Packaging faults are wrapped in `VisioExtractionException` so Core can report an
unreadable result.

**The image reader.** `VisioImageReader` follows internal image relationships, yields image bytes
unchanged, deduplicates by media part, records the pages that reference each image, flags master-only
images as template furniture, and excludes the package thumbnail.

### The inline supporting types (D8)

- **`VisioDocumentModel`** and its records (`VisioPageModel`, `VisioShapeModel`,
  `VisioConnectionModel`, `VisioPageImageRef`) — the shared model the reader populates and the
  emitter consumes.
- **`VisioExtractionException`** — the exception type this package raises for unreadable Visio
  package conditions.
- **`VisioPackageBuilder`** — the in-memory package synthesizer used by self-tests and fixtures.
- **`NamespaceDoc`** — the namespace documentation type for `OpenXml`.
