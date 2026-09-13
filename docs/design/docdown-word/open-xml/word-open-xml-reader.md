## WordOpenXmlReader

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordOpenXmlReader` reads a Word Open XML document into the backend-neutral `WordDocumentModel`.
Its single responsibility is the SDK-to-model translation: headings from styles and outline level,
ordered and bulleted lists from `numbering.xml`, genuine tables from `w:tbl`, images from blips,
the accepted-revisions view of tracked changes, comments and footnotes, and — the feature that
most distinguishes Word from PDF — the `## Document Control` section built once from headers and
footers with page-numbering furniture detected structurally by field instruction rather than by
pattern-matching rendered text.

### Data Model

`WordOpenXmlReader` is an `internal sealed class` holding per-read state, so a fresh instance is
used per document. The state:

- **`_numbering`** — the map from a numbering id to a level-to-ordered-flag map, built once at the
  start of `Read` from `mainPart.NumberingDefinitionsPart.Numbering`. Any format other than
  `bullet` and `none` is ordered; unknown numberings default to bulleted.
- **`_footnotesById`** — the footnote definitions indexed by their positive ids, built from
  `mainPart.FootnotesPart.Footnotes`. Separator and continuation footnotes carry non-positive ids
  and are excluded because they are not content.
- **`_footnoteBodies`** — the referenced-in-body-order accumulator, populated on each
  `AppendFootnote` and carried out on the model.
- **`_trackedChanges`** — the count of `w:ins` (kept, contributing text) and `w:del` (dropped,
  counted anyway) elements walked, reported on the model as `TrackedChangeCount`.
- **`_emptyTables`** — the count of empty `w:tbl` elements skipped, reported on the model as
  `EmptyTablesSkipped`.
- **`_currentHeading`** — the text of the nearest preceding body heading, updated as the walk passes
  each heading and read when a drawing gathers its image-text candidates. It supplies a drawing's
  section heading as a low-confidence naming hint; it is context, never asserted as a description.
- **`OleCompoundFileSignature`** (static readonly) — the eight bytes
  `D0 CF 11 E0 A1 B1 1A E1` that front an OLE compound file. The SDK exposes password protection
  only through a raw package error, so the reader detects the signature up front and turns it into
  a clear `WordExtractionException`.
- **`FurnitureFieldInstructions`** (static readonly) — the case-insensitive set `PAGE`, `NUMPAGES`,
  `SECTIONPAGES`, `SECTIONPAGESNUM`. A field's first token is matched against this set (see
  `IsFurnitureInstruction`), so `PAGEREF` is never misclassified and a revision string that
  happens to contain digits is never mistaken for page numbering.
- **`FieldFrame`** (private nested class) — one frame of the complex-field state stack: the
  accumulated `Instruction`, an `InResult` flag turned on at the field separator, and a
  `Furniture` flag decided when the separator arrives.

### Key Methods

- **`WordDocumentModel Read(Stream docx)`** — validates the stream, checks the OLE signature (see
  `ThrowIfPasswordProtected`), opens the package read-only through `WordprocessingDocument.Open`,
  rebuilds the numbering and footnote indices, walks every top-level body element via
  `AppendBodyElement`, then builds the document-control section and the comments. Returns a
  `WordDocumentModel` carrying the body, control sections, comments, footnotes, `Title`/`Author`
  from `docProps/core.xml`, `ProducerPageCount` from `docProps/app.xml <Pages>` when parseable,
  and every count observed during the walk. Preconditions: `docx` non-null.
- **`AppendBodyElement`** (private) — dispatches on the body element: a `W.Paragraph` becomes a
  heading, list item, paragraph, image, and/or page break through `AppendParagraph`; a `W.Table`
  becomes a `Table` block through `BuildTable`, or increments `_emptyTables` when the model comes
  back null.
- **`AppendParagraph`** (private) — walks the paragraph, then classifies: heading (from
  `DetermineHeadingLevel`) → list item (from `TryGetListInfo`) → paragraph. Every image collected
  in the walk is appended after the text block. A page break inside the paragraph appends a
  `PageBreak` block.
- **`WalkContainer`** (private) — the recursive workhorse. `W.InsertedRun` contributes text and
  increments `_trackedChanges`; `W.DeletedRun` contributes nothing but increments `_trackedChanges`
  — the accepted-revisions view. `W.Hyperlink` becomes a single linked inline. `W.SimpleField`
  and complex fields (through `UpdateFieldState`) are honored, with furniture stripping applied
  when the container is a header or footer.
- **`ProcessRun`** (private) — reads run properties (`Bold`, `Italic`), then walks children:
  `W.FieldChar`/`W.FieldCode` maintain the complex-field state; `W.Text` and `W.TabChar` append
  characters (guarded by `SkipText` so field definition text and stripped furniture never appear);
  `W.Break` of type `Page` sets the page-break flag; `W.Drawing` collects images through
  `CollectImage`; `W.FootnoteReference` flushes accumulated text and appends the raw `[^n]` marker
  and the collected footnote body.
