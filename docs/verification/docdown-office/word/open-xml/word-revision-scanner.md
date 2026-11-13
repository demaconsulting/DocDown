## WordRevisionScanner Verification Design

This document describes the unit-level verification strategy for `WordRevisionScanner`, the unit
that finds every tracked change in a Word document with its author, date, and location.

### Verification Approach

`WordRevisionScanner` is verified through unit tests in `OpenXml/WordTrackedChangesTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, with method names beginning with `WordRevisionScanner_`.

The unit is driven directly: each test opens a synthetic `.docx` built in code by
`TestData/DocxFixtures.cs` (invented text, authors, and dates) and calls `Scan()` on its main part.
The SDK is not mocked. Assertions are made on the returned records, so they name the scanner's
contract rather than a rendering outcome.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, net10.0, and net11.0
- **Inputs**: WordprocessingML documents generated at test time by `TestData/DocxFixtures.cs`
- **Filesystem**: none; each fixture is read from a `MemoryStream`
- **Mocking**: none
- **Isolation**: each test builds and opens its own document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordRevisionScanner` test run passes when insertions and deletions are
found with their author, date, and heading location; when a clean document yields no revisions;
when a formatting-only change is classified as formatting; when each moved element and each inserted
paragraph mark is counted; when header and footnote revisions report their own locations; and when
table-row revisions report row locations.

### Test Scenarios

#### Insertions and deletions are found with author, date, and location

**Test**: `WordRevisionScanner_Scan_RedlineDocument_FindsRevisionsWithAuthorDateAndLocation`

Proves the scan returns the inserted and deleted revisions with the authors and dates the fixture
declares and a location naming the heading they sit under. Evidence for
`DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### A clean document yields nothing

**Test**: `WordRevisionScanner_Scan_CleanDocument_FindsNothing`

Proves a document without tracked changes returns an empty list. Evidence for
`DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### A formatting change is counted as formatting

**Test**: `WordRevisionScanner_Scan_FormattingChange_IsCountedAsFormatting`

Proves a run-property change is returned with the formatting kind. Evidence for
`DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### Moves and inserted paragraph marks count each element

**Test**: `WordRevisionScanner_Scan_MoveAndInsertedMark_CountsEachElement`

Proves a move yields a moved-from and a moved-to revision and an inserted paragraph mark yields its
own revision. Evidence for `DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### Header and footnote revisions report their locations

**Test**: `WordRevisionScanner_Scan_HeaderAndFootnote_ReportsTheirLocations`

Proves revisions in a header and in a footnote are found and carry locations naming those parts.
Evidence for `DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### Table-row revisions report row locations

**Test**: `WordRevisionScanner_Scan_TableRows_ReportsRowLocations`

Proves inserted and deleted table rows are found and carry row locations. Evidence for
`DocDownWord-OpenXml-WordRevisionScanner-ScansRevisions`.

#### Every revision is located by part, heading path, and position

**Tests**: `WordRevisionScanner_Scan_LocatedRevisions_DescribesPartHeadingPathAndPosition`,
`WordRevisionScanner_Scan_DeletedHeading_FollowsTheRenderedMode`,
`WordRevisionScanner_Scan_DeletedRow_NumbersRowsAsRendered`,
`WordRevisionScanner_Scan_TwoFooters_UsesContentMarkdownLabels`,
`WordRevisionScanner_Scan_WithoutLabels_NamesPartFile`

Proves exact locations for a paragraph before the first heading, a nested heading path, a table cell,
a deleted heading (markup and accepted views), a formatting-only change, a footer, and a footnote;
that table row numbers match the rendered table in both modes (a deleted row counts in markup mode and
is omitted in the accepted view); and that header and footer locations use the labels `content.md`
shows (`Footer (1)`, `Footer (2)`).
Evidence for `DocDownWord-OpenXml-WordRevisionScanner-LocatesRevisions`.
