## PdfImageExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfImageExtractor` writes a PDF's embedded images through the sink. Its single responsibility is the
image decision and its accounting: which bytes to write for each image, what to truthfully call them,
and how to account — by count and by reason — for every image it could not deliver.

The unit exists because those two things must not be separable. Choosing bytes without recording how
they were produced is what makes a manifest's provenance field decorative; recording a provenance
that the byte-choosing code did not actually take is what makes it false. Here the two decisions are
one expression, and the same hint carries both to the sink.

The governing constraint, from which everything below follows: **the extension always describes the
bytes on disk, the transform always describes what was done to produce them, and any image that could
not be extracted is counted and explained.** A short image set with no explanation is a defect, not a
tidy result.

### Data Model

`PdfImageExtractor` is an `internal static class`. It holds no mutable state between calls; the
running accounting lives for the duration of a single call, so it is thread-safe.

- **`RawBytesMeaning`** (private `enum`) — what a filter's raw stored bytes *are*, independent of how
  they are handled: a complete image file, a complete image file in a format this extractor cannot
  decode, compressed samples, a containerless bitstream, or uncompressed samples.
- **`FilterClassification`** (private `sealed record`) — one row of the filter table: the meaning and,
  when the bytes are a file, its media type. A null media type is the single fact that answers "may
  these bytes be written verbatim?", so the question never has to be re-derived.
- **`EncodedImage`** (private `readonly record struct`) — the `Bytes`, `MediaType`, and `Transform`
  chosen for one image. The three travel together because they must agree; a mismatch between them is
  precisely the dishonesty this unit exists to avoid.
- **`ImageAccounting`** (private `sealed class`) — the running tally: images `Found`, the counts and
  bounded, ordered reference lists for undecodable images, images written with a readability caveat,
  size-skipped images, and images written in a non-PNG encoding under a PNG request. Each of the three
  encoding-bearing tallies is grouped by PDF filter name in a sorted map, which is what makes the gap
  text byte-deterministic.
- **`PdfExtractedImage`** (`internal sealed record`) — one written image: its source page, the
  relative path the sink allocated, and the stable reference used as link text.
- **`PdfImageResult`** (`internal sealed record`) — the written images plus the `Found` and `Written`
  counts, so the caller can describe the extraction without recounting.

### Key Methods

- **`static ValueTask<PdfImageResult> ExtractAsync(IReadOnlyList<Page> pages, IExtractionSink sink,
  ExtractionOptions options, CancellationToken)`** — returns immediately with an empty result when
  embedded images are disabled, because the caller asked for none to be *attempted* and Core already
  owns the record of that deliberate absence. Otherwise walks the selected pages in order, counting
  every image as found **before** any decision is taken about it, then reports the found count
  unconditionally — including zero, so the ledger can state "0 of 0" honestly — and reports the
  accounting gaps. Preconditions: all arguments non-null.
- **`WriteImageAsync`** (private) — applies the caller's dimension limit, classifies the filter,
  chooses the encoding, applies the byte limit, hands the bytes to the sink with the full hint, and
  records a readability caveat and an unhonored PNG request where each applies. A `string.Empty`
  return from the sink means the image was not stored, and no link is emitted for it.
- **`Classify`** (private) — the table lookup below. An unlisted filter is assumed to hold samples,
  because that is both the common case and the safe direction to be wrong in.
- **`Encode`** (private) — writes the raw bytes verbatim exactly when the classification carries a
  media type, and otherwise decodes to PNG or refuses.
- **`FilterNameOf`** (private) — reads the effective PDF filter name from the image dictionary. The
  last entry of a filter array is the image encoding; earlier entries are transport compressions the
  parser has already undone. Both the full `Filter` key and its inline `F` abbreviation are consulted
  so inline images are named as precisely as XObjects.
- **`DescribeImage`** (private) — builds the stable reference `page N image M`. The parser does not
  surface an XObject's resource name, so the page and the discovery ordinal are used instead: they
  are stable for a given document and page range, which is what keeps gap text and manifest
  provenance byte-deterministic across runs.
- **`ReportAccountingGaps`** and its four helpers (private) — emit the decode-failure,
  readability-caveat, size-skip, and unhonored-output-mode gaps.

### What the raw bytes are, per filter

This classification is stated explicitly, as a table in the code (`FilterTable`) rather than as a
shape a reader has to reconstruct from branches, because the distinction it encodes is easy to get
wrong and expensive to get wrong.

| PDF filter | The raw stored bytes are | Handling, media type, and transform |
| ---------- | ------------------------ | ----------------------------------- |
| `DCTDecode` | a complete JPEG stream | written verbatim to `.jpg` as `image/jpeg`, `Passthrough` |
| `JPXDecode` | a JPEG 2000 codestream | written verbatim to `.jp2` as `image/jp2`, `Passthrough`, **caveated** |
| `FlateDecode` | zlib over **raw pixel samples** | decoded and re-encoded as `image/png`, `DecodedToPng` |
| `LZWDecode`, `RunLengthDecode` | compressed raw samples | as `FlateDecode` |
| `CCITTFaxDecode` | a bare fax bitstream, no container | decoded to PNG if possible; otherwise a counted gap |
| `JBIG2Decode` | an embedded JBIG2 segment, no container | as `CCITTFaxDecode` |
| none declared | uncompressed samples | encoded as `image/png`, `DecodedToPng` |
| anything else | assumed to be samples | as `CCITTFaxDecode` |

