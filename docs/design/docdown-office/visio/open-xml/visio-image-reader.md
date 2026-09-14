## VisioImageReader

![DocDown.Visio Structure](VisioView.svg)

### Purpose

`VisioImageReader` resolves the embedded images of a Visio drawing to their bytes and provenance, directly
from the Open Packaging container because `DocumentFormat.OpenXml` has zero Visio types. Its single
responsibility is to yield each distinct embedded image once and record which page references it, so the
image-to-page association survives extraction and an image is citable back to the page it appears on.

### Data Model

`VisioImageReader` is an `internal static class`. It knows the Open Packaging image relationship type, the
document-properties path prefix (where the thumbnail lives), and the masters path marker. It returns a
`VisioImageCollection` — the distinct images in package-part order, each carrying every referring page, and
the per-page ordered image references used for inline linking. A private `PartAccumulator` gathers each
distinct media part's referring pages and its template flag while the package is walked.

### Key Methods

- **`VisioImageCollection Collect(Package package, IReadOnlyDictionary<string, int> pageIndexByContentsUri)`**
  — walks the container's parts in URI order, follows every internal image relationship to its media part,
  accumulates each distinct part once with its referring pages, builds the distinct images in
  first-occurrence order, and resolves each page's ordered image occurrences into inline references.
- **`AddPageRef`** (private) — adds a media part to a page's ordered, distinct image references.
- **`BuildImage`** (private) — reads an accumulated media part's bytes once and builds its reference with
  its name and its full referrer set, flagging a template-only image.
- **`ReadPartBytes`** / **`NameFromUri`** (private) — read a part fully and derive a base name from its URI,
  since a foreign-data image carries no authored description.

### Error Handling

Only internal image relationships are followed, so an external image (a link, not embedded content) is
skipped. The package thumbnail is excluded both by following only image relationships and, as a safeguard,
by its document-properties path. A relationship part is skipped because asking it for its own relationships
would throw. A missing relationship target is skipped rather than failing the read.

### Dependencies

- **DocDown.Core** — `EmbeddedImage` and its provenance fields (source pages, template-referenced flag).
- **System.IO.Packaging** — the OPC container, its parts, and their relationships.
- **VisioPageImageRef** — the per-page inline image reference type.

### Callers

`VisioPackageReader` calls `Collect` with the opened package and the page-contents URI-to-index map while
building the drawing model, so the images and the per-page references reach the emitter together with the
pages.
