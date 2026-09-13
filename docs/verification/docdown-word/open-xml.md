## OpenXml Subsystem Verification Design

This document describes the verification strategy for the OpenXml subsystem, the managed Word
backend: the extractor the engine selects, the reader that turns the Open XML DOM into the
backend-neutral model, and the image reader that yields each embedded image's bytes and
provenance.

### Verification Approach

The OpenXml subsystem is verified through tests exercising its three units — `WordOpenXmlExtractor`,
`WordOpenXmlReader`, and `WordOpenXmlImageReader` — in `OpenXml/WordOpenXmlExtractorTests.cs`,
`OpenXml/WordOpenXmlReaderTests.cs`, and `OpenXml/WordOpenXmlImageReaderTests.cs`, all in
`DemaConsulting.DocDown.Word.Tests`.

The extractor is driven through the engine end to end against generated documents, because its
contract is what the engine and a host observe: a descriptor, a run over a real document, and the
diagnostics and gaps that reach the produced folder. The reader is driven directly against
generated documents, because its contract is the model it produces from a `.docx`; every scenario
opens a fixture stream, calls `Read`, and asserts on the returned model. The image reader is
driven directly against an opened `WordprocessingDocument`, because its contract is the sequence of
image parts and their transform hints — the level at which a passthrough claim can be checked
against the true stored bytes.

The SDK is **not** mocked. Every fixture is a real `.docx` built at test time by the Open XML SDK
writer in `TestData/DocxFixtures.cs`, so each scenario exercises a genuine document rather than a
simulation of one; mocking the SDK would verify only that the units call the API they were written
to call.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by the Open XML SDK writer; no
  committed binaries and no network access
- **Filesystem**: per-test `TempScratch` folders receive the extractor's output; the reader and
  image reader tests operate on streams and opened documents
- **Mocking**: none; the SDK is exercised against real documents and the sink is Core's real writer
  through the engine
- **Isolation**: each test opens its own document and constructs its own extractor, reader, or
  image reader

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor declares exactly the
supported format, priority, and capabilities and — expressly — does not declare the rendered-pages
capability; when a merged-cell document produces a counted structural gap and its diagnostic; when
a force-PNG request that cannot be honored is written through and explained; when an empty document
degrades with the `WORD0001` diagnostic; when a page-rendering request degrades with a declarative
gap and no remedy the reader could act on and fail at; when a per-part request produces a `parts/` folder split at
each top-level heading; when a vector image is written unchanged with the `WORD0006` caveat; when
the extractor contributes exactly two self-test cases with the round-trip case passing and the
rendering case skipped; when the reader maps styled paragraphs, lists, and tables into structured
blocks, builds a Document Control section from a header carrying a revision and classification,
omits a page-number-only footer as page furniture recorded by an informational diagnostic,
deduplicates identical headers across sections,
renders tracked changes in the accepted view, detects a password-protected container with a
`WordExtractionException`, collects a comment's author and text, reports the producer-stated page
count from the extended properties, surfaces the declared title and author, and reuses a single
image path for a logo shared between header and body; and when the image reader yields a PNG's
bytes unchanged with a passthrough hint, unstated pixel dimensions, and the part's own media type,
and reports the vector media type for an EMF part.

### Test Scenarios

The subsystem's behavior is verified by the unit-level scenarios below and by the reading scenarios
in the reader chapter; the full per-scenario detail is given in each unit chapter.

#### The extractor orchestrates the managed extraction end to end

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndCapabilities`

Proves the descriptor directly. The extraction-time behavior — force-PNG explanation, empty
document, page rendering, per-part split, vector caveat, structural gap, and self-tests — is given
in the *WordOpenXmlExtractor Verification Design*. Evidence for `DocDownWord-OpenXml-Extraction`.

#### The reader turns a document into the backend-neutral model

**Test**: `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

Proves the mapping directly for structured blocks. The Document Control, tracked changes,
protected-container, comment, metadata, page-count, deduplication, and page-number-footer
scenarios are given in the *WordOpenXmlReader Verification Design*. Evidence for
`DocDownWord-OpenXml-Reading`.

#### The image reader yields passthrough images with honest provenance

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the passthrough hint, the null pixel dimensions, and the media type directly, with the
vector media-type case given in the *WordOpenXmlImageReader Verification Design*. Evidence for
`DocDownWord-OpenXml-Images`.
