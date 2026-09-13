## PdfTextExtractor Verification Design

This document describes the unit-level verification strategy for `PdfTextExtractor`, which renders a
PDF page's glyphs into markdown.

### Verification Approach

`PdfTextExtractor` is verified through unit tests in `PdfTextExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfTextExtractor_`.

The unit is driven directly against pages opened from the generated fixtures, with the extracted
images supplied as plain values rather than produced by a real image extraction. Nothing is mocked:
the parser and its layout-analysis pipeline are real, because the behavior under test — recovering
reading order from positioned glyphs — is precisely the behavior a mock would have to fabricate.

Assertions are **property-based rather than golden**. Reading-order recovery, word grouping, and page
segmentation are heuristics, and a golden-markdown comparison would fail on any harmless change to
them while proving nothing extra about correctness. The scenarios therefore assert that expected text
is present, that known markers appear in the expected relative order, that structure exists which the
parser's raw page text does not have, and that links land under the right page.

The prohibition on the parser's raw page text is asserted rather than merely documented: one scenario
reads the raw text and requires the unit's output to differ from it while still containing the same
words.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: pages from PDFs generated at test time; no committed binaries, no network access
- **Filesystem**: none; the unit performs no input or output
- **Mocking**: none; the parser and its layout-analysis pipeline are exercised as they run in production
- **Isolation**: each test opens its own document and disposes it

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PdfTextExtractor` unit test run passes when a page's text is emitted in
reading order; when the output differs from the parser's raw page text while containing the same
words and carrying word separation the raw run lacks; when each page contributes its own boundary
marker, in order; when a declared document title heads the output; when each extracted image is
linked under the page it came from; when a page carrying no glyph is reported as such rather than
silently yielding nothing; when an untagged document yields paragraphs with no invented headings; and
when an empty page list yields empty output without an exception. Any invented heading, any link
under the wrong page, or any use of raw page text is a failure.

### Test Scenarios

#### A page's text is rendered in reading order

**Test**: `PdfTextExtractor_Extract_SingleTextPage_RendersParagraphsInReadingOrder`

Proves both lines of a two-line page are present and that the upper one precedes the lower, as a
reader would take them. Evidence for `DocDownPdf-PdfTextExtractor-ReadingOrder`.

#### The output is not the parser's raw page text

**Test**: `PdfTextExtractor_Extract_AnyPage_ProducesStructureRawPageTextDoesNot`

Proves the prohibition, rather than restating it. The raw page text is read from the same page and
the unit's output is required to differ from it and not to contain it, while still carrying the same
words and the word separation the raw run lacks. Emitting raw page text would be cheap and would look
like text while being wrong in a way a consumer cannot detect, which is why this is asserted at all.
Evidence for `DocDownPdf-PdfTextExtractor-AvoidsRawPageText`.

#### Every page contributes a boundary marker

**Test**: `PdfTextExtractor_Extract_MultiplePages_EmitsAPageMarkerPerPage`

Proves each of three pages emits its own marker and that the markers appear in page order, so any
passage in the output can be traced back to its source page. Evidence for
`DocDownPdf-PdfTextExtractor-MarksPageBoundaries`.

#### A declared document title heads the output

**Test**: `PdfTextExtractor_Extract_DocumentTitle_HeadsTheMarkdown`

Proves the content document identifies itself when the PDF declares a title. This is a supporting
scenario with no linked requirement of its own.

#### Image links land under their own page

**Test**: `PdfTextExtractor_Extract_ExtractedImages_LinksThemUnderTheirOwnPage`

Proves the link uses the path the sink allocated — not one constructed here — and that it appears
after its own page's marker rather than under another page. A link under the wrong page is worse than
no link, because it asserts a relationship that does not exist. Evidence for
`DocDownPdf-PdfTextExtractor-LinksImages`.

#### A page with no glyphs is reported as such

**Test**: `PdfTextExtractor_Extract_PageWithNoGlyphs_ReportsNoGlyphsPresent`

Proves a purely graphical page is signalled as carrying no glyphs, which is the fact the caller needs
to explain a scanned document rather than emit a silently empty result. Evidence for
`DocDownPdf-PdfTextExtractor-ReportsNoTextLayer`.

#### An untagged document gets no invented headings

**Test**: `PdfTextExtractor_Extract_UntaggedDocument_EmitsParagraphsWithoutInventedHeadings`

Proves the deliberate fallback. The fixture is first confirmed to carry no marked-content structure,
then the rendering is required to contain the text without promoting any of it to a heading. Guessing
headings from appearance would produce a confident structure the document never declared, which is
worse for a consumer than a flat one. Evidence for `DocDownPdf-PdfTextExtractor-AvoidsRawPageText`.

#### An empty page list produces empty output

**Test**: `PdfTextExtractor_Extract_NoPages_ProducesNoGlyphsAndNoMarkers`

Proves the page-less case — which a zero-page document produces — yields an empty result rather than
an exception. This is a boundary test with no linked requirement.

#### A null page list is rejected

**Test**: `PdfTextExtractor_Extract_NullPages_ThrowsArgumentNullException`

Proves the unit's only input is mandatory. This is a defensive test with no linked requirement.