> **Standing warning — do not turn the lower rows into a passthrough.** For every filter below the
> first two, **the raw stored bytes are compressed pixel data, not a file format**. There is no file
> extension that makes them viewable, because interpreting them requires the width, height, color
> space, and bits-per-component that live in the image dictionary and not in the stream. "Can we just
> dump `RawBytes` to a file with the right extension?" is answered **yes only for the first two rows**.
> Applying it anywhere else emits files that open in nothing, under names that claim otherwise — which
> is precisely the silent dishonesty this unit exists to prevent. The only honest routes for those
> rows are a genuine decode-and-re-encode to PNG, or a counted gap saying the image was not delivered.

Why each of the first two rows is different, and different from each other:

- **`DCTDecode`** — the stored stream is already a complete JPEG file. Decoding and re-encoding it
  would cost quality for no gain, and would make the passthrough claim untrue. The written file is
  byte-identical to the embedded stream, and the test suite compares it against the fixture's retained
  source bytes rather than against the extractor's own output, so the claim is falsifiable.
- **`JPXDecode`** — the stored stream is a JPEG 2000 codestream: a real image file, in a format this
  library takes no decoder for. Writing it as `.jp2` is truthful about both the bytes and the name,
  and a file a JPEG 2000-capable consumer can open is worth more to a multimodal consumer than an
  absence. It is **not** treated as a loss, because it is in the output; it is instead reported with a
  caveat stating that many viewers and image libraries cannot read the format. Passing it through
  without that caveat would imply a readability the format does not have, which would be the same
  dishonesty in the opposite direction.

### The gap accounting rules

Four conditions are reported separately, never merged, because they have different causes and
different remedies:

1. **Undecodable encodings** — `PDF0001` plus a gap targeting `images/` whose reason gives the count
   as "n of m" and names each encoding with its own count, sorted by encoding name so the text is
   byte-deterministic. Scope is `PartiallyExtracted` when at least one image was written and
   `Unavailable` when none was. That choice is not cosmetic: calling an empty folder "partially
   extracted" would both overstate the result and contradict the contract verifier's rule that a
   partial folder holds at least one file and an absent one holds none. **Only images that were not
   written are counted here**, so the number always means exactly "images lost".
2. **Limited readability** — `PDF0004` plus a gap naming the count, the encoding, and the consequence:
   the codestream was written unchanged with a `.jp2` extension, and many image viewers and image
   libraries cannot read JPEG 2000 files. The remedy names the conversion a caller can perform. The
   scope is always `PartiallyExtracted`, which is sound because the gap is only reported when at least
   one such file exists on disk. These images count toward the `obtained` side of the ledger, so
   "n of m images extracted" keeps meaning what it says.
3. **Size and dimension skips** — their own gap with its own reason and a remedy naming the options
   that caused it. A skip the caller configured is undone by relaxing an option; a decode failure is
   a property of the document. Reporting them as one condition would offer a remedy that cannot work
   or hide one that would.
4. **PNG output that could not be honored** — `PDF0002` plus a gap naming the count and the encodings
   responsible, taken from what was actually written rather than assumed to be JPEG: a JPEG 2000
   passthrough defeats a `ForcePng` request in exactly the same way, and naming the wrong encoding
   would be its own small dishonesty. Core's naming rule already guarantees the file on disk is not
   mislabeled, because the extension follows the bytes actually written and never a requested format.
   What this gap adds is the *explanation*: without it a caller who asked for PNG and received JPEG
   would have to infer why, and might conclude the option was ignored rather than unachievable. The
   consequence — the run reports degraded and incomplete — is accepted and correct, because a
   requested option genuinely was not fully honored.

Every gap targets the path `images/` exactly, because a gap whose target does not match the ledger
entry is reported by the contract verifier as an unexplained absence.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. No image condition raises
an exception: an image that cannot be decoded, is too large, or is declined by the sink is recorded
and the walk continues, because one unreadable image must not cost a caller the rest of the document.
Affected-item lists are bounded so a pathological document cannot produce an unreadable gap; the
counts remain exact even where the item list is truncated. Cancellation is observed per page and
propagates.

### Dependencies

- **PdfPig** (OTS) — `Page.GetImages`, `IPdfImage.RawBytes`, `IPdfImage.TryGetPng`, and the image
  dictionary from which the filter name is read.
- **DocDown.Core** — `IExtractionSink`, `ImageHint`, `ImageTransform`, `ExtractionOptions`,
  `ExtractionGap`, `ExtractionDiagnostic`, `GapKind`, `GapScope`, `ImageOutputMode`.

### Callers

`PdfDocumentExtractor`, before the text rendering that places this unit's links. See
*PdfDocumentExtractor Design*.
