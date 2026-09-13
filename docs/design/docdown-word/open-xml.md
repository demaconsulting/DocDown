## OpenXml Subsystem

![DocDown.Word Structure](DocDownWordView.svg)

### Overview

The OpenXml subsystem is the managed Word extraction backend. It reads a `.docx` package through
the Open XML SDK, populates the shared `WordDocumentModel`, and hands the model to
`WordContentEmitter` which routes every byte through the sink. The subsystem is fully managed,
carries no native asset, and is deployable anywhere the .NET runtime is; it is the reason
`DocDown.Word` can claim runtime-identifier agnosticism as a package property rather than an
aspiration.

The subsystem exists because two responsibilities are cleanly separable: the descriptor the engine
selects and probes (`WordOpenXmlExtractor`), and the SDK-to-model translation whose surface is
substantial and whose scope is bounded (`WordOpenXmlReader`, with `WordOpenXmlImageReader`
factored out because image resolution is scoped to a `OpenXmlPartContainer` rather than to the
document as a whole).

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | `ProbeAvailability` under 50 ms, no I/O, no throw |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration is cheap; work runs only in a delegate |
| `DocumentSource` | Inbound, from Core | .NET record | Buffered before opening because a stream may not seek |
| `IExtractionSink` | Outbound, via `WordContentEmitter` | .NET interface | The only output channel |
| `WordDocumentModel` | Outbound, from the reader | .NET record | The pivot between reading and rendering |
| `WordprocessingDocument` | Internal | Open XML SDK | Opened read-only; never `InnerXml`-materialized |

### Design

**The descriptor.** `WordOpenXmlExtractor` declares `Id = "word-openxml"`, `DisplayName = "Word
(Open XML SDK)"`, `SupportedFormats = [Docx]`, `Priority = 10`, and `Capabilities = Text |
EmbeddedImages | DocumentMetadata | DocumentStructure`. `RenderedPages` is pointedly absent. The
`ProbeAvailability` implementation returns `Available(Capabilities)` unconditionally, with no I/O:
there is nothing to probe because the SDK is a managed assembly shipped inside this package.

**The self-test set.** Two cases: a `word.openxml.parseRoundTrip` case that builds a one-paragraph
document in memory with `WordprocessingDocument.Create`, reads it back with the reader, and passes
when the body carries content; and a `word.pageRendering` case that reports a reasoned skip
because rendered pages are a capability this package does not claim. Building rather than
embedding a fixture keeps the case free of a shipped binary payload and exercises the writer and
reader together.

**The reader.** `WordOpenXmlReader` opens the package read-only, walks the body once, and produces
the model. It handles: styled paragraphs (`Heading1..9`, `Title` → `#`, with `w:outlineLvl` as the
fallback); numbered and bulleted lists from `numbering.xml`, with the ordered/bulleted decision on
`w:numFmt`; genuine tables built by `BuildTable` using the seven rules the writer expects
(counting merged and nested cells so the emitter can emit `WORD0005`); images from `A:Blip`
elements, resolved through `WordOpenXmlImageReader`; the accepted-revisions view of tracked
changes (`w:ins` kept and counted, `w:del` dropped and counted); footnotes as `[^n]` markers with
a trailing bodies list; and comments through the comments part. The reader detects the OLE
compound-file signature up front — the container fronting a password-protected `.docx` — and
raises a plain `WordExtractionException` so the message reaching the caller is the backend's own
explanation rather than the SDK's raw package error.

**The document-control build.** `BuildDocumentControl` is the reader's headline correctness step:
it collects header and footer parts from every `w:sectPr`, renders each through the same walker
with `stripFurniture: true`, and detects page-numbering furniture **structurally** by field
instruction — `PAGE`, `NUMPAGES`, `SECTIONPAGES`, `SECTIONPAGESNUM`, matched on the instruction's
first token so `PAGEREF` is not misclassified. Never by regex over rendered text: rendered page
numbers are locale-dependent and format-dependent, and a text regex would misfire on documents
where numbers look like nothing else and fire on revision strings that happen to contain digits.
Identical rendered content across sections collapses to one entry so a header repeated on every
section survives once; a part reduced to nothing is counted, and its reason (`contains only page
numbering` versus `is empty`) is carried on the model for the emitter's `WORD0009` gap.

**The image reader.** `WordOpenXmlImageReader` is static and does two things: it resolves a blip's
`r:embed` id within its containing part (so a header's image parts are resolved against the
header, not the main part), returning a `WordImageRef` that carries the bytes, the media type, the
preferred name, and the part URI; and it enumerates every image part of the document as
`(byte[], ImageHint)` pairs, always claiming `ImageTransform.Passthrough` with null pixel
dimensions. Both operations are read-only I/O over an already-open package.

**Adverse cases.** The reader deliberately translates only the one condition it can recognize
better than the SDK — the OLE compound-file signature — because a hand-written message about
encryption is clearer than a raw `PackageException`. Every other fault (malformed package,
truncated part, missing relationship) propagates to Core, which converts it into an
`ExtractorFailed` failure with the full layout still written. Translating everything locally would
duplicate that machinery and discard the SDK's own explanation of what was wrong.

**Self-containment.** No SDK type reaches the public surface: `WordOpenXmlExtractor` is public but
its public members (from `IDocumentExtractor` and `ISelfValidating`) name Core types only;
`WordOpenXmlReader` and `WordOpenXmlImageReader` are internal, exposed to the test project through
`InternalsVisibleTo` for the same reason `PdfPig` types are confined to three files in
`DocDown.Pdf`.
