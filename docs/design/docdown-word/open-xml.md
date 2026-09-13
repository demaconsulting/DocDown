## OpenXml Subsystem

![DocDown.Word Structure](DocDownWordView.svg)

### Overview

The OpenXml subsystem is the managed Word extraction backend. It reads a `.docx` package through
the Open XML SDK, populates the shared `WordDocumentModel`, and hands the model to
`WordContentEmitter`, which routes every byte and report through the sink. The subsystem is fully
managed, carries no native asset, and is deployable anywhere the .NET runtime is.

The subsystem exists because two responsibilities are cleanly separable: the extractor the engine
selects and probes (`WordOpenXmlExtractor`), and the SDK-to-model translation whose surface is
substantial and whose scope is bounded (`WordOpenXmlReader`, with `WordOpenXmlImageReader`
factored out because image resolution is scoped to an `OpenXmlPartContainer` rather than to the
document as a whole).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | Cheap no-I/O availability probe |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; case bodies do the work |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream source may not seek |
| `IExtractionSink` | Outbound, via `WordContentEmitter` | .NET interface | The only output channel |
| `WordDocumentModel` | Outbound, from the reader | .NET record | The pivot between reading and rendering |
| `WordprocessingDocument` | Internal | Open XML SDK | Opened read-only |

### Design

**The extractor descriptor.** `WordOpenXmlExtractor` declares `Id = "word-openxml"`,
`DisplayName = "Word (Open XML SDK)"`, `SupportedFormats = [Docx]`, and `Priority = 10`.
`ProbeAvailability()` returns `ExtractorAvailability.Available()` unconditionally, with no I/O:
the SDK is a managed assembly shipped inside this package, so if the type can be constructed the
extractor can run.

**The extraction path.** `ExtractAsync()` reports two environment facts,
`word.backend = Open XML SDK (managed)` and
`word.pageRendering = not provided by this extractor`, buffers the source into memory, opens a
read-only `MemoryStream`, builds a fresh `WordOpenXmlReader`, and hands the resulting model to
`WordContentEmitter`. Normal completion returns `ExtractionOutcome.Produced`.

**The self-test set.** Two cases are exposed: `word.openxml.parseRoundTrip`, which builds a
one-paragraph document in memory with `WordprocessingDocument.Create()`, reads it back, and passes
when the body contains content; and `word.pageRendering`, which reports a skip with a reason
because this package does not attempt page rendering.

**The reader.** `WordOpenXmlReader` opens the package read-only, walks the body once, and produces
the model. It handles styled paragraphs (`Heading 1` through `Heading 9`, `Title`, and
`w:outlineLvl`), ordered and bulleted lists from `numbering.xml`, genuine tables built from
`w:tbl`, embedded images from `a:blip`, the accepted view of tracked changes, comment content
through the comments part, and footnotes as inline references with trailing bodies. It also reads
metadata from the core and extended properties.

**The document-control build.** `BuildDocumentControl()` is the reader's headline correctness
step. It collects header and footer parts from every `w:sectPr`, renders each through the same
block walker as the body, strips page-numbering furniture by examining the field instruction's
first token, and deduplicates identical rendered content across sections. Empty and furniture-only
parts are omitted from the rendered section instead of being surfaced as content.

**The image reader.** `WordOpenXmlImageReader` resolves a drawing's `r:embed` relationship within
its containing part, returning a `WordImageRef` that carries the complete part bytes, content
type, selected name or description text, and source URI. The same helper also enumerates package
image parts for tests, always pairing the bytes with a passthrough image hint.

**Adverse cases.** The subsystem deliberately translates only the conditions it can name more
clearly than the SDK. The OLE compound-file signature that fronts a password-protected `.docx`
becomes a plain `WordExtractionException`. Other faults such as malformed packages, truncated
parts, or missing relationships propagate to Core, which renders them as unreadable output.
Translating everything locally would duplicate Core's failure machinery and discard the SDK's own
explanation.

**Self-containment.** No Open XML SDK type reaches the host-facing surface. The extractor is public
because a host registers it indirectly, but its public members name Core types only; the reader and
image reader stay internal and are exposed to tests through `InternalsVisibleTo`.
