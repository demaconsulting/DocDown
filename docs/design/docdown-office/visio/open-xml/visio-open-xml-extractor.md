## VisioOpenXmlExtractor

![DocDown.Visio Structure](VisioView.svg)

### Purpose

`VisioOpenXmlExtractor` is the managed backend the engine selects for ordinary content extraction.
Its single responsibility is orchestration: identify the modern Visio drawings it reads, probe
available everywhere the package can load, record the managed-path environment facts, buffer and
open the drawing, hand the stream to the reader, delegate emission to the shared emitter, and
contribute self-tests that describe its own behavior.

### Data Model

`VisioOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and
`ISelfValidating`. It holds no per-extraction state, opens no file at construction, and probes no
environment there, so one instance can be registered once and reused across concurrent extractions.

- **`Id`** — `visio-openxml`. **`DisplayName`** — `Visio (Open Packaging)`.
- **`SupportedFormats`** — `Vsdx` and `Vsdm`. **`Priority`** — `10`, above the COM backend.
- **`PageRenderingApplicable`** — `true`, because Visio drawings are paginated even though this
  managed backend does not render them.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — returns `Available()` everywhere the package can
  load. It performs no I/O and reports no rendered-page support.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`**
  — records `visio.backend` and `visio.pageRendering`, buffers the source because the reader must
  seek, reads the model with `VisioPackageReader.Read`, delegates to `VisioContentEmitter.EmitAsync`,
  and returns `Produced` on normal completion.
- **`GetSelfTestCases()` / `RunParseRoundTrip`** — contribute a `visio.openxml.parseRoundTrip`
  self-test that reads the embedded probe drawing — two labeled shapes joined by a glued connector —
  and proves the connection resolves, and a
  `visio.pageRendering` case that reports skipped with a reason because rendering belongs to the COM
  backend.
- **`ReadSourceAsync`** (private) — buffers the source fully so the reader has a seekable stream.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Any fault from the reader propagates to
Core, which converts it into an unreadable result with the full layout still written; the extractor
itself writes no bytes outside the sink. The self-test reports every fault as data rather than
throwing at its caller.

### Dependencies

- **DocDown.Core** — the extractor and self-validation contracts, sink, options,
  `DocumentSource`, `EnvironmentFact`, and outcome and self-test types.
- **VisioPackageReader** — reads the drawing into the model.
- **VisioContentEmitter** — emits the model through the sink.
- **`SelfTestProbe`** (Core) — loads the embedded drawing the self-test reads.
- **System.IO.Packaging** (transitively, through the reader) — the OPC container reader.

### Callers

The engine resolves and invokes this unit for every ordinary `.vsdx` or `.vsdm` extraction.
`VisioDocDownBuilderExtensions.AddVisio` constructs it through a factory when a host builds an
engine, and `VisioComExtractor` constructs one to delegate the managed aspects of a rendering
extraction.
