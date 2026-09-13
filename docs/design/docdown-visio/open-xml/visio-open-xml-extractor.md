## VisioOpenXmlExtractor

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioOpenXmlExtractor` is the managed backend the engine selects for a content extraction — the
guaranteed content path that recovers a drawing's page names, shape text, and directed topology with no
Visio installation present. Its single responsibility is orchestration: declare what it can deliver, probe
unconditionally, record the environment facts, buffer and open the drawing, hand the stream to the reader,
delegate emission to the emitter, and contribute the self-tests that describe its own behavior.

### Data Model

`VisioOpenXmlExtractor` is a `public sealed class` implementing `IDocumentExtractor` and `ISelfValidating`.
It holds no per-extraction state, opens no file at construction, and probes no environment there, so one
instance can be registered once and reused across concurrent extractions.

- **`Id`** — `visio-openxml`. **`DisplayName`** — `Visio (Open Packaging)`.
- **`SupportedFormats`** — `Vsdx` and `Vsdm`. **`Priority`** — `10`, above the COM backend.
- **`Capabilities`** — `Text | EmbeddedImages | DocumentMetadata | DocumentStructure`, pointedly not
  `RenderedPages`.

### Key Methods

- **`ExtractorAvailability ProbeAvailability()`** — always available with the full declared capability set;
  `System.IO.Packaging` is a managed assembly, so if the type could be constructed the extractor can run.
  Performs no I/O and cannot throw.
- **`ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)`** —
  records the `visio.backend` and `visio.pageRendering` environment facts, buffers the source (a stream is
  not guaranteed seekable and the reader must seek), reads the model with `VisioPackageReader.Read`,
  delegates to `VisioContentEmitter.EmitAsync`, and returns `Degraded` when the emitter reported any gap.
- **`GetSelfTestCases`** / **`RunParseRoundTrip`** — contribute a `visio.openxml.parseRoundTrip` case that
  builds a one-page drawing with two labeled shapes and a directed edge, reads it back, and confirms the
  single connection resolved, and a `visio.pageRendering` case that reports skipped with a reason because
  this extractor does not provide rendering.
- **`ReadSourceAsync`** (private) — buffers the source fully so the reader has a seekable stream.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Any fault from the reader propagates to Core,
which converts it into a structured failure with the full layout still written; the extractor itself writes
no bytes, because the sink is the only output channel. The self-test reports every fault as data rather
than throwing at its caller.

### Dependencies

- **DocDown.Core** — the extractor and self-validation contracts, the sink, the options, `DocumentSource`,
  `EnvironmentFact`, and the outcome and self-test types.
- **VisioPackageReader** — reads the drawing into the model.
- **VisioContentEmitter** — emits the model through the sink.
- **VisioPackageBuilder** — builds the self-test drawing in memory.
- **System.IO.Packaging** (transitively, through the reader) — the OPC container reader.

### Callers

The engine resolves and invokes this unit for every `.vsdx`/`.vsdm` content extraction.
`VisioDocDownBuilderExtensions.AddVisio` constructs it, through a factory, when a host builds an engine,
and `VisioComExtractor` constructs one to delegate the managed aspects of a rendering extraction.
