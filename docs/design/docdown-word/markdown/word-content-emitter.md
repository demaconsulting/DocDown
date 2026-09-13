## WordContentEmitter

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordContentEmitter` emits a rendered `WordDocumentModel` through the extraction sink, applying
the honest gap-and-diagnostic policy in one place. Every fact the
model records — an assumed table header, a flattened merge, an empty or page-furniture-only header
or footer, a document with no text, a request for rendered pages the backend does not provide —
becomes a diagnostic or a counted, reasoned gap here, gap-versus-diagnostic by whether anything was
actually lost: genuine loss is a counted gap that degrades, while an expected non-loss such as an
omitted empty or furniture-only part is an informational diagnostic that does not. Nothing is
omitted silently.

The unit exists because model-to-sink emission is genuinely one responsibility. Holding it apart
from the reader is what lets every emission decision be proved from a hand-built model with no
document behind it, and what keeps the output contract from acquiring a second implementation that
could quietly diverge from the first.

### Data Model

`WordContentEmitter` is an `internal static class` with no state. Three private path constants —
`ContentTarget = "content.md"`, `ImagesTarget = "images/"`, `PagesTarget = "pages/"` — are what
every gap targets exactly, because a gap whose target does not match the ledger entry is reported
by the contract verifier as an unexplained absence.

### Key Methods

- **`static ValueTask<bool> EmitAsync(IExtractionSink sink, ExtractionOptions options,
  WordDocumentModel model, CancellationToken cancellationToken)`** — writes images first (so the
  content renderer places the sink-allocated links), writes the content honoring the split mode,
  reports the `DocumentInfo`, then reports every diagnostic and gap the model implies. Returns
  `true` when any gap was reported so the caller can map to `Degraded`. Preconditions: every
  argument non-null. Postcondition: every artifact was routed through the sink; nothing was
  written to the filesystem directly.
- **`internal static ImageHint HintFor(WordImageRef image)`** — builds the passthrough hint every
  image is added with: `Transform = Passthrough`, `WidthPx = null`, `HeightPx = null`,
  `SourcePage = null`, plus the media type, preferred name, and source-reference URI from the
  reference, and the image's chosen `Description` and `DescriptionSource` so Core records them as
  image metadata (both absent when only the media name was available). Passthrough because an Open XML
  image part stores a complete image file byte-for-byte; null pixel dimensions because `wp:extent` is
  an EMU display size and reporting EMU as pixels would be a false provenance claim.
- **`WriteImagesAsync`** (private) — returns an empty result immediately when embedded images are
  suppressed, because the caller asked for none to be attempted and Core owns the record of that
  deliberate absence. Otherwise walks the model's image references — body first, then document
  control — hands each to the sink, records the returned path in the source-reference map, and
  reports the found count as the number of distinct files written (Core's SHA-256 dedup means one
  logo referenced many times is one image). Emits `WORD0006 VectorImageWrittenAsIs` when any EMF
  or WMF metafile was written, and `WORD0007 ForcePngNotHonored` when `ImageOutputMode.ForcePng`
  was requested and any image was written in its source encoding.
- **`WriteContentAsync`** (private) — inspects `options.ContentSplit`. `Auto` and `Single` both
  produce a single-flow `content.md` (Auto deliberately chose single-flow for Word because a Word
  document is one continuous flow). `PerPart` calls `BuildParts` to split at every `Heading 1`
  boundary; when the body has no such boundary the caller falls back to the single flow rather
  than losing the whole document to an empty parts list.
- **`BuildParts`** (private) — walks the body once collecting `Heading 1` positions. Leading
  matter before the first boundary (together with the `## Document Control` section) becomes part
  one, titled from the document title or `Front matter`; each subsequent section is titled from
  its heading's plain text (or `Section` when the heading is empty). Comments and footnotes are
  appended to the final part so a split never loses them.
