# DocDown.Pdf System Design

![DocDown.Pdf Structure](DocDownPdfView.svg)

`DocDown.Pdf` is the managed PDF extraction backend for DocDown. It reads document metadata, page
text, and embedded images from a PDF and writes the extracted facts through `IExtractionSink`. Its
reporting model is intentionally narrow: DocDown reports what it extracted and where. Ordinary
document absences become content-inventory counts, and attempted steps that could not complete
become short factual notes.

## Architecture

The system is flat: it has no subsystems. Four units divide the work.

- **PdfDocumentExtractor** is the backend the engine selects and invokes. It reports parser and
  page-rendering environment facts, buffers the source, opens the document, selects pages, reports
  document metadata, delegates image and text extraction, writes the markdown content, reports the
  content inventory, and returns `ExtractionOutcome.Produced` on normal completion. Parser faults
  propagate to Core, which converts them to `ExtractionOutcome.Unreadable`.
- **PdfTextExtractor** turns positioned glyphs into markdown in reading order, emits page markers,
  places image links under the page they came from, and counts the headings and paragraphs it wrote.
- **PdfImageExtractor** writes embedded images through the sink, chooses whether each image is
  passed through or decoded to PNG, counts every image found before any decision is taken, and
  emits plain notes for undecodable images, size-limit skips, and PNG requests it could not honor.
- **PdfDocDownBuilderExtensions** is the registration seam. The single `AddPdf` call adds this
  backend to a `DocDownBuilder`.

### Reporting model

This package uses only two reporting mechanisms beyond the extracted artifacts themselves:

- **Content inventory** for counts the extractor knows exactly from the structures it walked,
  including zero when it genuinely looked for something and found none.
- **Extraction notes** for facts about an attempted extraction step that could not complete.

The package uses no third reporting channel beyond inventory and notes. A note states a fact about
the extraction step; it does not judge the document.

## External Interfaces

- **`IDocumentExtractor`** - inbound from the engine as a .NET interface. The probe stays cheap,
  side-effect free, and non-throwing.
- **`ISelfValidating`** - inbound from the engine as a .NET interface. Case enumeration is cheap,
  and work happens only when a case runs.
- **`IExtractionSink`** - outbound to Core as a .NET interface. It is the only output channel for
  content, images, metadata, and notes.
- **`DocDownBuilder`** - inbound from a host as a .NET extension surface. `AddPdf` is the only
  registration entry point.
- **Source document** - inbound as a PDF byte stream. A stream source is not guaranteed seekable.

Additional interface constraints:

- `ProbeAvailability` is unconditional because this backend is managed-only and environment
  independent.
- `IExtractionSink` owns naming, deduplication, and file layout. This package never constructs
  output paths.
- A stream-backed `DocumentSource` is buffered before parsing because the parser must seek within
  the PDF.

## Dependencies

- **DocDown.Core** - the extraction contract, sink interfaces, options, metadata model, note model,
  and output layout. See the *DocDown.Core System Design*.
- **PdfPig** (OTS) - managed PDF parsing, page access, marked-content inspection, layout analysis,
  and embedded-image access. See *PdfPig* in the OTS integration design.

The package has no runtime dependency on a native renderer. Page rasterization lives in the
separate `DocDown.Pdf.Rendering` package.

## Risk Control Measures

- **Parser containment.** PdfPig types appear only in the units that read PDF structures and do not
  reach the public API surface.
- **Provenance segregation.** `PdfImageExtractor` chooses the bytes, media type, and transform hint
  together so the manifest cannot describe an image differently from how it was produced.
- **Failure containment.** Encrypted and malformed PDFs propagate as parser faults for Core to
  convert into unreadable outcomes with the standard output layout still written.
- **Count-from-source reporting.** Content inventory counts come from the same extraction walk that
  produced the markdown and images, so zero counts describe what was actually looked for rather than
  what a later heuristic inferred.

## Data Flow

1. The engine selects this backend for a PDF and calls `ExtractAsync` with the source, options,
   sink, and cancellation token.
2. `PdfDocumentExtractor` records the managed parser and the fact that page rendering is not
   provided by this extractor, then buffers the source and opens the document.
3. It selects the requested pages, reports document information and PDF document metadata, and asks
   `PdfImageExtractor` to walk the selected pages for embedded images.
4. `PdfImageExtractor` counts every image found, writes each image it can through the sink, and
   emits a plain note when an attempted image step could not complete.
5. `PdfTextExtractor` renders the selected pages to markdown, emits page markers, places image
   links, and returns the heading and paragraph counts from the same rendering pass.
6. `PdfDocumentExtractor` writes the markdown, reports the content inventory, and returns
   `ExtractionOutcome.Produced`.
7. If the parser faults while opening or reading the document, Core converts that fault into
   `ExtractionOutcome.Unreadable` and writes the standard summary and manifest layout.

## Design Constraints

- **Managed-only availability.** `ProbeAvailability()` always returns available because there is no
  native binary, external application, or environment probe to perform.
- **No page renderer in this package.** The managed PDF backend does not rasterize pages. A caller
  requesting rendered pages receives no rendered-page files from this package, and the system record
  states that plainly.
- **Reading order over paint order.** PdfPig's raw page text is not an acceptable output because it
  follows content-stream paint order rather than human reading order.
- **Truthful image naming.** The extension and media type always describe the bytes written. For
  `DCTDecode` and `JPXDecode`, the stored bytes are already image files and are written through
  unchanged. Other encodings are decoded to PNG or, if that cannot be done, reported with a note.
- **Notes are reserved for incomplete steps.** A note is emitted only when DocDown attempted a step
  and could not complete it. An image written successfully as `.jp2` is counted as extracted and
  carries no extra note.
