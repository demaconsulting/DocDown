## WordOpenXmlImageReader

![DemaConsulting.DocDown.Word Structure](WordView.svg)

### Purpose

`WordOpenXmlImageReader` resolves the embedded images of a Word Open XML document to their bytes
and provenance. Its single responsibility is passthrough: an Open XML image part stores a complete
image file byte-for-byte, so every image this unit yields is written unchanged with a
`Passthrough` transform and the part's own media type. It never decodes, never re-encodes, and
never guesses pixel dimensions.

The unit is factored out of the reader because image resolution is scoped to an
`OpenXmlPartContainer` — the main part or a header or footer part — rather than to the document as
a whole. Resolving by relationship id therefore needs the enclosing container as a parameter.

### Data Model

`WordOpenXmlImageReader` is an `internal static class` with no state. All operations are pure or
read-only I/O over an already-open package.

### Key Methods

- **`static WordImageRef? Resolve(OpenXmlPartContainer container, string? embedId, ...candidates)`**
  — resolves a blip's `r:embed` relationship id within its enclosing part container, taking the
  reader's ordered image-text candidates. Returns `null` when the id is null or empty, or when it
  does not resolve to an `ImagePart`, which is a valid outcome for drawings that point to something
  else such as a chart. On success it returns a `WordImageRef` carrying the complete part bytes,
  `imagePart.ContentType` as the media type, the selected preferred name, optional description,
  description source, and `imagePart.Uri.ToString()` as the source-reference URI. It appends the
  part's file name as the final fallback candidate and lets the shared `ImageTextSelector` rank the
  candidates; the alt text is set only for a descriptive selection, and the description and its
  source are set for descriptive and contextual selections but not for the bare file-name fallback.
- **`BuildReference()`** (private) — assembles the `WordImageRef` from the bytes, media type,
  source URI, and the selected text.
- **`ReadPartBytes()`** (private) — reads an image part fully into a byte array through a read-only
  stream.
- **`NameFromUri()`** (private) — derives a fallback name from the part URI when the document
  supplied no descriptive text.

### Error Handling

A null `container` or `candidates` argument is rejected with `ArgumentNullException` at the call site. A
relationship id that does not resolve to an image part is a valid case and yields `null` from
`Resolve()` rather than throwing; the reader simply does not add an image reference for that
drawing. Other faults such as a corrupt or truncated part propagate to Core, which renders them as
unreadable output.

Deduplication of stored files is Core's job, keyed by image content, so the same logo referenced many
times is stored once and every link resolves to it. This unit intentionally yields a reference for each
blip it is asked to resolve, including duplicates, and `WordOpenXmlReader` does not deduplicate them
either; the single stored file for a repeated logo is the result of the sink's content-keyed
deduplication.

### Dependencies

- **DemaConsulting.DocDown.Core** — `ImageTextSelector`, `ImageTextCandidate`, `ImageTextSource`, and
  `ImageTextConfidence`.
- **DocumentFormat.OpenXml** (OTS) — `OpenXmlPartContainer` and `ImagePart`. See the
  *DocumentFormat.OpenXml* OTS design.
- **`WordImageRef`** — the record returned by `Resolve()`.

### Callers

`WordOpenXmlReader.CollectImage()` calls `Resolve()` for every blip in a drawing, including body,
header, footer, comment, and footnote content. Tests call `Resolve()` directly through
`InternalsVisibleTo`.