- **`ReportModelDiagnostics`** (private) — emits `WORD0001 NoTextContent` with an
  `Unavailable`-scope content gap when the model carries no text, no comments, no footnotes, and
  no surviving document-control subsection; delegates to `ReportTableDiagnostics` otherwise. Emits
  `WORD0008 TrackedChangesAccepted` (informational) with the revision count when the reader
  counted any. Records omitted header and footer parts through `ReportHeaderFooterOmissions` when
  the reader counted any empty or page-furniture-only parts. Emits the rendering-unavailable gap
  through `ReportRenderingUnavailableGap` when `options.RenderPages` is set, so a request meets the
  backend-specific reason alongside Core's own engine-level gap.
- **`ReportChartsNotExtracted`** (private) — emits `WORD0010 ChartsNotExtracted` (warning) with a
  counted `GapKind.Text`/`Unavailable` gap when the model reports embedded charts, because a Word
  chart anchors through a graphic frame carrying no image blip, so neither the text walk nor the
  image walk sees it and its plotted data would otherwise vanish from a document claiming a complete
  extraction. This backend deliberately does **not** recover chart data: unlike the Excel and
  PowerPoint backends, which read a chart's cached data series and emit them as Core
  `ContentPartKind.Chart` parts (see _Output Subsystem Design_), the Word backend reports the charts
  as a gap and points the remedy at the source workbook. The image, table, and embedded-image
  handling here is likewise the Word-specific application of the product-wide sink contract, not a
  behavior unique to Word.
- **`ReportTableDiagnostics`** (private) — enumerates every `Table` block (body plus document
  control), accumulates the flattened-cell count and the assumed-header flag, and emits
  `WORD0004 TableHeaderAssumed` (informational), `WORD0003 EmptyTableSkipped` (informational,
  with the model's skipped-table count), and — when any cell was flattened — `WORD0005
  MergedCellsFlattened` with a counted `GapKind.Structure`/`PartiallyExtracted` gap whose
  `AffectedCount` is the flattening count.
- **`ReportHeaderFooterOmissions`** (private) — records the two omission counts as
  `WORD0009 HeaderFooterPageNumberingOnly` **informational diagnostics**, never gaps and never a
  degrade. An empty header or footer part and a page-furniture-only part are distinct expected
  non-losses — an empty part carried no content, a furniture-only part carried only structural
  page numbering — so each is reported through its own message helper (`EmptyPartsMessage`,
  `FurniturePartsMessage`) with singular/plural agreement, and a mixed batch produces both notes
  rather than one case's wording describing the other. Nothing was lost, so `content.md` stays
  complete and the run stays clean; reserving gaps and DEGRADED for genuine loss keeps the
  signal meaningful.
- **`ReportVectorImageGap`** and **`ReportForcePngGap`** (private) — the `WORD0006` and `WORD0007`
  gaps, targeting `images/`, with `PartiallyExtracted` scope because the images they refer to are
  on disk and the caveat concerns their readability or their encoding choice.
- **`ReportRenderingUnavailableGap`** (private) — emits `GapKind.Pages`/`Unavailable` targeting
  `pages/`, stating that rendered page images would come from a separate Word page-rendering
  extractor package that a host would register alongside this one. The wording is a fact about
  where the capability would live rather than an action verb — and true today
  and true on the day such a package exists.

### Error Handling

Every null argument to `EmitAsync` is rejected with `ArgumentNullException`. Cancellation is
observed between images and between parts. No condition of the model raises an exception here:
every shortfall becomes a diagnostic or a counted gap on the sink. The unit does no filesystem
I/O itself — every byte goes through the sink, so it holds no OS handles and has no cleanup path
of its own.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `ExtractionDiagnostic`,
  `ExtractionGap`, `EnvironmentFact`, `DocumentInfo`, `ImageHint`, `ImageTransform`, `ContentPart`,
  `ContentPartKind`, `ImageOutputMode`, `ContentSplitMode`, `GapKind`, `GapScope`,
  `DiagnosticSeverity`.
- **`WordDocumentModel`, `WordBlock`, `WordImageRef`, `WordTableModel`, `WordDocumentControlSection`,
  `WordDiagnosticCodes`** — the model and codes emitted.
- **`WordMarkdownWriter`, `WordTableWriter`** — for content rendering.

### Callers

`WordOpenXmlExtractor.ExtractAsync` after it has produced a
model. Also indirectly `WordOpenXmlImageReader.Read`, which reuses `HintFor` to build the image
hints for its enumerated bytes.
