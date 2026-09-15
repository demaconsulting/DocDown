## WordOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlExtractor`, the
managed backend the engine selects and invokes for a `.docx`.

### Verification Approach

`WordOpenXmlExtractor` is verified through integration tests in `OpenXml/WordOpenXmlExtractorTests.cs`
in `DemaConsulting.DocDown.Office.Tests`, with method names beginning with `WordOpenXmlExtractor_`.

The unit is driven through the engine end to end, not against a stand-in context, because its
observable contract is what a host sees: the descriptor, the environment facts, the produced
folder, the content inventory, and any extraction notes. The SDK is not mocked. The documents are
the real generated fixtures from `TestData/DocxFixtures.cs`, so the unit is exercised against
genuine `.docx` files rather than against a simulation of one.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time by `TestData/DocxFixtures.cs`
- **Filesystem**: a per-test `TempScratch` folder holds the fixture file and output folder
- **Mocking**: none; the SDK is exercised against real documents and the sink is Core's real writer
  through the engine
- **Isolation**: each test constructs its own engine and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlExtractor` test run passes when the descriptor's identity,
priority, supported format, and managed availability are reported correctly; when a vector image is
written unchanged; when a merged-cell document produces a table-flattening note; when an empty
document produces zero text blocks in the content inventory and no separate note; when an
image-bearing document's inline links resolve on disk; when a header logo identical to a body image
reuses one written image path; and when the extractor contributes exactly two self-test cases with
one passing round trip and one skipped page-rendering case.

### Test Scenarios

#### The descriptor matches the managed backend contract

**Test**: `WordOpenXmlExtractor_Descriptor_HasPriority10AndManagedAvailability`

Proves the identity is `word-openxml`, the priority is 10, `.docx` is supported, and the
availability report states the extractor is available while page rendering is not provided here.
Evidence for `DocDownWord-OpenXml-WordOpenXmlExtractor-ReportsManagedAvailability`.

#### A vector image is written as-is

**Test**: `WordOpenXmlExtractor_Extract_VectorImage_WritesAsIs`

Proves the image is written unchanged, the run completes as `Produced`, and no extra note is
required for that image type. Evidence for `DocDownWord-VectorImagesWrittenAsIs` and
`DocDownWord-OpenXml-WordOpenXmlExtractor-WritesVectorImageAsIs`.

#### Flattened table structure is explained by a note

**Test**: `WordOpenXmlExtractor_Extract_MergedCells_ReportsFlatteningNote`

Proves the result remains `Produced` and a note states that merged or nested table cells were
flattened because markdown cannot represent that structure. Evidence for
`DocDownWord-TableStructureNotes` and
`DocDownWord-Markdown-WordContentEmitter-ReportsFlattenedTableNote`.

#### An empty document produces a zero-count text inventory entry

**Test**: `WordOpenXmlExtractor_Extract_EmptyDocument_ProducesZeroCountTextInventory`

Proves the result remains `Produced`, no note is emitted, and `manifest.json` records a `text
blocks` content feature with a count of zero. Evidence for `DocDownWord-EmptyDocumentInventory`.

#### Inline image links resolve on disk

**Test**: `WordOpenXmlExtractor_Extract_ImageLinks_ResolveOnDisk`

Proves an image-bearing document's inline links resolve on disk from the root `content.md` the
backend writes. Evidence for `DocDownWord-Markdown-WordContentEmitter-EmitsModelContent`.

#### A header logo is deduplicated with the body image

**Test**: `WordOpenXmlReader_Read_HeaderWithLogo_ReusesDeduplicatedImagePath`

Proves a logo that appears in both the header and the body is written once rather than twice.
Supporting evidence for the shared image-provenance path.

#### Self-test cases run and report honestly

**Test**: `WordOpenXmlExtractor_SelfValidation_ReportsCases`

Proves the backend contributes exactly two cases under its category, that one result is passed and
one is skipped, and that no case fails. Evidence for
`DocDownWord-OpenXml-WordOpenXmlExtractor-ContributesSelfTests`.
