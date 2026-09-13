## WordOpenXmlImageReader

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordOpenXmlImageReader` resolves the embedded images of a Word Open XML document to their bytes
and provenance. Its single responsibility is passthrough: an Open XML image part stores a complete
image file byte-for-byte, so every image this unit yields is written unchanged with a
`Passthrough` provenance and honest bytes-versus-name agreement. It never decodes, never
re-encodes, and never guesses pixel dimensions.

The unit is factored out of the reader because image resolution is scoped to an
`OpenXmlPartContainer` — the main part *or* a header or footer part — rather than to the document
as a whole. A header references its own image parts through its own relationships, not the main
part's, so resolving by relationship id needs the enclosing container as a parameter. Making that
a static entry point keeps the reader's per-drawing collection loop small and keeps the resolution
rule stated in one place.

### Data Model

`WordOpenXmlImageReader` is an `internal static class` with no state. All operations are pure or
read-only I/O over an already-open package.

### Key Methods

- **`static WordImageRef? Resolve(OpenXmlPartContainer container, string? embedId, ...candidates)`** —
  resolves a blip's `r:embed` relationship id within its enclosing part container, taking the reader's
  ordered image-text `candidates` (an `IReadOnlyList<ImageTextCandidate>`). Returns `null`
  when the id is null or empty, or when it does not resolve to an `ImagePart` (a valid outcome
  when the drawing points to something that is not a raster image, for example a chart). On
  success returns a `WordImageRef` carrying the complete part bytes, `imagePart.ContentType` as
  the media type, and `imagePart.Uri.ToString()` as the source-reference URI Core keys
  deduplication on. It appends the media-part name as the final `Uri` fallback candidate to the
  `candidates` the reader gathered, runs the shared `ImageTextSelector`, and turns the result into
  the reference's naming hint (any usable source), its alt text (a descriptive source only), and its
  manifest description with provenance (a descriptive or contextual source, never the bare media
  name). Precondition: `container` and `candidates` non-null. Postcondition: read-only I/O; the
  container's package remains open.

  The pixel dimensions on the resulting reference are unavailable — reported as `null` by the
  emitter's `HintFor` — because the Open XML drawing surface exposes `wp:extent`, and `wp:extent`
  is a display size in EMUs, not a pixel count. Reporting EMU as pixels would be a false
  provenance claim.
- **`static IReadOnlyList<(byte[] Bytes, ImageHint Hint)> Read(WordprocessingDocument document)`** —
  enumerates every `ImagePart` in the main part directly, so the result reflects what the package
  stores regardless of how the body references it. Each entry pairs the complete bytes with the
  `ImageHint` the emitter's `HintFor` builds, always claiming `Transform = Passthrough` and null
  pixel dimensions. Precondition: `document` non-null. Postcondition: read-only I/O.
- **`ReadPartBytes`** (private) — reads an image part fully into a byte array through
  `GetStream(FileMode.Open, FileAccess.Read)` and a `MemoryStream` copy. Read-only I/O.
- **`NameFromUri`** (private) — takes the substring after the final `/` of the part URI, or the
  whole URI when there is no slash, and returns `null` for a blank result. Pure.

### Error Handling

A null `container` or `document` is rejected with `ArgumentNullException` at the point of the
call. A relationship id that does not resolve to an image part is a valid case — the drawing may
point to a chart or a media reference — and yields `null` from `Resolve` rather than throwing;
the reader simply does not add a `WordImageRef` for that drawing. Every other fault (a corrupt
part, a truncated stream) propagates to Core, which wraps it as an `ExtractorFailed` failure with
the full layout still written.

Deduplication is Core's job, keyed by SHA-256, so the same logo referenced many times is stored
once and every link resolves to it. This unit intentionally yields duplicates: filtering here
would hide the fact that a duplicate reference exists and complicate the caller's provenance
accounting.

### Dependencies

- **DocDown.Core** — `ImageHint`, `ImageTransform`.
- **DocumentFormat.OpenXml** (OTS) — `WordprocessingDocument`, `OpenXmlPartContainer`, `ImagePart`,
  `MainDocumentPart`. See the *DocumentFormat.OpenXml* OTS design.
- **`WordImageRef`** — the record returned by `Resolve`.
- **`WordContentEmitter.HintFor`** — used by `Read` to build the paired hint from a reference.

### Callers

`WordOpenXmlReader.CollectImage` calls `Resolve` for every blip in a drawing (in the body, in
headers and footers, and inside comment and footnote content). That is the unit's only entry point:
there is no parallel enumeration of the package's image parts, because a second path maintained
only for tests would be free to disagree with the one a real extraction takes.
