### FormatSniffer

![Detection Structure](DetectionView.svg)

#### Purpose

`FormatSniffer` identifies a document's format from its file-name extension, using the leading
content bytes only as a fallback, and reports the evidence and confidence behind the identification.
Its single responsibility is format *detection* — it neither reads document content for extraction
nor writes any output, and it never opens, unpacks, or parses the document.

#### Data Model

`FormatSniffer` is a `static class` holding one constant and two static tables. It is static because
detection is a pure function of a stream and a name: no collaborator and no state remain, so there is
nothing for an instance to carry.

- **`SignatureBytes`** (`const int`, 512) — The maximum number of leading bytes inspected for a
  content signature. Ample for the `%PDF-` header and for a leading HTML doctype while bounding the
  read cost.
- **`ExtensionMap`** (`static (string, DocumentFormat)[]`) — The ordered file-extension-to-format
  table; the primary detection signal.
- **`PdfSignature`** (`static byte[]`) — The `%PDF-` ASCII signature.

The extension table maps `.pdf`, `.docx`/`.doc`, `.xlsx`/`.xls`, `.pptx`/`.ppt`, `.vsdx`/`.vsd`,
`.html`/`.htm`, and `.txt`/`.text`/`.log` to their formats — the `.doc`, `.xls`, `.ppt`, and `.vsd`
entries are the legacy binary Office formats, detectable but not extractable. Recognizing an
extension here does not imply an extractor is registered for it — that is `ExtractorSelector`'s
concern, and it reports the absence honestly by name.

#### Key Methods

- **`static FormatDetection Detect(Stream content, string? fileName)`** — the detection algorithm.
  - *Preconditions*: `content` non-null, readable, and seekable.
  - *Postcondition*: the stream's original position is restored (in a `finally` block), so detection
    is non-destructive.
  - *Algorithm*, in order: (1) guard null, non-readable, and non-seekable input; (2) look up the
    invariant-lowercase extension of `fileName` in `ExtensionMap` — a match returns immediately as
    matched format / `Extension` / `0.9`, without reading a single byte; (3) otherwise record the
    entry position and read up to `SignatureBytes` leading bytes; (4) `%PDF-` → `Pdf` /
    `ContentSignature` / `1.0`; (5) a BOM-and-whitespace-tolerant `<!DOCTYPE html` or `<html`
    (case-insensitive) → `Html` / `ContentSignature` / `0.9`; (6) otherwise `Unknown` / `Extension` /
    `0.0`.

The extension short-circuit at step 2 is both the trust decision and an optimization: the common case
performs no I/O at all.

Private helpers isolate the read-with-short-read-tolerance (`ReadUpTo`), the prefix comparison
(`StartsWith`), the HTML heuristic (`LooksLikeHtml`), BOM detection (`DetectEncoding`, handling UTF-8
and UTF-16 LE/BE), and the extension lookup (`DetectByExtension`). All are pure apart from the stream
reads.

#### Error Handling

`Detect` throws `ArgumentNullException` when `content` is null and `ArgumentException` when the stream
is not readable or not seekable — these are caller programming errors detected at entry, and both are
checked before the extension short-circuit so the guard contract holds regardless of the file name.
Beyond those guards the method never throws for malformed content: unrecognized bytes return an
`Unknown` detection. The stream position is restored in a `finally` block whenever bytes were read, so
a partially read or rewound stream is never left behind.

A document whose extension does not match its bytes is *not* an error here. The named format is
reported, and the decoder that receives the document is what rejects the mismatch — honestly, with a
reason — which is a better place to discover it than a heuristic in Detection.

#### Dependencies

- **DocumentFormat**, **DetectionBasis**, **FormatDetection** (supporting types; see
  *Detection Subsystem Design*) — the format descriptors and the returned result.
- The .NET Base Class Library (`System.IO`, `System.Text`) — no runtime NuGet dependencies, and no
  archive, compression, or XML facility.

#### Callers

`DocDownEngine` calls `Detect` during the detection step of the pipeline (see *DocDownEngine Design*).
`FormatSniffer` is also part of the public API and may be called directly by a consumer that only needs
format detection.
