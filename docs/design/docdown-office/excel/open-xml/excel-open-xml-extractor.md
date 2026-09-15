## ExcelOpenXmlExtractor

![DocDown.Excel Structure](ExcelView.svg)

### Purpose

`ExcelOpenXmlExtractor` is the managed Excel backend the engine selects and invokes for an `.xlsx`. Its
single responsibility is orchestration: it identifies the backend, answers the availability probe
unconditionally, records the environment facts, buffers and opens the workbook, hands the stream to
`ExcelOpenXmlReader`, and delegates every extracted artifact to `ExcelContentEmitter`. It writes no
bytes itself and constructs no path, because Core's sink is the only output channel.

### Data Model

`ExcelOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, so one registered instance safely serves every
extraction.

- **`Id`** (`string`) — `excel-openxml`; the stable key for selection, caller override, and manifest
  use.
- **`DisplayName`** (`string`) — `Excel (Open XML SDK)`.
- **`SupportedFormats`** — `Xlsx` only. The legacy binary `.xls` format is not supported by DocDown.
- **`Priority`** (`int`) — `10`; the package's only backend, placing Excel extraction above a
  hypothetical lower-priority generic reader a host might also register.
- **`PageRenderingApplicable`** (`bool`) — `false`; a workbook has no page grid, so page rendering
  applies to nothing rather than being deferred work.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns `ExtractorAvailability.Available()`
  unconditionally, performing no I/O. Precondition: none. Postcondition: never throws and completes
  well under the 50 ms the contract allows. There is nothing to probe: the Open XML SDK is a managed
  assembly shipped inside this package.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  reports two environment facts (`excel.backend = Open XML SDK (managed)` and
  `excel.pageRendering = not applicable to a non-paginated workbook`), buffers the source through
  `ReadSourceAsync`, opens a read-only `MemoryStream` over the buffered bytes, calls
  `ExcelOpenXmlReader.Read` to produce an `ExcelWorkbookModel`, delegates to
  `ExcelContentEmitter.EmitAsync`, and returns `ExtractionOutcome.Produced` when those steps complete.
  Preconditions: both arguments non-null. Buffering is what makes a stream source and a file source
  behave identically and gives the package reader the seekable stream it needs.
- **`IEnumerable<SelfTestCase> GetSelfTestCases()`** — returns two cases:
  - `excel.openxml.parseRoundTrip`, which reads the embedded workbook authored in Microsoft Excel
    and passes when the model carries at least one
    worksheet. Embedding a probe Excel itself authored is what makes the case prove this deployment
    can read what the real application emits, rather than that a library agrees with itself.
  - `excel.pageRendering`, which reports a reasoned skip because a workbook is non-paginated and page
    rendering does not apply.
- **`ReadSourceAsync`** (private) — copies the source into memory, because a stream-backed
  `DocumentSource` is not guaranteed seekable and the SDK's package reader must seek.
- **`RunParseRoundTrip`** (private) — the round-trip case body. It reads the embedded workbook named
  by `ProbeResourceName`, a real `.xlsx` authored in Microsoft Excel; the case catches every exception
  and reports it as a failed result
  rather than throwing.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. `ExcelOpenXmlReader` throws
`ExcelExtractionException` for a package it cannot open or a missing workbook part; every other fault
propagates to Core, which converts it into a structured `Unreadable` result with the full output layout
still written where possible. `OperationCanceledException` propagates. The self-test cases are the one
exception to this rule: a case reports every fault as data rather than throwing.

### Dependencies

- **DocDown.Core** — `IDocumentExtractor`, `ISelfValidating`, `IExtractionSink`, `IExtractionContext`,
  `DocumentSource`, `ExtractionOptions`, `ExtractorAvailability`, `ExtractionOutcome`, `SelfTestCase`,
  `SelfTestResult`, `DocumentFormat` (as `CoreFormat`), `EnvironmentFact`.
- **DocumentFormat.OpenXml** (OTS) — the managed reader this backend is built on
  workbook.
- **ExcelOpenXmlReader** — the SDK-to-model translation.
- **ExcelContentEmitter** — the shared model-to-sink emission.

### Callers

The engine resolves and invokes this unit after selection. `ExcelDocDownBuilderExtensions.AddExcel`
constructs it, through a factory, when a host builds an engine. See *ExcelDocDownBuilderExtensions
Design*.
