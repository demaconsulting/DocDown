## WordRevisionScanner

![DemaConsulting.DocDown.Word Structure](WordView.svg)

### Purpose

`WordRevisionScanner` finds every tracked change (revision) in a Word document. Its single
responsibility is detection and description: for each change it records the kind, the author, the
date, a human-readable location, and a short snippet of the affected text. It never renders anything;
`WordOpenXmlReader` decides how the changes are shown, and `WordContentEmitter` reports them.

The scan is a separate pass because the reader needs to know whether the document has any tracked
changes before it walks the body, so that a document without them takes exactly the pre-existing
rendering path.

### Data Model

`WordRevisionScanner` is an `internal static class` with no state. It returns an
`IReadOnlyList<WordRevision>`, where `WordRevision(Kind, Author, Date, Location, Snippet)` is a
record in the Markdown subsystem and `WordRevisionKind` distinguishes inserted, deleted, moved-from,
moved-to, inserted and deleted paragraph marks, formatting changes, and inserted and deleted table
rows. Two constants bound the work: `SnippetMaxLength` (200 characters) and the Wordprocessing
namespace used to read the author and date attributes.

### Key Methods

- **`static IReadOnlyList<WordRevision> Scan(MainDocumentPart, bool markup = true, WordTocInfo? toc = null)`**
  — walks
  the body, then every header and footer, then every footnote that is not a separator, in that order,
  and returns the revisions in document order. `markup` says whether deleted text is visible (the
  reader's mode), `toc` carries the paragraphs the reader omits, and `partLabels` (optional) the header
  and footer labels. A null part is rejected with
  `ArgumentNullException`.
- **`ScanContainer()`, `ScanParagraph()`, `ScanTable()`** (private) — a structure walk that mirrors the
  reader's block rules (a heading resets paragraph and table ordinals; a deleted paragraph mark merges
  into the next paragraph; content controls are entered) so the location agrees with `content.md`.
- **`Compose()`** (private) — builds the location `Label: path, position — "context"`, where the label
  is `Body`, the header or footer label `content.md` shows (`Header`, `Footer (2)`), or `Footnote [^n]`
  (the marker number in
  `content.md`), the path is the heading path joined by ` > ` or `before the first heading`, and the
  position is `paragraph N`, `table T, row R, column C`, `(heading)` for a heading, `after paragraph N`
  for an empty paragraph, or `table of contents (omitted from content.md), entry N`. Each revision
  also carries its `Part` and an `InToc` flag.
- **`Classify()`** and **`ClassifyLeaf()`** (private) — map an element to its kind: inserted,
  deleted, or moved runs; the inserted or deleted mark of a paragraph or row, told apart by the
  parent; inserted or deleted cells; and the property-change elements, which become formatting
  changes.
- **`BuildSnippet()`** (private) — the affected text, whitespace-collapsed and bounded to
  `SnippetMaxLength`. Like the location context text, it renders a `w:sym` through `WordSymbolMap`
  so snippets match `content.md`; in the accepted view a symbol inside a deleted run is omitted,
  as deleted text is.
- **`BuildLocation()`** (private) — superseded by `Compose()` above; the location is never a page number.
- **Known limitations** — ordinals for a paragraph merged into a following heading, and for images
  (not counted), can be off by one; a repeated footnote reference maps to its first marker. Table
  rows are numbered as rendered in `content.md` (deleted rows count in markup mode; in the accepted
  view they are omitted and a revision on one is located as `deleted row (omitted from the accepted
  view), after row N`).
- **Header and footer labels** — the reader passes `partLabels` (part URI to the label `content.md`
  shows, for example `Header`, `Footer (2)`), built by `BuildDocumentControl()` after deduplication, and
  re-scans so locations use them. A part not shown in `content.md` (empty, furniture only, or
  unreferenced) is labeled `Header (headerN.xml, not shown in content.md)`.
- **`ReadAttribute()`** and **`ParseDate()`** (private) — read the author and date as raw attributes,
  so an absent or malformed value degrades to `null` rather than failing the scan.

### Error Handling

An author or date the document omits, or a date that does not parse, yields `null` for that field;
the revision is still counted. Faults from a corrupt part propagate to Core, which renders them as
unreadable output.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — `MainDocumentPart`, the header, footer, and footnote parts, and
  the `Wordprocessing` namespace.
- **`WordRevision` and `WordRevisionKind`** — the record and enum this unit returns.

### Callers

`WordOpenXmlReader.Read()` calls `Scan()` once per document. Tests call it directly through
`InternalsVisibleTo`.
