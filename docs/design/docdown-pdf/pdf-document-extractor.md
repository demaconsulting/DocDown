## PdfDocumentExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfDocumentExtractor` is the backend the engine selects and invokes for a PDF. Its single
responsibility is orchestration: it declares what this package can deliver, answers the availability
probe, opens the document, reports its metadata, delegates the text and image work, and reports the
shortfalls that only a PDF reader can explain. It writes no bytes itself and constructs no path.

### Data Model

`PdfDocumentExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance safely serves every
extraction.

- **`Id`** (`string`) — `pdf`; the stable key selection, caller override, and the manifest use.
- **`DisplayName`** (`string`) — `PDF (PdfPig)`; shown in summaries and backend status.
- **`SupportedFormats`** — the PDF format only.
- **`Capabilities`** — `Text | EmbeddedImages | DocumentMetadata`. `RenderedPages` is pointedly
  absent, because this package ships no renderer.
- **`Priority`** (`int`) — `0`; there is no other PDF backend to rank against yet.

`PdfDiagnosticCodes` is an internal constant holder declared alongside the class. It defines the
`PDF0001` (undecodable image encoding), `PDF0002` (PNG output not honored), and `PDF0003` (no
extractable text layer) codes this package emits. The codes live in this file rather than in one of
their own so every source file maps one-to-one to a unit review-set. The `PDF` prefix is deliberate:
Core's `DD` range is internal to Core and already assigned, so a backend emitting a `DD` code would
either collide with Core's meaning or invent a second, conflicting one.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns available with the full declared
  capability set, unconditionally, performing no input or output. Precondition: none. Postcondition:
  never throws, and completes in well under the 50 ms the contract allows. There is genuinely nothing
  to probe: the parser is a managed assembly shipped inside this package.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  reports the environment facts, buffers and opens the document, selects the pages, reports the
  document info, delegates to `PdfImageExtractor` and then `PdfTextExtractor`, writes the resulting
  markdown, reports the structural gaps, and returns degraded when any shortfall was reported and
  succeeded otherwise. Preconditions: both arguments non-null. Postcondition: every artifact was
  routed through the context's sink. Images are extracted *before* text because the text renderer
  places the links the sink allocated for them.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** — returns two cases: a parse round trip that
  builds a one-page document in memory and reads its glyphs back, and a page-rendering case that
  reports a reasoned skip. Enumeration is cheap; the work happens only when a case's delegate runs.
- **`ReadSourceAsync`** (private) — copies the source into memory. A stream-backed `DocumentSource`
  is not guaranteed seekable, and a PDF parser must seek to the cross-reference table at the end of
  the file, so buffering makes both source kinds behave identically instead of making stream sources
  a special case that fails late.
- **`SelectPages`** (private) — returns the document's pages filtered by any requested `PageRange`.
  A range wider than the document yields the pages that exist rather than an error.
- **`ReportStructuralGaps`** (private) — reports the three shortfalls this unit owns: a document with
  no pages, a document with no text layer on any extracted page, and a request for rendered pages.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. Parser faults —
encryption, malformation, truncation — are deliberately **not** translated here: they propagate, and
the engine converts any backend exception into a coded
`ExtractionFailureKind.ExtractorFailed` failure with the full output layout still written.
Translating them locally would duplicate that machinery and discard the parser's own explanation of
what was wrong with the document. `OperationCanceledException` likewise propagates, as the contract
requires. The self-test cases are the one exception to this rule: a case reports every fault as a
failed result rather than throwing, because a self-test exists to produce a health signal rather than
to abort its caller.

### Dependencies

- **PdfTextExtractor** and **PdfImageExtractor** — the delegated work. See their designs.
- **PdfPig** (OTS) — document opening, page access, and document information.
- **DocDown.Core** — `IDocumentExtractor`, `ISelfValidating`, `IExtractionSink`, `ExtractionOptions`,
  `DocumentInfo`, `EnvironmentFact`, `ExtractionGap`, `ExtractionDiagnostic`, `PageRange`.

### Callers

The engine resolves and invokes this unit after selection. `PdfDocDownBuilderExtensions` constructs
it, through a factory, when a host builds an engine. See *PdfDocDownBuilderExtensions Design*.
