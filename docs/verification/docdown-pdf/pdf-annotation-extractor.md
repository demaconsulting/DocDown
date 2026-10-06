## PdfAnnotationExtractor Verification Design

This document describes the unit-level verification strategy for `PdfAnnotationExtractor`, which
reads the reviewer commentary a PDF carries as annotations and returns each remark's author, text,
and page.

### Verification Approach

`PdfAnnotationExtractor` is verified through unit tests in `PdfAnnotationExtractorTests.cs` in
`DemaConsulting.DocDown.Pdf.Tests`, with method names beginning with `PdfAnnotationExtractor_`.

The unit is driven directly against pages opened from generated fixtures, so the inclusion decision
is inspected where it is made rather than inferred from the rendered `review-comments.md`. The
inclusion rule has two independent halves — the annotation-type list and the requirement that an
annotation carry text — and neither subsumes the other, so the scenarios isolate them: a `Link` that
carries text is excluded by type alone, and a `Highlight` with no text is excluded by content alone
while a `Highlight` with text is kept.

The parser is not mocked. The behavior under test depends on real annotation dictionaries and the
real token types PdfPig produces for a PDF string, so the tests exercise the actual parser output
the production code consumes. That matters most for the author lookup, where accepting both a
literal and a hexadecimal string is only meaningfully proved against tokens the parser really built.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: pages from PDFs generated at test time by `PdfFixtures`, including a two-page
  annotated document and a document whose annotation reference is damaged
- **Filesystem**: none; the fixtures are assembled in memory and the sink records in memory
- **Mocking**: the sink is a test double; the parser is real
- **Isolation**: each test opens its own document and constructs its own sink

The annotated fixtures are assembled byte by byte rather than written by PdfPig's document builder,
because that builder cannot attach annotations to a page. No binary PDF is committed, so the exact
content of every annotation is readable in the same file the assertions live beside.

### Acceptance Criteria

Per IEC 62304 Section 5.5.2, a `PdfAnnotationExtractor` unit test run passes when a commentary
annotation's text, author, and 1-based page all reach the result; when an author recorded as a
hexadecimal string is decoded to the same text a literal string would give; when an annotation that
records no author yields no author rather than a placeholder; when an annotation whose type is
outside the documented commentary list is excluded even though it carries text; when an annotation
inside that list is excluded if its text is empty and included if it is not; when comments arrive in
page order; when a document carrying no annotations yields neither comments nor a note; when damage
to one page's annotation structures costs only that page's comments; and when null arguments are
rejected. Any fabricated author, any comment emitted for an excluded type, any empty comment, and
any misattributed page is a failure.

### Test Scenarios

#### A commentary annotation's text, author, and page are reported

**Test**: `PdfAnnotationExtractor_Extract_NamedAnnotation_ReportsBodyAuthorAndPage`

Proves a named sticky note arrives with its remark verbatim, attributed to the person who wrote it
and the page it sits on. Evidence for `DocDownPdf-PdfAnnotationExtractor-ExtractsAnnotationText`,
`DocDownPdf-PdfAnnotationExtractor-ResolvesAuthor`, and
`DocDownPdf-PdfAnnotationExtractor-ReportsPageNumber`.

#### A padded body is reported verbatim rather than trimmed

**Test**: `PdfAnnotationExtractor_Extract_PaddedBody_ReportsBodyVerbatim`

Proves a remark's leading and trailing spaces survive into the result unchanged: the blankness check
rejects a whitespace-only body without trimming a body that is merely padded, so `manifest.json`
carries the text exactly as the document records it, the same promise every other backend keeps.
Evidence for `DocDownPdf-PdfAnnotationExtractor-ExtractsAnnotationText`.

#### An annotation with no author yields no author

**Test**: `PdfAnnotationExtractor_Extract_AnnotationWithoutTitleEntry_ReportsNullAuthor`

Proves an annotation carrying no `/T` entry keeps its remark but invents no name for its writer.
Evidence for `DocDownPdf-PdfAnnotationExtractor-ReportsAbsentAuthorAsUnattributed`.

#### A hexadecimal author string is decoded

**Test**: `PdfAnnotationExtractor_Extract_HexadecimalAuthorString_ReportsDecodedAuthor`

Proves an author stored as a hexadecimal string is read as text rather than rendered as its digits
or dropped. Evidence for `DocDownPdf-PdfAnnotationExtractor-ResolvesAuthor`.

#### A non-commentary annotation is excluded despite carrying text

