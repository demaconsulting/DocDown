## WordOpenXmlReader

![DocDown.Word Structure](WordView.svg)

### Purpose

`WordOpenXmlReader` reads a Word Open XML document into the backend-neutral `WordDocumentModel`.
Its single responsibility is the SDK-to-model translation: headings from styles and outline level,
ordered and bulleted lists from `numbering.xml`, genuine tables from `w:tbl`, images from blips,
the accepted view of tracked changes, comments and footnotes, and the `## Document Control`
section built once from headers and footers with page-numbering furniture detected structurally by
field instruction rather than by pattern-matching rendered text.

### Data Model

`WordOpenXmlReader` is an `internal sealed class` holding per-read state, so a fresh instance is
used per document. The important state is:

- **`_numbering`** — a map from numbering id to a level-to-ordered-flag map, built once at the
  start of `Read()` from `numbering.xml`.
- **`_footnotesById`** and **`_footnoteBodies`** — the indexed footnote definitions and the
  referenced-in-body-order accumulator used to build the rendered footnote section.
- **`_trackedChanges`** — the count of inserted and deleted tracked-change elements the walk
  consumed while producing the accepted view.
- **`_emptyTables`** — the count of authored tables that produced no visible cell content.
- **`_currentHeading`** — the nearest preceding heading text, used as a low-confidence image naming
  hint when the document offered no descriptive text, and as the section half of a comment's
  location hint.
- **`_commentAnchors`** and **`_openCommentRanges`** — the per-read anchor table keyed by comment
  id, and the stack of comment ranges currently bracketing run text.
- **`SnippetMaxLength`** — the 60-character bound on a comment's quoted snippet, which keeps the
  location hint a pointer into the document rather than a second copy of it.
- **`OleCompoundFileSignature`** — the eight bytes `D0 CF 11 E0 A1 B1 1A E1` that front an OLE
  compound file. The reader checks these up front so it can raise a plain `WordExtractionException`
  for password-protected `.docx` containers.
- **`FurnitureFieldInstructions`** — the case-insensitive set `PAGE`, `NUMPAGES`,
  `SECTIONPAGES`, `SECTIONPAGESNUM`, matched against the first token of a field instruction so page
  furniture is recognized structurally.

### Key Methods

- **`WordDocumentModel Read(Stream docx)`** — validates the stream, checks the OLE signature, opens
  the package read-only through `WordprocessingDocument.Open()`, rebuilds numbering and footnote
  indices, walks every top-level body element via `AppendBodyElement()`, then builds document
  control and comments. Returns a model carrying the body, document-control content, comments with
  their resolved location hints,
  footnotes, title and author from `docProps/core.xml`, producer page count from `docProps/app.xml`,
  metadata, and the counts observed during the walk.
- **`AppendBodyElement()`** (private) — dispatches on the body element. A paragraph becomes a
  heading, list item, paragraph, image, and-or page break through `AppendParagraph()`; a table
  becomes a `Table` block through `BuildTable()`, or increments `_emptyTables` when no visible
  content survived.
- **`AppendParagraph()`** and **`WalkContainer()`** (private) — the recursive workhorses. They
  collect styled text, hyperlinks, tracked changes, footnote references, page breaks, field
  results, and drawings while preserving the accepted view of tracked changes. They also track
  comment anchors: `w:commentRangeStart` and `w:commentRangeEnd` open and close a bracketed range
  by `w:id`, and a `w:commentReference` inside a run marks a point anchor for its id. While a range
  is open, run text is appended to that comment's anchor snippet.
- **`OpenCommentRange()`**, **`CloseCommentRange()`**, **`NoteCommentAnchor()`**, and
  **`AppendAnchoredText()`** (private) — maintain the anchor table keyed by comment id. Each entry
  records the value of `_currentHeading` at the moment the anchor was first seen, so a later
  re-anchoring cannot overwrite the section a comment was attached to, and accumulates anchored run
  text up to one character past the snippet bound so truncation stays detectable.
