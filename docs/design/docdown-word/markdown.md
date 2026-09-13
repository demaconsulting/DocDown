## Markdown Subsystem

![DocDown.Word Structure](DocDownWordView.svg)

### Overview

The Markdown subsystem is the reader-neutral core of `DocDown.Word`. It defines the block
vocabulary the reader populates, the writer that turns that vocabulary into a markdown flow, the
table writer that expresses `w:tbl` structure as a GitHub-flavored-markdown table, the emitter that
walks a rendered model through the extraction sink, and the pinned diagnostic-code contract. Every
mapping decision that differentiates a Word extraction from a PDF extraction — genuine tables, the
`## Document Control` section, image passthrough with honest provenance, tracked-change
accepted-view, footnote and comment sections, per-part splitting at `Heading 1` — lives here and
is testable from a hand-built model with no document and no Open XML SDK.

The subsystem exists because reading a document and rendering what was read are separate concerns.
Holding the whole projection apart from the reader is what makes every mapping decision provable
from a hand-built model, and what keeps the output contract from acquiring a second implementation
that could drift from the first.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `WordDocumentModel` | Inbound, from the reader | .NET record | The pivot between reading and rendering |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound, from `IExtractionContext` | .NET record | Split mode, force-PNG, render, images |
| `WordDiagnosticCodes` | Outbound to callers | .NET constants | The `WORD0001`–`WORD0009` contract, pinned by test |

### Design

**The model.** `WordDocumentModel` is the whole cross-backend contract. It carries the body as an
ordered `WordBlock` sequence; the surviving `WordDocumentControlSection` list; the `WordComment`
list; the footnote bodies as inline sequences; the metadata (`Title`, `Author`,
`ProducerPageCount`); and the counts the reader observed but the writer cannot recompute
(`TrackedChangeCount`, `HeaderFooterPartsFound`/`Omitted` with a reason string,
`EmptyTablesSkipped`). Counts live on the model because only the reader, walking the document
once, can observe them; the emitter turns them into the corresponding gaps and diagnostics.

**The block vocabulary.** `WordBlockKind` (`Heading`, `Paragraph`, `ListItem`, `Table`, `Image`,
`PageBreak`, `DocumentControl`) is deliberately small — a language-model consumer gains nothing
from colors, fonts, or strikethrough, which would add tokens without adding meaning. A single
`WordBlock` record with kind-selected payloads keeps the sequence uniform and cheap to walk.
`WordInline` carries the literal text and the minimal formatting the mapping preserves — bold,
italic, hyperlink — plus a `Raw` flag so a footnote-reference marker or a hard break survives
escaping. `WordListInfo` records the zero-based nesting level and whether the list is ordered;
ordering is decided from `numbering.xml`'s `w:numFmt` as *any format other than `bullet` is
ordered*.

**The table model.** `WordTableModel` and `WordTableCell` preserve rows and columns and account
for exactly what could not be preserved. Horizontally merged (`w:gridSpan`) and vertically merged
(`w:vMerge`) continuation cells are present but empty, so column alignment survives even though
GFM cannot express the merge itself; `MergedCellCount` and `NestedTableCount` carry the flattening
count into the caller's `WORD0005` gap. `FirstRowIsHeader` records whether Word itself marked the
first row a header, so the caller can decide whether to emit `WORD0004 TableHeaderAssumed`.

**The document-control model.** `WordDocumentControlSection` records the origin label (`Header`,
`Footer`, or a numbered variant when more than one distinct value survives) and the block sequence
for the subsection — including its own tables and images, which flow through the same writer as
the body. `WordDocumentModel.HeaderFooterPartsEmpty` and `HeaderFooterPartsPageFurniture` carry the
two counted, separated omissions the caller reports as `WORD0009 HeaderFooterPageNumberingOnly`
informational diagnostics — never gaps, because omitting an empty or furniture-only part loses no
document content.

**The image reference.** `WordImageRef` carries the complete stored bytes together with the media
type, a preferred base name, and the source-reference URI within the package. Bytes and provenance
travel as one record because the two must agree; the emitter's `HintFor` always claims
`ImageTransform.Passthrough` because an Open XML image part stores a complete image file
byte-for-byte, and `WidthPx`/`HeightPx` stay `null` because `wp:extent` is an EMU display size, not
a pixel count.

**The diagnostic-code contract.** `WordDiagnosticCodes` defines the nine codes this package owns —
`WORD0001` `NoTextContent`, `WORD0002` `PasswordProtected`, `WORD0003` `EmptyTableSkipped`,
`WORD0004` `TableHeaderAssumed`, `WORD0005` `MergedCellsFlattened`, `WORD0006`
`VectorImageWrittenAsIs`, `WORD0007` `ForcePngNotHonored`, `WORD0008` `TrackedChangesAccepted`,
`WORD0009` `HeaderFooterPageNumberingOnly`. The numbering is contiguous and pinned by
`WordDiagnosticCodes_Table_MatchesPinnedContract`, so a consumer branching on a code is never
surprised by a renumbering. The distinct `WORD` prefix cannot collide with Core's `DD` range or
the PDF package's `PDF` range whatever any of them adds later, which makes ownership self-evident
in any manifest.

**The unit split.** Three units divide the work along the boundaries their responsibilities draw:
`WordMarkdownWriter` owns rendering (block sequence to markdown, inline escaping, document-control
placement, comment and footnote sections); `WordTableWriter` owns the seven table rules and the
flattened-cell count; `WordContentEmitter` owns the sink walk and the whole gap-and-diagnostic
policy. The supporting D8 types listed above live in the same
subsystem folder because they are the vocabulary of the units, not units of their own.
