## PdfAnnotationExtractor

![DemaConsulting.DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfAnnotationExtractor` reads the reviewer commentary a PDF carries as annotations. Its single
responsibility is to decide which annotations are remarks about the document and to return each
remark's author, text, and page; it writes nothing and renders nothing.

The separation is the point. A reviewer's remark is not part of the document — it is commentary
*about* the document — so it travels to Core through the review-comment channel and reaches
`review-comments.md`, never `content.md`. A reader who cannot tell the two apart can trust neither.

### Data Model

`PdfAnnotationExtractor` is an `internal static class`. It holds no mutable shared state; the
collected results live for the duration of one call, so the unit is safe for concurrent use.

- **`CommentaryAnnotationTypes`** (private `static readonly IReadOnlySet<AnnotationType>`) - the
  annotation types whose text counts as a reviewer's remark. See *Annotation-type inclusion* below.
- **`MaxNamedPages`** (private `const int`) - bounds how many page numbers the unreadable-annotations
  note names; the count it states is exact even when the list is truncated.
- **`PdfReviewAnnotation`** (`internal sealed record`) - one remark: its 1-based `PageNumber`, its
  `Author` (null when the document names none), and its `Body` (verbatim as the document records it,
  and never blank).

`PdfReviewAnnotation` carries the page number rather than a formatted location string because how a
comment's position is phrased is a presentation decision shared across formats, and so belongs to the
caller rather than to this unit.

### Key Methods

- **`static IReadOnlyList<PdfReviewAnnotation> Extract(IReadOnlyList<Page> pages, IExtractionSink
  sink, CancellationToken)`** - walks the selected pages in order, reads each page's annotations,
  keeps those that are reviewer commentary, and returns them in page order and, within a page, in
  the order the document lists its annotations. Document order is preserved because
  `review-comments.md` renders comments as reported and a reader scanning it expects to travel
  through the document rather than to jump about it. Preconditions: all arguments non-null.
- **`TryReadPageComments`** (private) - reads and converts one page's annotations inside a single
  guard, reporting failure as data rather than as an exception. PdfPig enumerates annotations
  lazily, and so does the conversion step, so a malformed annotation dictionary can surface either
  while the sequence is walked or while judging and converting one annotation's type, content, or
  `/T` entry; materializing both inside the same guard is what makes a failure attributable to a
  single page rather than escaping past it.
- **`ToReviewComment`** (private) - judges and converts in one step, so a returned comment's body is
  non-blank by construction. The body is checked for blankness but never trimmed: `manifest.json`
  promises the text as the document records it, the same promise every other backend keeps.
- **`ReadAuthor`** (private) - resolves the author from the annotation dictionary. See *Author
  resolution* below.
- **`ReportUnreadableAnnotationsNote`** (private) - emits one plain note naming the pages whose
  annotations could not be read.

### Annotation-type inclusion

Which annotation types count as reviewer commentary is an explicit decision, not a default. A PDF
annotation is any interactive object layered over a page, and most annotation types carry no human
remark at all. The included set is therefore stated as a table in code and reproduced here.

- **Purpose-built commentary** — `Text`, `FreeText`, `Popup`. A sticky note, an on-page text box,
  and a comment window exist only to carry words a person wrote.
- **Text markup** — `Highlight`, `Underline`, `Squiggly`, `StrikeOut`. The mark itself is not a
  comment, but reviewers routinely attach a remark to one.
- **Drawn and attached markup** — `Line`, `Square`, `Circle`, `Polygon`, `PolyLine`, `Stamp`,
  `Caret`, `Ink`, `FileAttachment`. A "Please revise" typed into a stamp or a callout is a review
  comment however it was drawn.

Deliberately excluded, whatever they contain: `Link`, `Widget`, `Screen`, `Sound`, `Movie`,
`PrinterMark`, `TrapNet`, `Watermark`, `Artwork3D`, and `Other`. A `Link` is navigation, a `Widget`
is a form control, and the production marks are machinery; none is a remark about the document, so
text found on one is not commentary. Treating every annotation as a comment would fill
`review-comments.md` with machinery and bury the remarks a reviewer actually wrote.

The list names what is *included* rather than what is excluded on purpose: a new or unrecognized
annotation type is then kept out by default instead of being admitted by accident. The included set
is the PDF markup annotations defined by ISO 32000-1 Section 12.5.6.2 — the annotations a PDF reader
itself shows in its comments pane — together with `Popup`, which is where a reader stores a remark's
text.

**Blank-content rule.** Within the included set, an annotation is kept only when its `/Contents`
entry carries text. That single rule is what makes the markup types behave sensibly: a highlight
drawn with no remark attached is a reading aid rather than a comment, and a `Popup` with no text of
its own is merely the window that displays its parent's remark, so emitting either would invent a
comment nobody wrote — and, in the popup's case, duplicate one that was. A highlight or popup that
*does* carry text is a genuine remark and is kept.

**Stated limitation of that rule.** A producer that copies a parent annotation's `/Contents` into
the `Popup` that displays it yields two entries for one remark. Deduplicating would mean resolving
each popup's `/Parent` through an indirect reference, which this unit does not do, and matching on
text would silently drop a reviewer who genuinely wrote the same words twice. Duplicating is the
safer error, because nothing a reviewer wrote is lost, so the behavior is characterized by a test
(`PdfAnnotationExtractor_Extract_PopupDuplicatingParent_ReportsBothEntries`) and stated here rather
than changed.

### Author resolution

PdfPig's `Annotation` exposes no author member, so the author is read from the annotation
dictionary's PDF title entry (`/T`), which is where a reader stores the commenter's name. A PDF may
hold a string as either a literal string or a hexadecimal string and both are legal here, so both are
accepted; any other token shape is a malformed value and yields no author rather than a rendering of
whatever it was.

An annotation that names no author yields `null`, never a placeholder. "The document did not say who
wrote this" and "someone called Unknown wrote this" are different claims and only the first is true;
Core renders an unattributed comment as such, and fabricating a name here would put an assertion in
the output that the document never made.

### Error Handling

Null arguments are rejected with `ArgumentNullException` as caller errors. Cancellation is observed
per page and propagates.

Annotation reading and conversion are contained per page. A page whose annotations defeat the parser,
or whose annotation judging/conversion faults on a malformed dictionary, is counted and named in a
plain `ExtractionNote`, and the walk continues; it never aborts an extraction whose text and images
were perfectly readable, because losing a whole document on account of one damaged comment would cost
far more than the comment was worth. Containing the loss silently would be worse than failing, so the
note is what keeps the loss visible.

Deliberate exclusions raise no note. Notes are reserved for steps that were attempted and could not
complete; a `Link` that is not a comment and a document that carries no annotations are both ordinary
facts about the document, not shortfalls.

### Dependencies

- **PdfPig** (OTS) - `Page.GetAnnotations()`, `Annotation.Type`, `Annotation.Content`,
  `Annotation.AnnotationDictionary`, `DictionaryToken.TryGet`, `NameToken.T`, `StringToken.Data`,
  and `HexToken.Data`.
- **DemaConsulting.DocDown.Core** - `IExtractionSink` and `ExtractionNote`.

### Callers

`PdfDocumentExtractor`, after the page text has been written. It formats each remark's location as
`Page {n}` and reports it through `IExtractionSink.ReportReviewComment`. See *PdfDocumentExtractor
Design*.