**Test**: `PdfAnnotationExtractor_Extract_LinkAnnotationWithContent_IsExcludedByType`

Proves a `Link` whose text would pass the content rule is still excluded, so its absence can only be
explained by the type decision. Evidence for
`DocDownPdf-PdfAnnotationExtractor-AppliesCommentaryTypeList`.

#### A markup annotation is excluded when blank and included when it carries a remark

**Tests**: `PdfAnnotationExtractor_Extract_HighlightWithoutContent_IsExcluded`,
`PdfAnnotationExtractor_Extract_HighlightWithContent_IsIncluded`

Prove the content rule, not the type, decides between two annotations of the same included type.
Evidence for `DocDownPdf-PdfAnnotationExtractor-SkipsEmptyAnnotations` and
`DocDownPdf-PdfAnnotationExtractor-AppliesCommentaryTypeList`.

#### An empty popup does not duplicate its parent's remark

**Test**: `PdfAnnotationExtractor_Extract_EmptyPopup_IsExcluded`

Proves a popup window carrying no text of its own contributes no comment, which is what keeps a
reader's remark from appearing twice. Evidence for
`DocDownPdf-PdfAnnotationExtractor-SkipsEmptyAnnotations`.

#### A popup repeating its parent's text produces both entries

**Test**: `PdfAnnotationExtractor_Extract_PopupDuplicatingParent_ReportsBothEntries`

Characterizes the stated limit of the blankness rule: a `Popup` whose `/Contents` repeats its
parent's text is not blank, so both entries are reported and the remark appears twice. The rule
removes the common duplicate — producers overwhelmingly leave the popup's text empty — and does not
attempt to recognize a repeated one, because comparing bodies across annotations would silently drop
a reviewer's genuine second remark that happened to say the same thing. Pinning the behavior here
makes the boundary a decided one rather than an untested corner, and makes any future change to it
visible. Evidence for `DocDownPdf-PdfAnnotationExtractor-SkipsEmptyAnnotations`.

#### Comments are attributed to the right page

**Test**: `PdfAnnotationExtractor_Extract_MultiPageDocument_AttributesCommentsToTheirPages`

Proves the second page's remark is reported against page two and no other. Evidence for
`DocDownPdf-PdfAnnotationExtractor-ReportsPageNumber`.

#### Comments arrive in document order

**Test**: `PdfAnnotationExtractor_Extract_MultiPageDocument_PreservesDocumentOrder`

Proves page numbers never decrease as the results are walked, so the review artifact reads as a
journey through the document. Evidence for
`DocDownPdf-PdfAnnotationExtractor-PreservesDocumentOrder`.

#### A document with no annotations reports nothing at all

**Tests**: `PdfAnnotationExtractor_Extract_DocumentWithoutAnnotations_ReportsNothing`,
`PdfAnnotationExtractor_Extract_ReadableAnnotations_RaisesNoNote`

Prove an uncommented document yields no comments and, crucially, no note, because an ordinary
absence is not an incomplete step. Evidence for
`DocDownPdf-PdfAnnotationExtractor-ReportsNothingWhenAbsent`.

#### Damage to one page's annotations costs only that page

**Test**: `PdfAnnotationExtractor_Extract_DamagedAnnotationReference_KeepsOtherPagesComments`

Proves a page whose `/Annots` entry points at a non-existent object does not cost the reader the
comments on other pages, nor the document's text. The same guard also covers the conversion step
(judging each annotation's type, content, and author) rather than only the enumeration of a page's
annotations, so a fault raised while converting one annotation is contained exactly like a fault
raised while reading the page's annotation list. Evidence for
`DocDownPdf-PdfAnnotationExtractor-ContainsUnreadableAnnotations`.

**Coverage limitation, stated plainly.** This scenario verifies the containment half of that
requirement but not the note itself. PdfPig's lenient parser absorbs every malformed annotation shape
that could be assembled here and simply reports no annotations for the page, so the guarded path
that raises the note was not reachable from a constructible fixture. The note-raising code is
therefore verified by inspection rather than by execution. It is retained rather than removed because
its absence would turn a future parser fault into a silently incomplete review artifact, which is the
one outcome the design rules out.

#### Null arguments are rejected

**Tests**: `PdfAnnotationExtractor_Extract_NullPages_ThrowsArgumentNullException`,
`PdfAnnotationExtractor_Extract_NullSink_ThrowsArgumentNullException`

Prove a missing page list or sink is refused at the boundary rather than reported as an empty
result. These are defensive tests with no linked requirement.
