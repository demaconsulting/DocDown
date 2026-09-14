## WordOpenXmlExtractor

![DocDown.Word Structure](WordView.svg)

### Purpose

`WordOpenXmlExtractor` is the managed Word backend the engine selects and invokes for a `.docx`.
Its single responsibility is orchestration: it reports availability, records the environment facts,
buffers and opens the document, hands the stream to `WordOpenXmlReader`, and delegates every
artifact to `WordContentEmitter`. It writes no bytes itself and constructs no output path, because
Core's sink is the only output channel.

### Data Model

`WordOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance safely serves every
extraction.

- **`Id`** (`string`) — `word-openxml`; the stable key for selection, override, and the manifest.
- **`DisplayName`** (`string`) — `Word (Open XML SDK)`; shown in backend listings and summaries.
- **`SupportedFormats`** — `Docx` only. The legacy binary `.doc` format is outside this package's
  scope.
- **`Priority`** (`int`) — `10`; the package's only backend.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns
  `ExtractorAvailability.Available()` unconditionally, performing no I/O. Precondition: none.
  Postcondition: never throws and completes well under the contract's time budget. There is nothing
  to probe: the Open XML SDK is a managed assembly shipped inside this package.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`**
  — reports two environment facts (`word.backend = Open XML SDK (managed)` and
  `word.pageRendering = not provided by this extractor`), buffers the source through
  `ReadSourceAsync()`, opens a read-only `MemoryStream`, constructs a fresh `WordOpenXmlReader`,
  produces a `WordDocumentModel`, and delegates to `WordContentEmitter.EmitAsync()`. Preconditions:
  both arguments non-null. Postcondition: every artifact was routed through the context's sink.
  Normal completion returns `ExtractionOutcome.Produced`.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** — returns two cases:
  - `word.openxml.parseRoundTrip`, which builds a one-paragraph document in memory with
    `WordprocessingDocument.Create()`, reads it back, and passes when the body carries content.
  - `word.pageRendering`, which reports a reasoned skip because this package does not attempt page
    rendering.
- **`ReadSourceAsync()`** (private) — copies the source into memory. Buffering is essential because
  a stream-backed `DocumentSource` is not guaranteed seekable and the SDK's package reader must
  seek to read OPC parts.
- **`RunParseRoundTrip()`** (private) — the self-test case body. It catches every exception and
  converts it into `SelfTestResult.Failed`, because a self-test reports faults as data rather than
  throwing at its caller.
- **`BuildProbeDocument()`** (private) — writes the round-trip document into a stream.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. The only fault the
extractor names intentionally is the one the reader can recognize better than the SDK: the OLE
compound-file signature of a password-protected `.docx`, which the reader raises as
`WordExtractionException`. Other faults such as malformed packages, missing relationships,
truncation, and `OperationCanceledException` propagate to Core. Core converts adverse extraction
exceptions into `ExtractionOutcome.Unreadable` output. The self-test cases are the one exception: a
self-test reports every fault as a result rather than throwing.

### Dependencies

- **DocDown.Core** — `IDocumentExtractor`, `ISelfValidating`, `IExtractionContext`,
  `DocumentSource`, `ExtractionOptions`, `ExtractorAvailability`, `ExtractionOutcome`,
  `SelfTestCase`, `SelfTestResult`, `EnvironmentFact`, and `DocumentFormat`.
- **DocumentFormat.OpenXml** (OTS) — `WordprocessingDocument.Create()` used only by the in-memory
  self-test probe.
- **`WordOpenXmlReader`** — the SDK-to-model translation.
- **`WordContentEmitter`** — the shared model-to-sink emission path.

### Callers

The engine resolves and invokes this unit after selection. `WordDocDownBuilderExtensions.AddWord()`
constructs it, through a factory, when a host builds an engine. See
*WordDocDownBuilderExtensions Design*.
