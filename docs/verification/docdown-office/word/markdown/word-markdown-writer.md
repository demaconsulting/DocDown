## WordMarkdownWriter Verification Design

This document describes the unit-level verification strategy for `WordMarkdownWriter`, the unit that
renders the backend-neutral document model to a single markdown flow.

### Verification Approach

`WordMarkdownWriter` is verified through unit tests in `Markdown/WordMarkdownWriterTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, with method names beginning with `WordMarkdownWriter_`.

The unit is driven against **hand-built `WordDocumentModel` instances with no document behind
them**, because the writer's contract is the projection from a model onto markdown; a document read
is the reader's job, and mixing the two would obscure which unit was responsible for a defect. Each
scenario supplies exactly the model shape it needs — a heading of a given level, a list item at a
given depth, a run flagged bold or italic, an image block whose path map holds the sink's chosen
target, a comment with an author, a footnote reference and its content, a Document Control section
carrying a subsection — so a failure names one block kind rather than one document.

Nothing is mocked: the image path map is a plain dictionary the sink would otherwise supply, and no
sink or engine is involved. This is the right level because the writer's inputs are already the
model the reader hands over, so a stand-in would only re-implement the record types the writer
consumes.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built models constructed from `WordBlock`, `WordInline`, `WordImageRef`,
  `WordComment`, and `WordDocumentControlSection` records
- **Filesystem**: none
- **Mocking**: none; the image path map is a plain dictionary
- **Isolation**: each test constructs its own model

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordMarkdownWriter` unit test run passes when a heading of level *n*
renders as *n* hash marks; when an ordered item and a bulleted item render with the correct markers
and the depth-appropriate indentation; when markdown metacharacters in text are escaped so they do
not render as unintended formatting; when bold, italic, and hyperlink runs render as their markdown
equivalents; when an image block resolves to a markdown image link whose target is the path the
sink allocated; when comments render into a Comments section attributed to the author; when
footnote references render inline and their content appears under a Footnotes section; and when the
Document Control section sits after the title heading and before the body. Any block kind that
renders as its underlying text, any escape omission, or a Document Control section placed elsewhere
is a failure.

### Test Scenarios

#### Headings render as the matching depth of hash marks

**Test**: `WordMarkdownWriter_Write_Headings_RendersHashLevels`

Proves a heading of level one renders as `# Top` and a heading of level two as `## Sub`, so the
outline the model declared survives in the content. Evidence for
`DocDownWord-Markdown-WordMarkdownWriter-RendersHeadings`.

#### Ordered and bulleted items render with markers and nesting

**Test**: `WordMarkdownWriter_Write_OrderedAndBulletedLists_RendersMarkers`

Proves a top-level bulleted item renders as `- Bullet` and a nested ordered item as
`1. Nested` — the two-space indent expressing the depth-one nesting and the `1.` expressing the
ordered kind. Evidence for `DocDownWord-Markdown-WordMarkdownWriter-RendersLists`.

#### Special characters are escaped

**Test**: `WordMarkdownWriter_Write_SpecialCharacters_AreEscaped`

Proves the run `a*b_c|d#e` renders as `a\*b\_c\|d\#e`, so literal text that happens to contain
markdown metacharacters does not surface as unintended formatting. Evidence for
`DocDownWord-Markdown-WordMarkdownWriter-EscapesSpecialCharacters`.

#### Bold, italic, and hyperlink runs render as inline markdown

**Test**: `WordMarkdownWriter_Write_BoldItalicAndLink_RenderInline`

Proves a bold run renders as `**bold**`, an italic run as `*italic*`, and a hyperlink run as
`[link](https://example.com)`, so emphasis and links carry meaning a plain-text flattening would
lose. Evidence for `DocDownWord-Markdown-WordMarkdownWriter-RendersInlineFormatting`.

#### An image block renders as a markdown link with descriptive alt text

**Test**: `WordMarkdownWriter_Write_DescriptiveImage_RendersDescriptionAsAltText`

Proves an image whose part path is `/word/media/image1.png` and whose sink-allocated path is
`images/0001-logo.png`, carrying the descriptive alt text `logo`, renders as
`![logo](images/0001-logo.png)`, so the content connects to the `images/` folder through the path the
sink chose and uses the document's own description as the alt text. Evidence for
`DocDownWord-Markdown-WordMarkdownWriter-RendersImageLink`.

#### An image with no description keeps neutral alt text

**Test**: `WordMarkdownWriter_Write_HeadingNamedImage_KeepsNeutralAltText`

Proves an image whose only text source is a nearby heading renders as `![image](...)`, never
`![references](...)`: the heading may have seeded the file name, but asserting it as alt text would imply
a description the document never gave, so the alt text stays the neutral `image`. Evidence for
`DocDownWord-Markdown-WordMarkdownWriter-NeutralAltForNonDescriptive`.

#### Comments render under a Comments section

**Test**: `WordMarkdownWriter_Write_Comments_RendersCommentsSection`

Proves a `## Comments` heading appears and the comment renders as `**Reviewer**: Please clarify`,
so a reviewer's remarks are preserved distinctly from the body they annotate. Evidence for
`DocDownWord-Markdown-WordMarkdownWriter-RendersComments`.

#### Footnotes render as references and a Footnotes section

**Test**: `WordMarkdownWriter_Write_Footnotes_RendersFootnotesSection`

Proves the reference `[^1]` appears inline and the corresponding `[^1]: The footnote text`
definition appears under a `## Footnotes` heading, so the anchor and its content stay connected.
Evidence for `DocDownWord-Markdown-WordMarkdownWriter-RendersFootnotes`.

#### Document Control sits between the title heading and the body

**Test**: `WordMarkdownWriter_Write_DocumentControl_PlacedAfterTitleHeading`

Proves the index of `# Title` precedes that of `## Document Control`, which precedes that of the
body paragraph, and that the subsection heading and its content — the escaped `Revision 3\.0` under
`### Header` — render. Ordering by index rather than by naive string search is what makes the
placement claim falsifiable against a writer that emitted the section in the wrong place. Evidence
for `DocDownWord-Markdown-WordMarkdownWriter-PlacesDocumentControl`.
