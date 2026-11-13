## WordTocDetector Verification Design

This document describes the unit-level verification strategy for `WordTocDetector`, the unit that
finds the paragraphs of a Word table of contents.

### Verification Approach

`WordTocDetector` is verified through tests in `OpenXml/WordTrackedChangesTests.cs` in
`DemaConsulting.DocDown.Office.Tests`. Each test reads a synthetic `.docx` built in code by
`TestData/DocxRevisionLocationFixtures.cs` (invented text) through `WordOpenXmlReader`, and asserts the
skipped-region and skipped-paragraph counts the detector produced and the content that remains.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, net10.0, and net11.0
- **Inputs**: WordprocessingML documents generated at test time
- **Filesystem**: none; each fixture is read from a `MemoryStream`
- **Mocking**: none
- **Isolation**: each test builds and opens its own document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordTocDetector` test run passes when a contents control with a `TOC` field
is found as one region with all its paragraphs, when contents-styled paragraphs are found while
an ordinary content control is left alone, and when a document without contents reports none.

### Test Scenarios

#### A tracked table of contents is omitted and counted

**Test**: `WordOpenXmlReader_Read_TrackedTableOfContents_IsOmittedButItsRevisionsAreCounted`

Proves one region of four paragraphs is found, none of its text reaches the rendered content, and its
two tracked changes are flagged and still counted. Evidence for
`DocDownWord-OpenXml-WordTocDetector-FindsTableOfContents`.

#### Style-only contents is omitted; an ordinary content control is kept

**Test**: `WordOpenXmlReader_Read_StyleOnlyTocAndContentControl_OmitsTocAndKeepsControlText`

Proves `TOC1`/`TOC2` paragraphs are omitted and a plain content control's text is still rendered.
Evidence for `DocDownWord-OpenXml-WordTocDetector-FindsTableOfContents`.

#### No contents, nothing skipped

**Test**: `WordOpenXmlReader_Read_DocumentWithoutToc_ReportsNoneSkipped`

Proves a document without a table of contents reports zero regions and paragraphs. Evidence for
`DocDownWord-OpenXml-WordTocDetector-FindsTableOfContents`.