- **`CollectImage`, `GatherImageTextCandidates`, `FindAdjacentCaption`** (private) — choose each
  image's naming and description text. `GatherImageTextCandidates` reads, in preference order, the
  author's `wp:docPr/@descr` (description) and `@title`, the adjacent `Caption`-styled paragraph
  (found by `AdjacentCaptionParagraph` and read through the same field-aware `WalkContainer` so a
  `SEQ` field's number is surfaced as a field result, never by matching digits), the `pic:cNvPr/@name`
  object name, and the nearest preceding heading. It deliberately does **not** read `wp:docPr/@name`,
  which is a tool-assigned placeholder. `CollectImage` hands the candidates to
  `WordOpenXmlImageReader.Resolve`, which appends the media-name fallback and applies the shared
  `ImageTextSelector` policy so the honesty rules live in Core.
- **`BuildTable`** (private) — the header-of-tables method. Walks rows, records
  `IsHeaderRow(row 0)` as `FirstRowIsHeader`, and for each cell: builds its inline content and
  nested-table flatten count with `BuildCellContent`; if the cell is a `w:vMerge` continuation
  emits an empty cell and increments the merged count; otherwise emits the built cell; then for
  each additional `w:gridSpan` column emits an empty cell and increments the merged count.
  Returns `null` when no cell of any row carries visible text, so the caller can skip and count
  the empty table.
- **`BuildCellContent`** (private) — walks paragraphs and nested tables. Paragraphs are joined by
  a raw `"\n"` inline (rendered as `<br>` in the writer); a nested table is flattened via
  `FlattenNestedTable` into a single raw inline holding `<br>`-joined rows and the nested-table
  count is incremented.
- **`BuildDocumentControl`** (private) — collects header and footer parts from every `w:sectPr`
  through `CollectHeaderFooterReferences`, renders each with furniture stripped through
  `RenderPartBlocks`, and deduplicates identical rendered content across sections by hashing the
  writer's own `WriteBlocks` output as the key. Distinct survivors are labeled by
  `AddLabeledSections` — a single survivor is labeled by kind (`Header` or `Footer`), multiples
  by numbered variants. Records two separate omission counts so each can be reported with its own
  matching wording: a part with no visible content that carried a furniture field is counted as
  page furniture, otherwise it is counted as empty. Keeping the two apart lets the emitter describe
  a mixed batch without one case's wording standing in for the other.
- **`IsFurnitureInstruction`** (private) — trims the instruction, splits on whitespace, and
  matches the first token case-insensitively against `FurnitureFieldInstructions`. This is the
  authoritative structural signal for page-numbering furniture, and is deliberately never a regex
  over rendered page-number text (which would be locale- and format-dependent, would misfire on
  documents where numbers look like nothing else, and would fire on revision strings containing
  digits).
- **`ThrowIfPasswordProtected`** (private) — reads eight bytes, rewinds, and raises
  `WordExtractionException` when they equal the OLE signature. This is the one adverse case the
  reader translates itself because the SDK would otherwise surface a raw package error.
- **`BuildNumbering`, `BuildFootnoteIndex`, `BuildComments`, `ReadProducerPageCount`,
  `ResolveHyperlink`, `DetermineHeadingLevel`, `TryGetListInfo`, `IsHeaderRow`,
  `IsVerticalMergeContinuation`, `GridSpanOf`, `IsOn`, `HasVisibleText`, `HasVisibleContent`,
  `NullIfBlank`** (all private) — the small pure helpers the walk composes from.

### Error Handling

A null stream is rejected with `ArgumentNullException`. A password-protected `.docx` (detected by
the OLE signature) surfaces as `WordExtractionException` with a clear "encrypted" message. A
missing `MainDocumentPart` surfaces as `WordExtractionException` for the same reason. Every other
fault propagates to Core, which wraps it as an `ExtractorFailed` failure. The reader translates
only what it can recognize better than the SDK; it never swallows adverse conditions and never
degrades the extraction quietly.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — `WordprocessingDocument`, `MainDocumentPart`, `HeaderPart`,
  `FooterPart`, `ImagePart`, the `Wordprocessing` type namespace (aliased `W`), the `Drawing`
  namespace (aliased `A`), the `Wordprocessing.Drawing` namespace (aliased `WP`), and the
  `Drawing.Pictures` namespace (aliased `PIC`) for the `pic:cNvPr` object name. See the
  *DocumentFormat.OpenXml* OTS design for the features used and the version pin.
- **`ImageTextSelector` and its candidate types** (Core) — the shared image-text policy the reader
  feeds through `WordOpenXmlImageReader`. See *ImageTextSelector Design*.
- **`WordOpenXmlImageReader`** — blip resolution and image-text selection.
- **`WordDocumentModel` and the Markdown D8 types** — the model this unit populates.
- **`WordExtractionException`** — the encrypted-document surface.

### Callers

`WordOpenXmlExtractor.ExtractAsync` for every extraction, and the same class's parse round-trip
self-test case (through a fresh instance each time). Tests construct additional instances directly
via `InternalsVisibleTo`.
