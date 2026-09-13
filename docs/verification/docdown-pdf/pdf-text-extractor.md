## PdfTextExtractor Verification Design

This document describes the unit-level verification strategy for `PdfTextExtractor`, which renders a
PDF page's glyphs into markdown.

### Verification Approach

`PdfTextExtractor` is verified through unit tests in `PdfTextExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfTextExtractor_`.

The unit is driven directly against pages opened from generated fixtures, with extracted images
supplied as plain values. Nothing is mocked. The behavior under test is the actual layout-analysis
pipeline that recovers reading order, chooses paragraph structure, preserves page boundaries, and
places image links.

Assertions are property-based rather than golden. Reading-order recovery and segmentation are
heuristic, so the tests assert ordering, structure, and zero-count behavior rather than exact
markdown formatting.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: pages from PDFs generated at test time; no committed binaries and no network access
- **Filesystem**: none; the unit performs no I/O
- **Mocking**: none; the parser and layout-analysis pipeline are exercised as used in production
- **Isolation**: each test opens and disposes its own document

### Acceptance Criteria

Per IEC 62304 Section 5.5.2, a `PdfTextExtractor` unit test run passes when a page's text is emitted
in reading order; when the output is not PdfPig's raw page text; when every selected page produces
its boundary marker; when a declared document title heads the output; when image links appear under
the page they came from; when text-free or empty inputs yield zero heading and paragraph counts; and
when untagged PDFs do not acquire invented headings. Any raw-page-text output, misplaced image link,
or invented heading is a failure.

### Test Scenarios

#### A page's text is rendered in reading order

**Test**: `PdfTextExtractor_Extract_SingleTextPage_RendersParagraphsInReadingOrder`

Proves the upper line of a simple page precedes the lower line in the emitted markdown. Evidence for
`DocDownPdf-PdfTextExtractor-ReadingOrder`.

#### The output is not the parser's raw page text

**Test**: `PdfTextExtractor_Extract_AnyPage_ProducesStructureRawPageTextDoesNot`

Proves the extractor emits structure the raw page text does not provide. Evidence for
`DocDownPdf-PdfTextExtractor-AvoidsRawPageText`.

#### Every page contributes a boundary marker

**Test**: `PdfTextExtractor_Extract_MultiplePages_EmitsAPageMarkerPerPage`

Proves each selected page emits its own trace marker in page order. Evidence for
`DocDownPdf-PdfTextExtractor-MarksPageBoundaries`.

#### A declared document title heads the output

**Test**: `PdfTextExtractor_Extract_DocumentTitle_HeadsTheMarkdown`

Proves the content document is self-identifying when a title is supplied. This is a supporting
scenario with no linked requirement.

#### Image links land under their own page

**Test**: `PdfTextExtractor_Extract_ExtractedImages_LinksThemUnderTheirOwnPage`

Proves the extractor uses the sink-provided relative path and places the link after the correct page
marker. Evidence for `DocDownPdf-PdfTextExtractor-LinksImages`.

#### Text-free input yields zero text counts

**Test**: `PdfTextExtractor_Extract_PageWithNoGlyphs_YieldsZeroTextCounts`

Proves a page with no glyphs contributes no invented text and returns zero heading and paragraph
counts. Evidence for `DocDownPdf-PdfTextExtractor-ReportsZeroTextCounts`.

#### An untagged document gets no invented headings

**Test**: `PdfTextExtractor_Extract_UntaggedDocument_EmitsParagraphsWithoutInventedHeadings`

Proves the extractor emits plain paragraphs rather than guessing headings from appearance. Evidence
for `DocDownPdf-PdfTextExtractor-AvoidsRawPageText`.

#### An empty page list produces empty output and zero counts

**Test**: `PdfTextExtractor_Extract_NoPages_ProducesZeroCountsAndNoMarkers`

Proves the empty-page case returns empty markdown with zero heading and paragraph counts. Evidence
for `DocDownPdf-PdfTextExtractor-ReportsZeroTextCounts`.

#### A null page list is rejected

**Test**: `PdfTextExtractor_Extract_NullPages_ThrowsArgumentNullException`

Proves the page list is mandatory. This is a defensive test with no linked requirement.
