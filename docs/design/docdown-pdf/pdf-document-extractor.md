## PdfDocumentExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfDocumentExtractor` orchestrates one PDF extraction. It reports the managed parser context,
buffers and opens the document, selects the pages to read, reports document information and PDF
document metadata, delegates image and text work, writes content-inventory counts, and returns
`ExtractionOutcome.Produced` when the extraction completes normally.

It does not judge the document. Ordinary absences, such as a scanned page with no glyphs or a PDF
with no pages, are reported as factual counts rather than as failure-oriented machinery.

### Data Model

`PdfDocumentExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance can safely serve
concurrent extractions.

- **`Id`** (`string`) - `pdf`, the stable backend key used by selection and reporting.
- **`DisplayName`** (`string`) - `PDF (PdfPig)`, shown in summaries and backend listings.
- **`SupportedFormats`** - the PDF format only.
- **`Priority`** (`int`) - `0`, the neutral rank for this backend.
- **`InterestingPdfFields`** (`string[]`, private static readonly) - the document-information fields
  mapped into `metadata.json` when present or recorded as absent when blank.

The instance is immutable after construction. All run-specific facts live in local variables inside
`ExtractAsync`.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** - returns
  `ExtractorAvailability.Available()` unconditionally. Precondition: none. Postcondition: performs
  no I/O, does not open the document, does not throw, and reports no rendered-page support.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`**
  - reports environment facts, buffers the source, opens the PDF, selects the requested pages,
  reports document information and document metadata, delegates image extraction and then text
  rendering, writes the markdown, reports the content inventory, and returns
  `ExtractionOutcome.Produced`. Preconditions: both arguments non-null. Postcondition: every output
  artifact is routed through the sink.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** - returns two cheap-to-enumerate cases: a
  parse round trip that builds and rereads a one-page PDF in memory, and a page-rendering case that
  reports a skipped result with an explanation.
- **`ReadSourceAsync`** (private) - buffers the source into memory so stream-backed and file-backed
  sources behave the same way to the parser.
- **`SelectPages`** (private) - filters the document's pages by the requested inclusive page range.
  A range wider than the document yields the pages that exist.
- **`ReportDocumentInfo`** (private) - records title, author, total page count, and selected part
  count. Blank metadata values are normalized to absent rather than reported as empty strings.
- **`ReportContentFeatures`** (private) - records the `pages`, `headings`, `paragraphs`, and, when
  honestly known, `inline images` content features from the same extraction pass that produced the
  markdown and images.

### Error Handling

Null `source` or `context` arguments are rejected with `ArgumentNullException` as caller errors.
Parser faults, including encrypted and malformed documents, are not translated here; they propagate
for Core to convert into `ExtractionOutcome.Unreadable` with the standard layout still written.
`OperationCanceledException` also propagates, as the contract requires.

The self-test cases are the exception to the throw-through rule. Each case converts faults into a
`SelfTestResult` because a self-test is a health signal, not an extraction call.

### Dependencies

- **PdfImageExtractor** and **PdfTextExtractor** - the delegated unit work.
- **PdfPig** (OTS) - document opening, page access, and the PDF document-information dictionary.
- **DocDown.Core** - `IDocumentExtractor`, `ISelfValidating`, `IExtractionContext`,
  `IExtractionSink`, `DocumentInfo`, `DocumentMetadata`, `ContentFeature`, and `PageRange`.

### Callers

The DocDown engine resolves and invokes this unit after selection. `PdfDocDownBuilderExtensions`
registers it with a `DocDownBuilder`. See *PdfDocDownBuilderExtensions Design*.
