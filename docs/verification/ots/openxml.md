## Open XML SDK Verification

This document provides the verification evidence for the Open XML SDK OTS software item.
Requirements for this OTS item are defined in the Open XML SDK OTS Software Requirements document.

### Required Functionality

The Open XML SDK is the managed WordprocessingML reader and writer `DocDown.Word` is built on. It
must open a `.docx` package without native binaries, expose the document's styled paragraphs,
lists, real tables, comments, footnotes, headers, and footers, enumerate embedded image parts with
their stored bytes and content type, and build documents suitable for use as test fixtures.

### Verification Approach

**The Open XML SDK is verified by transitive evidence from the `DocDown.Word` test suite.** Per
the software-items standard, a dedicated OTS test project is required only *if no other
verification evidence is available*. That is not the case here: the SDK is a runtime library on
the critical path of every `DocDown.Word` extraction, not a build-time tool whose correct operation
must be inferred from a pipeline completing. The extraction and unit tests named in the scenarios
below — those in `DocDownWordTests`, `WordOpenXmlExtractorTests`, `WordOpenXmlReaderTests`, and
`WordOpenXmlImageReaderTests` — each open a real generated document end to end, on three target
frameworks, in every CI matrix combination.

The evidence is limited to the tests named below. The claim is transitive, not comprehensive:
each scenario proves the SDK delivers what `DocDown.Word` promises on top of it, not that every
part of the SDK is exercised. Tests that reflect only over public surface — for example, the
`WordDocDownBuilderExtensions_*` scenarios that inspect a builder's descriptor list without
opening a document — reach no SDK code and are not claimed here.

A dedicated `test/OtsSoftwareTests/` project would therefore re-test, against synthetic inputs,
the same library paths the extraction suite already exercises against the documents this package
actually has to handle — and it would do so at one remove from the behavior that matters, since
what the repository needs to know is not that the SDK works in isolation but that it delivers what
`DocDown.Word` promises on top of it. The extraction suite answers that question directly. No
such project is created.

### Test Environment

The evidence is produced by the standard `DocDown.Word` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix. Every fixture is a
WordprocessingML document generated at test time by the SDK's own writer, so the evidence depends
on no committed binary and no network access.

### Test Scenarios

#### Managed WordprocessingML reading

**Tests**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`,
`WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

The first proves the SDK opens a generated document and reads its structure end to end through the
engine, producing the full four-artifact layout with a real GFM table and a Document Control
heading, on every framework in the matrix. The second proves the reader turns the SDK's DOM into
the backend-neutral model — a heading block, at least one bulleted list item, and a Table block
whose model has three rows and its first-row-is-header flag set — so styled paragraphs, lists, and
real tables are exposed rather than flattened. Together they are direct evidence that the SDK
supports the structure this package's central promise depends on. Evidence for
`DocDown-OTS-OpenXml`.

#### Embedded image enumeration

**Tests**: `DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem`,
`WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

The first proves the SDK's image parts reach the extractor and the written PNG is byte-identical
to the fixture's source PNG — so the enumeration exposes the parts and each part's stored bytes.
The second proves the image reader retrieves the part's own media type (`image/png`) and its bytes
directly from the SDK, and that a byte-identity comparison against the fixture's source PNG
holds — which is what makes the passthrough claim depend on the SDK's exposure of the raw stored
bytes rather than on any label. Evidence for `DocDown-OTS-OpenXml-Images`.

#### Document creation for fixtures

**Tests**: `WordOpenXmlReader_Read_HeaderWithRevisionAndClassification_ProducesDocumentControlSection`,
`WordOpenXmlExtractor_SelfValidation_ReportsCases`

Every fixture the suite uses is built at test time by the SDK's writer, so a scenario that reads a
document is also a scenario that trusts the writer to have produced one that can be read back.
The first proves the writer produced a header carrying a revision and a classification the reader
then recovers into the model's Document Control collection. The second is a round-trip case
contributed by the extractor's own self-validation: it reads back the embedded probe document the
SDK's writer produced, so a defect in either half is caught here. Evidence for
`DocDown-OTS-OpenXml-DocumentCreation`.
