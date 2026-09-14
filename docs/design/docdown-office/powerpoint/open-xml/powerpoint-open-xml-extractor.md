### PowerPointOpenXmlExtractor

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Purpose

`PowerPointOpenXmlExtractor` is the managed PowerPoint backend the engine selects and invokes for a
`.pptx` content extraction. Its single responsibility is orchestration: it exposes the selection
surface the engine uses, answers the availability probe unconditionally, records the environment facts,
buffers and opens the deck, hands the stream to `PowerPointOpenXmlReader`, and delegates every output
artifact to `PowerPointContentEmitter`. It writes no bytes itself and constructs no output path,
because Core's sink is the only output channel.

### Data Model

`PowerPointOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance safely serves every
extraction.

- **`Id`** (`string`) — `powerpoint-openxml`; the stable key for selection, caller override, and
  manifest use.
- **`DisplayName`** (`string`) — `PowerPoint (Open XML SDK)`.
- **`SupportedFormats`** — `Pptx` only. The legacy binary `.ppt` format is not supported by DocDown.
- **`PageRenderingApplicable`** — `true`; the backend participates in `.pptx` extraction requests
  even though rendered pages are not provided by this implementation.
- **`Priority`** (`int`) — `10`; higher than the COM backend, so the managed backend is the default
  for normal content extraction.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns `Available()` unconditionally, performing
  no I/O. Postcondition: the available result leaves rendered-page support false. There is nothing to
  probe: the Open XML SDK is a managed assembly shipped inside this package.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  reports two environment facts (`powerpoint.backend = Open XML SDK (managed)` and
  `powerpoint.pageRendering = not provided by this extractor`), buffers the source through
  `ReadSourceAsync`, opens a read-only `MemoryStream` over the buffered bytes, calls
  `PowerPointOpenXmlReader.Read` to produce a `PowerPointDeckModel`, delegates to
  `PowerPointContentEmitter.EmitAsync`, and returns `Produced` when extraction completes.
  Preconditions: both arguments non-null.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** — returns two cases:
  - `powerpoint.openxml.parseRoundTrip`, which builds a one-slide deck in memory with
    `PresentationDocument.Create`, reads it back, and passes when the model carries at least one
    slide. Building rather than embedding a fixture keeps the case free of a shipped binary payload.
  - `powerpoint.pageRendering`, which reports a reasoned skip because the managed backend does not
    render slide images. A behavior this backend does not provide must not be reported as a pass or a
    failure.
- **`ReadSourceAsync`** (private) — copies the source into memory, because a stream-backed
  `DocumentSource` is not guaranteed seekable and the SDK's package reader must seek.
- **`RunParseRoundTrip`** / **`BuildProbeDeck`** (private) — the round-trip case body and the one-slide
  deck it reads back; the case catches every exception and reports it as a failed result rather than
  throwing.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. The reader translates a package it cannot
open or a missing presentation part into a `PowerPointExtractionException`; every other fault
propagates to Core, which converts it into an `Unreadable` result with the full output layout still
written. `OperationCanceledException` propagates. The self-test cases are the one exception to this
rule: a case reports every fault as a failed result rather than throwing.

### Dependencies

- **DocDown.Core** — `IDocumentExtractor`, `ISelfValidating`, `IExtractionSink`,
  `IExtractionContext`, `DocumentSource`, `ExtractionOptions`, `ExtractorAvailability`,
  `ExtractionOutcome`, `SelfTestCase`, `SelfTestResult`, `DocumentFormat`, and `EnvironmentFact`.
- **DocumentFormat.OpenXml** (OTS) — `PresentationDocument.Create` used only by the self-test probe
  deck.
- **PowerPointOpenXmlReader** — the SDK-to-model translation.
- **PowerPointContentEmitter** — the shared model-to-sink emission.

### Callers

The engine resolves and invokes this unit after selection. `PowerPointDocDownBuilderExtensions`
constructs it, through a factory, when a host builds an engine. The COM backend also constructs and
runs it by delegation.
