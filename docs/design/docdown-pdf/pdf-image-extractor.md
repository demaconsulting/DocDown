## PdfImageExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfImageExtractor` writes a PDF's embedded images through the sink. Its single responsibility is to
choose truthful bytes, media type, and transform metadata for each image and to record short factual
notes when an attempted image step could not complete.

The unit also returns exact `Found` and `Written` counts so the caller can report image inventory
from the same walk that produced the files. Every image is counted before any decision is taken
about it.

### Data Model

`PdfImageExtractor` is an `internal static class`. It holds no mutable shared state; all accounting
lives for the duration of one call.

- **`RawBytesMeaning`** (private `enum`) - classifies what an image stream's raw bytes actually are:
  a complete image file, compressed samples, a containerless bitstream, or uncompressed samples.
- **`FilterClassification`** (private `sealed record`) - one row of the filter table: the raw-byte
  meaning and, when applicable, the media type of a verbatim image file.
- **`EncodedImage`** (private `readonly record struct`) - the bytes, media type, and transform chosen
  for one written image.
- **`ImageAccounting`** (private `sealed class`) - running counts and bounded ordered item lists for
  undecodable images, size-limited skips, and images that defeated a PNG request.
- **`PdfExtractedImage`** (`internal sealed record`) - one written image: its source page, relative
  path, and stable reference.
- **`PdfImageResult`** (`internal sealed record`) - the written images together with the `Found` and
  `Written` counts. `PdfImageResult.Empty` represents the case where image extraction was disabled
  and this unit attempted nothing.

### Key Methods

- **`static ValueTask<PdfImageResult> ExtractAsync(IReadOnlyList<Page> pages, IExtractionSink sink,
  ExtractionOptions options, CancellationToken)`** - returns `PdfImageResult.Empty` immediately when
  embedded-image extraction is disabled because Core records that deliberate suppression. Otherwise,
  it walks the selected pages in document order, counts every image found, writes each deliverable
  image through the sink, emits plain notes for incomplete attempts, and returns the written-image
  list with exact counts. Preconditions: all arguments non-null.
- **`WriteImageAsync`** (private) - applies dimension and byte limits, classifies the image filter,
  chooses the encoding, writes the image through the sink, and records a note condition when needed.
- **`Classify`** (private) - looks up the filter-handling table. Unlisted filters are treated
  conservatively as sample data rather than as a verbatim file.
- **`Encode`** (private) - passes through only those rows whose raw bytes are already a complete
  image file and otherwise decodes to PNG or returns no image when decoding is not possible.
- **`FilterNameOf`** (private) - reads the effective PDF filter name from the image dictionary.
- **`DescribeImage`** (private) - builds a stable reference such as `page 2 image 3`.
- **`ReportAccountingNotes`** and helpers (private) - emit one-sentence notes for undecodable
  encodings, size-limit skips, and unhonored `ForcePng` requests.

### Filter handling

| PDF filter | Raw stored bytes | Handling |
| ---------- | ---------------- | -------- |
| `DCTDecode` | complete JPEG file | write unchanged as `.jpg`; report `image/jpeg`, `Passthrough` |
| `JPXDecode` | complete JPEG 2000 codestream | write unchanged as `.jp2`; report `image/jp2`, `Passthrough` |
| `FlateDecode`, `LZWDecode`, `RunLengthDecode` | compressed pixel samples | decode and re-encode as PNG |
| `CCITTFaxDecode`, `JBIG2Decode` | containerless bitstream | decode to PNG if possible; otherwise note the encoding |
| no filter declared | uncompressed pixel samples | encode as PNG |
| unlisted filter | treated conservatively as samples | decode to PNG if possible; otherwise note the encoding |

Only `DCTDecode` and `JPXDecode` are passed through unchanged because those bytes are already image
files. `JPXDecode` carries no extra note when written successfully because the extraction step did
complete; notes are reserved for incomplete attempts.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. No image condition raises
an exception after the walk starts. An image that cannot be decoded, exceeds a caller-supplied size
limit, or cannot honor a requested PNG output mode is recorded as a note and the walk continues.
Cancellation is observed per page and propagates.

### Dependencies

- **PdfPig** (OTS) - `Page.GetImages`, `IPdfImage.RawBytes`, `IPdfImage.TryGetPng`, and the image
  dictionary used to identify the effective filter.
- **DocDown.Core** - `IExtractionSink`, `ImageHint`, `ImageTransform`, `ExtractionOptions`,
  `ExtractionNote`, and `ImageOutputMode`.

### Callers

`PdfDocumentExtractor`, before text rendering places the links for the images written here. See
*PdfDocumentExtractor Design*.
