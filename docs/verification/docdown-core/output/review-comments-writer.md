### ReviewCommentsWriter Verification Design

This document describes the unit-level verification strategy for `ReviewCommentsWriter`, which
finalizes `review-comments.md` — the one output artifact whose presence varies with the document —
from the reviewer comments the sink recorded during extraction.

#### Verification Approach

`ReviewCommentsWriter` is verified in isolation through unit tests in `ReviewCommentsWriterTests.cs`
under the `Output` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ReviewCommentsWriter_`.

Nothing is mocked. The writer's documented source of comments is `ExtractionSink`, so tests drive a
**real** `ExtractionSink` over a real `ScratchFolder` prepared on a per-test `TempScratch` folder:
the test reports comments through the sink, calls `ReviewCommentsWriter.WriteAsync`, and reconciles
the returned result against what is actually on disk. Using the real sink rather than a stub is
correct because the writer's contract is precisely to turn what the sink recorded into the final
on-disk artifact; a mock sink would not evidence that the entries and the file match.

The conditional cases are verified in both directions. Absence is proved by asserting the file does
**not** exist, not merely that the result reports no path, because a claimed-empty result alongside a
stray file on disk would be exactly the dishonesty the conditional artifact exists to avoid.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder backs the real `ScratchFolder` and `ExtractionSink`
- **Mocking**: none; the real sink supplies the recorded comments
- **Isolation**: each test owns its scratch folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `ReviewCommentsWriter` unit test run passes when a document with no reviewer
comments produces no file and a result claiming none; when a document with comments produces a file
carrying every comment's author, location, and body; when entries appear in the order the extractor
reported them; when a comment the document leaves unattributed is labelled as unattributed; and when
a multi-line body stays within its single entry. A written-but-empty file, a claimed path with no
file behind it, a re-sorted or dropped comment, an invented author, or a body that escapes its entry
is a failure.

#### Test Scenarios

##### No comments produce no file

**Test**: `ReviewCommentsWriter_WriteAsync_NoComments_WritesNoFileAndReportsNone`

Proves a document the sink recorded no comments for produces no artifact, a null path, and a count of
zero, so the absence of a review is told by the absence of the file rather than by an empty file
asserting an empty review. Evidence for
`DocDownCore-Output-ReviewCommentsWriter-ConditionalArtifact`.

##### A single comment produces the document

**Test**: `ReviewCommentsWriter_WriteAsync_SingleComment_WritesDocumentWithEntry`

Proves one recorded comment produces a headed `review-comments.md` whose entry carries the author,
the backend-composed location phrase, and the body verbatim, and that the returned result claims the
path and counts the comment. Evidence for
`DocDownCore-Output-ReviewCommentsWriter-ConditionalArtifact` and
`DocDownCore-Output-ReviewCommentsWriter-EntryFormat`.

##### Comments are written in report order

**Test**: `ReviewCommentsWriter_WriteAsync_ManyComments_WritesEntriesInReportOrder`

Proves three comments reported in a deliberately non-alphabetical order appear in that same order,
demonstrating the writer carries the backend's document ordering rather than imposing one of its own.
Evidence for `DocDownCore-Output-ReviewCommentsWriter-ReportOrder`.

##### An unattributed comment is reported as unattributed

**Test**: `ReviewCommentsWriter_WriteAsync_CommentWithoutAuthor_ReportsUnattributed`

Proves a comment the document records no author for is labelled `Unattributed` rather than being
dropped or attributed to a guessed name. Evidence for
`DocDownCore-Output-ReviewCommentsWriter-UnattributedReported`.

##### A multi-line body stays in its entry

**Test**: `ReviewCommentsWriter_WriteAsync_MultiLineBody_KeepsEntryOnOneLine`

Proves a body spanning lines in both line-ending conventions is flattened onto one entry with all its
words intact, so the comment cannot be misread as document text and the file stays one entry per line.
Evidence for `DocDownCore-Output-ReviewCommentsWriter-EntryFormat`.

##### A padded body survives flattening unchanged

**Test**: `ReviewCommentsWriter_WriteAsync_PaddedBody_PreservesLeadingAndTrailingSpaces`

Proves flattening a body's line breaks does not also trim its leading or trailing spaces: a backend's
verbatim-body contract (such as a PDF annotation's padded `/Contents` entry) is only honored
end-to-end if this writer does not quietly take back whitespace the extractor deliberately preserved.
Evidence for `DocDownCore-Output-ReviewCommentsWriter-EntryFormat`.

##### Markdown characters are escaped in the file but not in the record

**Test**: `ReviewCommentsWriter_WriteAsync_MarkdownCharacters_EscapedInFileButNotInRecord`

Proves a comment whose author, location, and body all carry markdown-significant characters is
written with those characters escaped, while what the extractor reported — and therefore what
`manifest.json` carries — stays exactly as the document recorded it. Both halves are asserted in one
test because the two artifacts make different promises and a future change must not satisfy one by
breaking the other. The case covers `&` and `~` alongside the structural characters: neither
restructures the entry, but an unescaped `&amp;` would render as a bare `&` and an unescaped
`~~old~~` struck through, either of which silently alters a reviewer's words. Evidence for
`DocDownCore-Output-ReviewCommentsWriter-EscapesMarkdown`.

##### Line-structural characters are left alone

**Test**: `ReviewCommentsWriter_WriteAsync_StructuralCharacters_AreNotEscaped`

Proves ordinary text containing characters that are structural only at the start of a line is
written through unchanged, with no backslash anywhere in the file. This pins the escape set to the
inline-structural one: escaping a trailing `.` is what once made a comment read as
`Please clarify the scope\.` in the shipped output. Evidence for
`DocDownCore-Output-ReviewCommentsWriter-EscapesMarkdown`.

##### A null sink is rejected

**Test**: `ReviewCommentsWriter_WriteAsync_NullSink_Throws`

Proves a null sink is rejected at entry with the documented exception rather than producing a partial
artifact. This is a defensive test with no linked requirement.
