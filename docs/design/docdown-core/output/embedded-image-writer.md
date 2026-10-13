### EmbeddedImageWriter

![Output Structure](OutputView.svg)

#### Purpose

`EmbeddedImageWriter` is the shared sink-write pipeline of the Open XML backends (PowerPoint, Excel, and
Visio). Each backend resolves the images of a document into `EmbeddedImage` values; this unit writes them
through the extraction sink once, applies the caller's size limits, and returns an accounting the backend
turns into its own extraction notes. It never reports gaps itself.

#### Data Model

`EmbeddedImageWriter` is a `static class` with no state, so it is safe for concurrent use. It works with
two supporting types declared in the same source file:

- **`EmbeddedImage`** (`record`) — one resolved image: the exact stored bytes, the media type, a preferred
  file-name base, a backend source reference, alt text, a manifest description with its provenance, the
  scalar source page, the sorted page referrers, and the template-referrer flag. `EmbeddedImage.Create`
  builds one from the bytes and the document-supplied `ImageTextCandidate` list: it appends the media name
  as the last-resort candidate, calls `ImageTextSelector.Select`, seeds the file name from any usable
  selection, sets alt text only for a `Descriptive` selection, and records the description and its source
  for `Descriptive` and `Contextual` selections but never for the bare media-name fallback.
- **`EmbeddedImageWriteResult`** (`class`) — the accounting: `Found` (distinct images), `WrittenCount`,
  `PathsBySourceRef`, `SizeSkippedCount`, `SizeSkippedItems` (bounded to 20 references), and
  `VectorWrittenCount`.

`ImageHint` is declared in the same file and documented with the other supporting types in the Output
subsystem design.

#### Key Methods

- **`WriteAsync(IExtractionSink, ExtractionOptions, IReadOnlyList<EmbeddedImage>, CancellationToken)`** —
  processes the images in order. It first merges the page referrers and template flag of byte-identical
  images, then, for each image, computes a SHA-256 content digest. An image whose digest was already seen is
  not counted again; its source reference resolves to the path chosen for the first occurrence. For a new
  image, `ImageDimensions.TryRead` supplies the pixel size when the header can be read, and an image beyond
  `ExtractionOptions.MaxImageDimensionPx` or `MaxImageBytes` is skipped, counted, and listed. Every kept image
  is passed through the sink unchanged as `ImageTransform.Passthrough` with the merged referrers, and the path
  the sink returns is recorded by source reference. A written EMF or WMF is counted in `VectorWrittenCount`.
  The cancellation token is checked between images.
- **`IsVectorMetafile(string)`** — reports whether a media type names an EMF or WMF metafile. SVG is not
  counted because it is broadly renderable.

#### Error Handling

`WriteAsync` rejects a null sink, options, or image list with `ArgumentNullException`, and
`IsVectorMetafile` rejects a null media type. A skipped image is reported through the accounting rather
than an exception, and a sink that returns an empty path (images disabled) simply leaves the image
unwritten.

#### Dependencies

- **`IExtractionSink`** — receives every kept image.
- **`ImageDimensions`** — reads raster pixel size for the dimension limit and the recorded dimensions.
- **`ImageTextSelector`** — ranks the text candidates in `EmbeddedImage.Create`.
- **`ExtractionOptions`** — supplies `MaxImageBytes` and `MaxImageDimensionPx`.
- `System.Security.Cryptography` for the SHA-256 digest.

#### Callers

The PowerPoint, Excel, and Visio content emitters pass the images of their document model to `WriteAsync`,
then turn the returned accounting into their own notes. See _ImageTextSelector Design_ for the naming policy.
