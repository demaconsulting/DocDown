## Markdown Subsystem

![DocDown.Word Structure](WordView.svg)

### Overview

The Markdown subsystem is the reader-neutral core of `DocDown.Word`. It defines the block
vocabulary the reader populates, the writer that turns that vocabulary into a markdown flow, the
table writer that expresses `w:tbl` structure as a GitHub-flavored-markdown table, and the emitter
that writes content, images, content inventory, metadata, and extraction notes through the sink.
Every mapping decision that differentiates a Word extraction from a PDF extraction — genuine
tables, the `## Document Control` section, image passthrough with honest provenance, comments,
and footnotes — lives here and is testable from a hand-built model
with no document and no Open XML SDK.

The subsystem exists because reading a document and rendering what was read are separate concerns.
Holding the whole projection apart from the reader is what makes every mapping decision provable
from a hand-built model, and what keeps the output contract from acquiring a second implementation
that could drift from the first.

### Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `WordDocumentModel` | Inbound, from the reader | .NET record | The pivot between reading and rendering |
| `IExtractionSink` | Outbound, from the emitter to Core | .NET interface | The only output channel |
| `ExtractionOptions` | Inbound | .NET | Image suppression and size limits |

### Design

**The model.** `WordDocumentModel` is the cross-backend contract. It carries the body as an ordered
`WordBlock` sequence; the surviving `WordDocumentControlSection` list; the `WordComment` list; the
footnote bodies as inline sequences; metadata (`Title`, `Author`, `ProducerPageCount`, and the
optional Core `DocumentMetadata` projection); and the counts only the reader can observe cheaply in
one pass, including tracked changes, omitted header or footer parts, empty tables, and embedded
charts.

**The block vocabulary.** `WordBlockKind` (`Heading`, `Paragraph`, `ListItem`, `Table`, `Image`,
`PageBreak`) is deliberately small. A language-model consumer gains little from Word-specific
typography, but it gains a great deal from an honest structure: headings, lists, real tables,
inline images, comments, and footnotes. `WordInline` carries literal text plus the minimal
preserved formatting — bold, italic, hyperlink, and a `Raw` flag for markers that must bypass
escaping.

**The table model.** `WordTableModel` and `WordTableCell` preserve rows and columns and account for
exactly what markdown could not preserve. Merge continuations are represented as empty cells so the
grid stays aligned, and nested tables are pre-flattened into `<br>`-joined rows.
`MergedCellCount` and `NestedTableCount` are the accounting the emitter uses when it reports that
markdown could not preserve all of the table structure.

**The document-control model.** `WordDocumentControlSection` records the origin label (`Header`,
`Footer`, or a numbered variant) and the block sequence for the subsection. Headers and footers
that reduce to page furniture or to nothing at all are omitted from the rendered section. The
reader still counts them on the model so tests can observe those cases without a second pass over the
package.

**The image reference.** `WordImageRef` carries the complete stored bytes together with the media
type, preferred base name, optional description, description source, and source part URI. Bytes
and provenance travel as one record because the two must agree. The emitter's `HintFor()` always
claims `ImageTransform.Passthrough`, and it leaves pixel dimensions unstated because `wp:extent`
is an EMU display size rather than a pixel count.

**The content inventory and notes.** `WordContentEmitter` reports the content inventory from the
model rather than by scanning the rendered markdown again. It counts text blocks, headings, tables, list
items, inline images, comments, distinct comment authors, and footnotes, and marks genuinely
looked-for categories so zero remains explicit. It emits only two short extraction notes: charts
whose chart parts were not read, and merged or nested table structure flattened for markdown.

**The unit split.** Three units divide the work along the boundaries their responsibilities draw:
`WordMarkdownWriter` owns rendering, `WordTableWriter` owns the table rules and the
flattened-cell count, and `WordContentEmitter` owns sink emission, inventory reporting, and note
reporting. The supporting record types live in the same subsystem folder because they are the
units' shared vocabulary rather than independent units of their own.
