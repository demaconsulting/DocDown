## PdfPig Verification

This document provides the verification evidence for the PdfPig OTS software item. Requirements for
this OTS item are defined in the PdfPig OTS Software Requirements document.

### Required Functionality

PdfPig is the managed PDF parser `DocDown.Pdf` is built on. It must parse PDF documents without
native binaries, expose glyphs with the position information needed to recover reading order,
enumerate embedded images with their stored bytes and encodings and convert the decodable ones,
expose document information and page count, signal encrypted and structurally invalid documents
rather than returning corrupt content, and build documents suitable for use as test fixtures.

### Verification Approach

**PdfPig is verified by transitive evidence from the `DocDown.Pdf` test suite.** This is stated
explicitly because it is a deliberate choice rather than an omission. Per the software-items
standard, a dedicated OTS test project is required only *if no other verification evidence is
available*. That is not the case here: unlike every other OTS item in this repository, PdfPig is not
a build-time tool invoked once per pipeline whose correct operation must be inferred from the
pipeline completing. It is a runtime library on the critical path of every `DocDown.Pdf` extraction,
and the extraction and unit tests named in the scenarios below — those in `PdfDocumentExtractorTests`,
`PdfTextExtractorTests`, `PdfImageExtractorTests`, and the `DocDownPdf_Extract_*` tests in
`DocDownPdfTests` — each parse a real PDF end to end, on three target frameworks, in every CI matrix
combination.

Two families of tests in the same project are deliberately **not** claimed as evidence that PdfPig
was exercised, because they execute none of its code. The `PdfDocDownBuilderExtensions_*` tests are
unrelated to PdfPig: they validate the builder registration seam only, constructing a builder,
calling `AddPdf()`, and inspecting the resulting descriptor. None of them reaches any parser code:
the registration surface exposes only the descriptor, and `PdfDocumentExtractor.ProbeAvailability()`
returns a constant availability value without opening a document. One of them asserts PdfPig's
*absence* from the public registration surface. `DocDownPdf_Package_BuildOutput_ContainsNoNativeAssets` is likewise an
assertion about how PdfPig is *packaged* — it inspects the built output for native assets — rather
than an exercise of the parser. Both families are valuable, and the packaging assertion is cited
below for exactly what it proves; neither is transitive evidence that the parser behaves as
required.

A dedicated `test/OtsSoftwareTests/` project would therefore re-test, against synthetic inputs, the
same library paths the extraction suite already exercises against the documents this package actually
has to handle — and it would do so at one remove from the behavior that matters, since what the
repository needs to know is not that the parser works in isolation but that it delivers what
`DocDown.Pdf` promises on top of it. The extraction suite answers that question directly. No such
project is created.

The mapping from each required feature to the tests that evidence it is given below and is recorded
in the requirement links themselves, so a reader can follow either direction.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix. Every fixture is a PDF
generated at test time, so the evidence depends on no committed binary and no network access.

### Test Scenarios

#### Managed parsing with no native binaries

**Tests**: `DocDownPdf_Package_BuildOutput_ContainsNoNativeAssets`,
`DocDownPdf_Extract_SimpleTextPdf_ProducesContractLayout`

The first asserts the package's build output contains no runtime-identifier folder and no unmanaged
binary, and that every library present is a managed assembly — so the claim rests on the built output
rather than on the dependency's documentation. The second proves the parser then actually parses a
document in that fully managed configuration, on every framework in the matrix. Evidence for
`DocDown-OTS-PdfPig`.

#### Glyph access with position information

**Tests**: `PdfTextExtractor_Extract_SingleTextPage_RendersParagraphsInReadingOrder`,
`PdfTextExtractor_Extract_AnyPage_ProducesStructureRawPageTextDoesNot`

Reading order can only be recovered from glyph geometry, so a rendering that comes out in reading
order and that demonstrably differs from the parser's own paint-order text is direct evidence that
the parser exposed the position information required. Evidence for
`DocDown-OTS-PdfPig-TextExtraction`.

#### Image enumeration, encoding, and conversion

**Tests**: `PdfImageExtractor_AddImage_DctImage_SetsPassthroughTransformHint`,
`PdfImageExtractor_AddImage_FlateImage_SetsDecodedToPngTransformHint`,
`PdfImageExtractor_AddImage_JpxImage_WritesJp2PassthroughWithoutExtraNote`,
`PdfImageExtractor_Extract_UndecodableEncoding_ReportsPlainNoteAndWritesNothingForIt`

Together these exercise all three things the parser must supply. The first proves the stored bytes of
a JPEG XObject are exposed and are exactly the embedded file. The second proves a compressed-sample
image is converted to a usable format. The third proves the same raw-byte access holds for an
encoding the parser cannot decode at all — a JPEG 2000 codestream is exposed intact, typed
`image/jp2`, and written as a passthrough, so the image counts as written and the run records no
note. The fourth proves an image the parser can neither decode nor expose as a file, a JBIG2 segment,
is enumerated rather than dropped: the run reports two images found and one written, and records a
plain note naming `JBIG2Decode` and the count that was not written. Evidence for
`DocDown-OTS-PdfPig-ImageExtraction`.

#### Document information and page count

**Tests**: `PdfDocumentExtractor_ExtractAsync_DocumentWithMetadata_ReportsTitleAuthorAndPageCount`,
`DocDownPdf_Extract_MultiPagePdf_ReportsPageCountAndAllText`

The document-metadata capability this package declares is satisfied entirely from what the parser
reads out of the document, so a title, author, and page count that match what the fixture declared is
direct evidence the parser exposed them. Evidence for `DocDown-OTS-PdfPig-DocumentMetadata`.

#### Protected and invalid documents are signalled

**Tests**: `PdfDocumentExtractor_ExtractAsync_EncryptedDocument_PropagatesParserFaultForCore`,
`PdfDocumentExtractor_ExtractAsync_MalformedDocument_PropagatesParserFaultForCore`

Proves the parser raises a clear, explained signal for a document behind a security handler it does
not implement and for a document with no cross-reference table, rather than returning whatever could
be scraped from them. A parser that returned partial content would make honest reporting impossible,
because this package would report a successful extraction of content the document does not contain.
Evidence for `DocDown-OTS-PdfPig-ProtectedDocuments`.

#### Document creation for fixtures

**Tests**: `DocDownPdf_Extract_SimpleTextPdf_ProducesContractLayout`,
`DocDownPdf_Extract_DctImage_RoundTripsByteIdenticalAsPassthrough`,
`PdfDocumentExtractor_GetSelfTestCases_DeployedBackend_ReturnsParseAndRenderingCases`

Every one of these depends on the parser's writer having produced a document the parser can then read
back — the third most directly, since the self-test case reads the embedded probe document and
checks its glyphs. The second additionally proves the writer embeds a JPEG verbatim, which is what
makes the byte-identity assertion meaningful. Evidence for `DocDown-OTS-PdfPig-DocumentCreation`.
