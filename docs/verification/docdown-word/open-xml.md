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
observable contract is what the engine and a host observe: descriptor data, environment facts,
produced content, content inventory, and any extraction notes. The reader is driven directly
against generated documents, because its contract is the `WordDocumentModel` it produces from a
`.docx`. The image reader is driven directly against an opened `WordprocessingDocument`, because
its contract is the sequence of image parts and their passthrough hints.

The SDK is not mocked. Every fixture is a real `.docx` built at test time by the Open XML SDK
writer in `TestData/DocxFixtures.cs`, so each scenario exercises a genuine document rather than a
simulation of one.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by the Open XML SDK writer; no
  committed binaries and no network access
- **Filesystem**: per-test `TempScratch` folders receive the extractor's output; the reader and
  image reader tests operate on streams and opened documents
- **Mocking**: none; the SDK is exercised against real documents and the sink is Core's real writer
  through the engine
- **Isolation**: each test opens its own document and constructs its own extractor or reader

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor reports its managed
availability, supports `.docx`, returns `Produced` for readable modern documents, reports zero text
blocks for an empty document rather than a separate note, writes a vector image unchanged, writes a
merged-cell document together with a table-flattening note, and exposes
two honest self-test cases; when the reader maps styled paragraphs, lists, and tables into
structured blocks, builds a Document Control section from header content, omits page-number-only
footer content from that section, deduplicates identical headers, renders tracked changes in the
accepted view, detects a password-protected container with `WordExtractionException`, collects
comments, surfaces metadata, reports the producer-stated page count, and chooses image naming text
from authored sources; and when the image reader yields a PNG's bytes unchanged with a passthrough
hint and reports an EMF part's media type truthfully.

### Test Scenarios

The subsystem's behavior is verified by the unit-level scenarios below and by the detailed unit
chapters.

#### The extractor orchestrates the managed extraction end to end

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndManagedAvailability`

Proves the descriptor directly. The extraction-time behavior — empty-document inventory, the
content flow, vector passthrough, the table-flattening note, and the self-tests — is given in
the *WordOpenXmlExtractor Verification Design*. Evidence for `DocDownWord-OpenXml-Extraction`.

#### The reader turns a document into the backend-neutral model

**Test**: `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

Proves the mapping directly for structured blocks. The Document Control, tracked-changes,
protected-container, comment, metadata, page-count, deduplication, and image-text scenarios are
given in the *WordOpenXmlReader Verification Design*. Evidence for `DocDownWord-OpenXml-Reading`.

#### The image reader yields passthrough images with honest provenance

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the passthrough hint, null pixel dimensions, and media type directly, with the vector
media-type case given in the *WordOpenXmlImageReader Verification Design*. Evidence for
`DocDownWord-OpenXml-Images`.
