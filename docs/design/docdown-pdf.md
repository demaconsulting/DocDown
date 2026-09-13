# DocDown.Pdf System Design

![DocDown.Pdf Structure](DocDownPdfView.svg)

`DocDown.Pdf` is the first real extraction backend for the DocDown output contract. It reads a PDF's
text, embedded images, and document metadata, and writes them through the `IExtractionSink` the
engine supplies. It is a separately distributed NuGet package that a host registers explicitly
alongside `DocDown.Core`.

## Architecture

The system is flat: it has no subsystems, because there is one architectural boundary here — the
PDF — rather than several. Four units divide the work along the lines the output contract itself
draws.

- **PdfDocumentExtractor** is the backend the engine selects and invokes. It declares the identity,
  formats, and capabilities selection ranks on; answers the availability probe; opens the document;
  reports its metadata and environment facts; selects the pages; delegates text and image work; and
  reports the shortfalls only it can explain. It also owns this package's diagnostic codes.
- **PdfTextExtractor** turns a page's positioned glyphs into markdown paragraphs in reading order,
  places the links to that page's images, marks page boundaries, and reports whether any glyph was
  present at all.
- **PdfImageExtractor** writes the embedded images, choosing an encoding per image, stating how each
  one was produced, and accounting by count and by reason for every image it could not deliver.
- **PdfDocDownBuilderExtensions** is the one visible edge from a host to this package: the single
  `AddPdf` call that registers the backend.

The three units that touch PDF structure are the only places a PdfPig type appears. The registration
seam is deliberately parser-free, so a host can reference the surface it configures without the
parser's types entering its own compilation.

### The division of honesty between Core and this package

The engine already knows some things this backend does not — that images were suppressed by an
option, that a requested capability is unavailable across all registered backends, that an artifact
is partial or absent and must therefore be explained. This package supplies the complementary half:
the facts only a PDF reader knows. Which encoding an image used and whether it could be decoded;
whether the pages carried any glyphs; why this particular backend cannot rasterize a page. Where the
two overlap — a page-rendering request produces a gap from each — the gaps are complementary rather
than redundant: the engine's states the capability accounting, this package's states the
backend-specific reason and where the capability lives.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPdf` is the whole surface |
| Source document | Inbound | PDF byte stream | Not guaranteed seekable |

The constraints each interface carries, stated in full:

- **`IDocumentExtractor`** is implemented by `PdfDocumentExtractor`. `ProbeAvailability` must be well
  under 50 ms, side-effect free, must not open the document, and must not throw.
- **`ISelfValidating`** enumeration must be cheap; the work happens only when a case's delegate is
  invoked, not when the cases are listed.
- **`IExtractionSink`** is the only output channel. Every byte and every report goes through it, and
  no filesystem path is ever constructed in this package.
- **`DocDownBuilder`** is extended by exactly one method, `AddPdf`; there is no other way to register
  this backend.
- **The source document** is obtained through `DocumentSource.OpenRead` and is not guaranteed
  seekable for a stream source, so it is buffered before parsing.

No PDF-parser type appears on any of these. That containment is machine-enforced by a reflection
test over the package's exported types.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout. See the
  *DocDown.Core System Design*.
- **PdfPig** (OTS) — the managed PDF parser. Confined to three source files and absent from the
  public API. See *PdfPig* under the OTS integration design for the features used, the version pin,
  and the framework-resolution constraint.

There are no other runtime dependencies, and no dependency of any kind on a native library.

## Risk Control Measures

- **Parser containment.** PdfPig types appear in exactly three files and in no public signature. A
  reflection test fails the build if any reaches the exported surface, so the parser stays
  replaceable and its pre-1.0 API churn cannot become a consumer's breaking change.
- **Provenance segregation.** The decision about what an image *is* is taken in one place
  (`PdfImageExtractor`), and the claim recorded about it travels with the bytes in the same call.
  There is no path by which bytes and their provenance label can be chosen independently and
  disagree.
- **Failure containment.** Parser faults are not translated here. They propagate to the engine, which
  converts them into a coded, structured failure that still writes the full output layout — so a
  protected or corrupt document in a batch run cannot abort the run or produce a partial layout.
- **Count-before-decide accounting.** Every image is counted as found before any decision is taken
  about it, so the ledger's denominator cannot be reduced by the same code path that failed to
  deliver an image.

## Data Flow

1. The engine selects this backend for a document detected as PDF and calls `ExtractAsync` with a
   context exposing the options, the sink, and a cancellation token — but no filesystem path.
2. `PdfDocumentExtractor` records the parser and the absence of page rendering as environment facts,
   then buffers the source and opens it. A parser fault from this point propagates to the engine.
3. It selects the pages, honoring any requested range, and reports the document's title, author, and
   page count.
4. `PdfImageExtractor` walks the selected pages. For each image it counts it as found, applies the
   caller's size limits, chooses an encoding, writes the bytes through the sink with an explicit
   provenance label, and receives back the relative path to link it by. It then reports the found
   count and any decode-failure, size-skip, or unhonored-output-mode gaps.
5. `PdfTextExtractor` renders the selected pages to markdown, placing each image's link under the
   page it came from, and reports whether any glyph was present.
6. `PdfDocumentExtractor` writes the markdown through the sink, reports the structural gaps — no
   pages, no text layer, no page rendering — and returns succeeded or degraded.
7. The engine adds its own derived gaps, finalizes the content, reconciles the ledger, and writes
   `summary.txt` and `manifest.json`.

## Design Constraints

- **PdfPig publishes no `net10.0` library asset.** Its highest framework group is `net9.0`, so
  net10.0 consumers of this package resolve the `net9.0` asset through normal framework
  compatibility. This is recorded here as well as in the OTS integration design because it is a
  property a reader of *this* system's constraints needs, not only a property of the dependency.
- **PdfPig is pre-1.0 and changes its public API on minor versions.** The package reference is
  therefore pinned to an exact version range rather than a floating minimum, and the parser's types
  are confined to three files so an upgrade has a bounded review surface.
- **The parser's raw page text must never be used.** It is content-stream paint order with no word or
  paragraph boundaries, which for a multi-column page is not reading order. The structured
  word-grouping, page-segmentation, and reading-order pipeline is used instead.
- **The package is 100% managed, runtime-identifier agnostic, and ships no native asset.** This is
  what makes it deployable anywhere the runtime is, and it is also precisely why the `renderedPages`
  capability is not declared: rasterizing a page needs a renderer, and shipping one would mean
  shipping native assets.
- **`ProbeAvailability` is unconditional.** Nothing about this backend is environment-dependent — no
  native binary to locate, no external application to launch — so the probe's obligations are
  satisfied by construction rather than by careful maintenance.
- **The optional PdfPig JPEG and JPEG 2000 filter packages are deliberately not referenced.** They
  would each be a further OTS item with its own license and its own framework-currency constraint,
  in exchange for decoding two encodings that are rare in practice. Neither is needed to deliver the
  image: both encodings store a complete image file, which is written through unchanged — as `.jpg`
  and as `.jp2` respectively, the latter with a caveat that many viewers and image libraries cannot
  read JPEG 2000. Encodings whose stored bytes are not a file at all are reported as counted,
  reason-bearing gaps instead — which is the behavior the output contract requires regardless of
  which encodings can be decoded.
