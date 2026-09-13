## PdfPig

### Purpose

PdfPig is the managed PDF parser `DocDown.Pdf` is built on. It was chosen because it is 100% managed
with no native assets and no transitive dependencies on the frameworks this repository targets, which
is the single property that lets the PDF package be deployed anywhere the .NET runtime is, without a
per-platform build and without a native binary to locate at run time. It is Apache-2.0 licensed,
compatible with this repository's MIT license.

It provides document parsing, positioned glyph access with the layout-analysis primitives needed to
recover reading order, embedded-image enumeration with access to both the stored bytes and the
encoding, document information, and a document writer used to generate test fixtures.

### Features Used

- `PdfDocument.Open` with `ParsingOptions`, and `PdfDocument.Dispose`
- `PdfDocument.NumberOfPages`, `GetPage`, `GetPages`, and `Information` (title and author)
- `Page.Number`, `Page.Letters`, `Page.GetMarkedContents`, and `Page.GetImages`
- `NearestNeighbourWordExtractor`, `DocstrumBoundingBoxes`, and
  `UnsupervisedReadingOrderDetector` for word grouping, page segmentation, and reading order
- `ContentOrderTextExtractor` as the fallback when segmentation yields no blocks
- `IPdfImage.RawBytes`, `IPdfImage.TryGetPng`, `IPdfImage.WidthInSamples`,
  `IPdfImage.HeightInSamples`, and `IPdfImage.ImageDictionary` for the filter name
- `PdfDocumentBuilder` and `PdfPageBuilder` — used only to generate test fixtures and the
  self-validation probe document, never on an extraction path

`Page.Text` is deliberately **not** used. It is content-stream paint order with no word or paragraph
boundaries, and PdfPig's own documentation warns against it; the structured pipeline above is used
instead.

The optional `PdfPig.Filters.Dct.JpegLibrary` and `PdfPig.Filters.Jpx.OpenJpeg` add-on packages are
deliberately **not** referenced. Each would be a further OTS software item with its own license,
maintainer, and framework-currency constraint, and each would need its own full artifact set, in
exchange for decoding two encodings that are rare in practice. Neither omission causes a silent loss:
a JPEG XObject's stored bytes are already a JPEG file and are written through unchanged, and a JPEG
2000 XObject's stored bytes are already a JPEG 2000 file and are written through unchanged as `.jp2`
with a gap warning that many viewers and image libraries cannot read the format. Encodings whose
stored bytes are not a file at all — JBIG2 in particular — are reported as counted, reason-bearing
gaps instead, which is the behavior the output contract requires regardless of which encodings can be
decoded, and which the `DocDown.Pdf` tests exercise directly.

### Integration Pattern

PdfPig is referenced as a real runtime dependency of the `DocDown.Pdf` package and flows to
consumers, unlike every other OTS item in this repository, which are build-time or
quality-pipeline tools. Its usage is a stateless open-read-dispose sequence per extraction: a
document is opened from a buffered byte array, read, and disposed within a single `ExtractAsync`
call. There is no global initialization, no configuration object retained between calls, and no
process-level state.

**Version pinning.** The package reference is pinned to an exact version range rather than a floating
minimum, because PdfPig is pre-1.0 and its own documentation states that the public API may change on
a minor version. A floating reference would let a restore silently substitute an incompatible API.

**Framework resolution.** PdfPig publishes library assets for `net462`, `net471`, `net6.0`, `net8.0`,
`net9.0`, `netstandard2.0`, and `netstandard2.1`. There is **no `net10.0` asset**: net10.0 consumers
of `DocDown.Pdf` resolve PdfPig's `net9.0` asset through normal framework compatibility. This is
recorded here and again in the `DocDown.Pdf` system design's Design Constraints, because it is a fact
a reader of either document needs.

**Containment as a risk control.** PdfPig types appear in exactly three source files —
`PdfDocumentExtractor.cs`, `PdfTextExtractor.cs`, and `PdfImageExtractor.cs` — and in no public
signature of the package. A reflection test over the package's exported types fails the build if any
PdfPig type reaches the public surface. This bounds the review surface of a parser upgrade and keeps
the parser's pre-1.0 API churn from becoming a breaking change for consumers of `DocDown.Pdf`.

**Native assets.** PdfPig's package contains no `runtimes/` folder and no unmanaged binary. The
absence is asserted by a test over the `DocDown.Pdf` build output rather than assumed from the
dependency's documentation.
