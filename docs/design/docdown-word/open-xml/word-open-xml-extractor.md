## WordOpenXmlExtractor

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordOpenXmlExtractor` is the managed Word backend the engine selects and invokes for a `.docx`.
Its single responsibility is orchestration: it declares what this backend can deliver, answers the
availability probe unconditionally, records the environment facts, buffers and opens the document,
hands the stream to `WordOpenXmlReader`, and delegates every artifact to `WordContentEmitter`. It
writes no bytes itself and constructs no path, because Core's sink is the only output channel.

### Data Model

`WordOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance safely serves
every extraction.

- **`Id`** (`string`) — `word-openxml`; the stable key for selection, caller override, and the
  manifest use.
- **`DisplayName`** (`string`) — `Word (Open XML SDK)`; shown in `--list-backends` and summaries.
- **`SupportedFormats`** — `Docx` only. The legacy binary `.doc` format is not supported by DocDown.
- **`Capabilities`** — `Text | EmbeddedImages | DocumentMetadata | DocumentStructure`.
  `RenderedPages` is pointedly absent because this package ships no renderer and declares no
  capability it cannot deliver.
- **`Priority`** (`int`) — `10`; the package's only backend, so the value simply places Word
  extraction above a hypothetical lower-priority generic reader a host might also register.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns `Available(Capabilities)`
  unconditionally, performing no I/O. Precondition: none. Postcondition: never throws, completes
  in well under the 50 ms the contract allows. There is nothing to probe: the Open XML SDK is a
  managed assembly shipped inside this package, so if this type could be constructed the extractor
  can run.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  reports two environment facts (`word.backend = Open XML SDK (managed) (available)`,
  `word.pageRendering = not provided by this extractor (NOT available)`), buffers the source
  through `ReadSourceAsync`, opens a read-only `MemoryStream` over the buffered bytes, constructs
  a fresh `WordOpenXmlReader` and calls its `Read` to produce a `WordDocumentModel`, and delegates
  to `WordContentEmitter.EmitAsync`. Returns `Degraded` when the emitter reported any gap and
  `Succeeded` otherwise. Preconditions: both arguments non-null. Postcondition: every artifact was
  routed through the context's sink. Buffering is what makes a stream source and a file source
  behave identically and gives the reader the seekable stream it needs to inspect the OLE
  compound-file signature and open the package.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** — returns two cases:
  - `word.openxml.parseRoundTrip`, which builds a one-paragraph document in memory with
    `WordprocessingDocument.Create`, reads it back with the reader, and passes when the body
    carries content or fails with the caught exception message when it does not. Building rather
    than embedding a fixture keeps the case free of a shipped binary payload and exercises the
    writer and reader together.
  - `word.pageRendering`, which reports a reasoned skip because rendered pages are a capability
    this package does not claim. Emitting a skip is correct and safe; because a traceability
    pipeline does not count a not-executed result as executed evidence, the skip satisfies no
    requirement — see the `SelfTest` subsystem design.
- **`ReadSourceAsync`** (private) — copies the source into memory. Buffering is essential because
  a stream-backed `DocumentSource` is not guaranteed seekable and the SDK's package reader must
  seek to read the OPC parts.
- **`RunParseRoundTrip`** (private) — the case body: catches every exception (a self-test reports
  every fault as data rather than throwing at its caller, so `CA1031` is deliberately suppressed
  here) and returns `SelfTestResult.Failed` with the message and the elapsed duration.
- **`BuildProbeDocument`** (private) — writes the round-trip document into a stream.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. The only fault the
extractor's own code translates is the one the reader can recognize better than the SDK: the OLE
compound-file signature of a password-protected `.docx`, which the reader raises as a
`WordExtractionException`. Every other fault (malformed package, missing relationship,
truncation) propagates to Core, which converts it into a coded, structured `ExtractorFailed`
failure with the full output layout still written. Translating them locally would duplicate that
machinery and discard the SDK's own explanation of what was wrong. `OperationCanceledException`
likewise propagates. The self-test cases are the one exception to this rule: a case reports every
fault as a failed result rather than throwing.

### Dependencies

- **DocDown.Core** — `IDocumentExtractor`, `ISelfValidating`, `IExtractionSink`,
  `IExtractionContext`, `DocumentSource`, `ExtractionOptions`, `ExtractorAvailability`,
  `ExtractorCapabilities`, `ExtractionOutcome`, `SelfTestCase`, `SelfTestResult`,
  `DocumentFormat` (as `CoreFormat`), `EnvironmentFact`.
- **DocumentFormat.OpenXml** (OTS) — `WordprocessingDocument.Create` used only by the self-test
  probe document.
- **`WordOpenXmlReader`** — the SDK-to-model translation.
- **`WordContentEmitter`** — the shared model-to-sink emission.

### Callers

The engine resolves and invokes this unit after selection. `WordDocDownBuilderExtensions.AddWord`
constructs it, through a factory, when a host builds an engine. See
*WordDocDownBuilderExtensions Design*.