- **`FormatCommentLocation()`** and the nested **`CommentAnchor`** type (private) — turn an anchor
  into the display hint. The snippet collapses whitespace runs to single spaces and is ellipsized
  at 60 characters. The hint is `§{Heading} — "{Snippet}"` when both parts are available, degrades
  to `§{Heading}` or `"{Snippet}"` when only one is, and is `null` when the document anchored the
  comment nowhere.
- **`BuildComments()`** (private) — resolves every comment's location hint from the anchor table
  *before* rendering any comment body, because rendering a body walks it through the same container
  walk and any markers it contains must not pollute a later comment's anchor. Each `w:comment` is
  then paired with its author, rendered inlines, and resolved location. A comment whose inlines
  reduce to no text is **dropped**, because it says nothing — the same rule the Excel, PowerPoint,
  and PDF readers apply, so the model-level invariant "no comment without text" holds for every
  format. A schema-valid `w:comment` carrying no runs arises in practice from a reviewer inserting a
  comment without typing and from a comment whose runs are all tracked deletions, and admitting one
  would both attribute an empty remark to a named person and, because Core rejects a blank comment
  body by contract, abort the entire extraction. A dropped comment whose only content was a picture
  or ink is counted in `CommentsWithUnreadableContent` rather than passing silently, because the
  reviewer did leave a remark this backend cannot render; a wholly empty comment is not counted,
  because nothing was lost and a note would assert a shortfall that did not occur.
- **`CollectImage()`**, **`GatherImageTextCandidates()`**, and **`FindAdjacentCaption()`**
  (private) — choose each image's naming and description text. They prefer authored descriptions
  and titles, then caption text, then object names, then the nearest heading, and they
  deliberately ignore the auto-generated drawing name.
- **`BuildTable()`** (private) — walks rows, records whether Word marked the first row a header,
  preserves alignment by emitting empty continuation cells for merges, and counts merged and nested
  cells so the emitter can later state when markdown could not preserve the full table structure.
- **`BuildCellContent()`** and **`FlattenNestedTable()`** (private) — flatten nested table content
  into `<br>`-joined text so the outer grid remains renderable.
- **`BuildDocumentControl()`** (private) — collects header and footer parts from every `w:sectPr`,
  renders each with page furniture stripped, deduplicates identical rendered content across
  sections, and labels the surviving sections for markdown rendering.
- **`IsFurnitureInstruction()`** (private) — trims the instruction, splits on whitespace, and
  matches the first token against the furniture set. This is the authoritative signal for
  page-numbering furniture.
- **`ThrowIfPasswordProtected()`** (private) — reads eight bytes, rewinds, and raises
  `WordExtractionException` when they equal the OLE signature.

### Error Handling

A null stream is rejected with `ArgumentNullException`. A password-protected `.docx` surfaces as
`WordExtractionException` with a clear message. A missing `MainDocumentPart` surfaces as
`WordExtractionException` for the same reason. Other faults propagate to Core, which renders them
as unreadable output. The reader translates only what it can recognize better than the SDK; it
never swallows adverse conditions and never rewrites the document's content to hide them.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — `WordprocessingDocument`, `MainDocumentPart`, `HeaderPart`,
  `FooterPart`, `ImagePart`, the `Wordprocessing` namespace, and the drawing namespaces for blips
  and picture metadata. See the *DocumentFormat.OpenXml* OTS design.
- **Core image-text selection types** — the shared image-text policy the reader feeds through
  `WordOpenXmlImageReader`.
- **`WordOpenXmlImageReader`** — blip resolution and image-text selection.
- **`WordDocumentModel` and the Markdown subsystem record types** — the model this unit populates.
- **`WordExtractionException`** — the encrypted-document surface.

### Callers

`WordOpenXmlExtractor.ExtractAsync()` uses this unit for every extraction, and the same class's
parse round-trip self-test case uses it against an in-memory probe document. Tests construct
additional instances directly through `InternalsVisibleTo`.
