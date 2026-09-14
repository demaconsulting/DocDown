using DocDown.Core;

namespace DocDown.Word.Markdown;


/// <summary>
///     The backend-neutral model of a whole Word document: the body flow plus the metadata,
///     document-control content, and the counts the emitter uses to describe the produced output
///     and any extraction notes.
/// </summary>
/// <param name="Body">The document body as an ordered block sequence.</param>
/// <param name="DocumentControl">
///     The surviving header and footer subsections, deduplicated across sections, rendered into a
///     labeled <c>## Document Control</c> section after the title and before the body. Empty when
///     every header and footer was page furniture. Each subsection carries its own block sequence,
///     so a header holding a table or a logo flows through the same writer as the body.
/// </param>
/// <param name="Comments">The document's comments, author-attributed, rendered into a <c>## Comments</c> section.</param>
/// <param name="Footnotes">The footnote and endnote bodies, referenced from the body as <c>[^n]</c> and rendered in a <c>## Footnotes</c> section.</param>
/// <param name="Title">The document title from <c>docProps/core.xml</c>, or <see langword="null"/> when blank or absent.</param>
/// <param name="Author">The document author from <c>docProps/core.xml</c>, or <see langword="null"/> when blank or absent.</param>
/// <param name="ProducerPageCount">
///     The producer-cached page count from <c>docProps/app.xml</c> <c>&lt;Pages&gt;</c>, or
///     <see langword="null"/> when absent. Never computed or guessed — Open XML has no true page count.
/// </param>
/// <param name="TrackedChangeCount">
///     The number of tracked-change revisions rendered in the accepted view, so tests and future
///     reporting can observe that choice without a second pass over the document.
/// </param>
/// <param name="HeaderFooterPartsFound">The total number of header and footer parts found across all sections.</param>
/// <param name="HeaderFooterPartsEmpty">
///     The number of header and footer parts omitted because they carried no content at all, kept on
///     the model so tests can distinguish them from surviving document-control content without
///     a second pass over the package.
/// </param>
/// <param name="HeaderFooterPartsPageFurniture">
///     The number of header and footer parts omitted because they carried only page-numbering fields
///     (document furniture), kept so tests can observe that omission without a second pass over the package.
/// </param>
/// <param name="EmptyTablesSkipped">
///     The number of tables skipped because they held no cell content, so the caller can distinguish
///     authored empty tables from tables that produced rendered markdown.
/// </param>
/// <param name="ChartsFound">
///     The number of DrawingML chart parts the document embeds. A chart in a Word document carries
///     its plotted data in a chart part exactly as a workbook's does, and this backend does not read
///     that data, so the count exists solely to let the emitter record that incomplete extraction
///     step rather than let the charts vanish from the output.
/// </param>
/// <param name="Metadata">
///     What the document asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model). Carried on the
///     model so the emitter can report it once through the sink for <c>metadata.json</c>.
/// </param>
/// <remarks>
///     The counts live on the model rather than being recomputed because only the reader, walking
///     the document once, can observe them; the emitter turns them into inventory counts and notes.
///     Immutable and thread-safe.
/// </remarks>
internal sealed record WordDocumentModel(
    IReadOnlyList<WordBlock> Body,
    IReadOnlyList<WordDocumentControlSection> DocumentControl,
    IReadOnlyList<WordComment> Comments,
    IReadOnlyList<IReadOnlyList<WordInline>> Footnotes,
    string? Title,
    string? Author,
    int? ProducerPageCount,
    int TrackedChangeCount,
    int HeaderFooterPartsFound,
    int HeaderFooterPartsEmpty,
    int HeaderFooterPartsPageFurniture,
    int EmptyTablesSkipped,
    int ChartsFound = 0,
    DocumentMetadata? Metadata = null);

/// <summary>
///     One surviving header or footer, rendered as a labeled subsection of <c>## Document Control</c>.
/// </summary>
/// <param name="Label">
///     The origin label (for example <c>Header</c>, or <c>Footer (section 2, first page)</c> when
///     more than one distinct value survives).
/// </param>
/// <param name="Blocks">The subsection's block sequence, rendered by the same writer as the body.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordDocumentControlSection(string Label, IReadOnlyList<WordBlock> Blocks);

/// <summary>
///     A single document comment.
/// </summary>
/// <param name="Author">The comment author, or <see langword="null"/> when the document records none.</param>
/// <param name="Content">The comment's inline content.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordComment(string? Author, IReadOnlyList<WordInline> Content);

/// <summary>
///     A single block in the backend-neutral document model.
/// </summary>
/// <param name="Kind">The block's kind, which selects the meaningful payload members.</param>
/// <param name="Inlines">
///     The inline runs for a heading, paragraph, list item, or document-control subsection, or
///     <see langword="null"/> for a table, image, or page break.
/// </param>
/// <param name="HeadingLevel">The 1-based heading level for a <see cref="WordBlockKind.Heading"/> block.</param>
/// <param name="List">The list membership for a <see cref="WordBlockKind.ListItem"/> block, else <see langword="null"/>.</param>
/// <param name="Table">The table for a <see cref="WordBlockKind.Table"/> block, else <see langword="null"/>.</param>
/// <param name="Image">The image reference for a <see cref="WordBlockKind.Image"/> block, else <see langword="null"/>.</param>
/// <param name="Label">
///     An origin label for a <see cref="WordBlockKind.DocumentControl"/> subsection (for example
///     <c>Header</c> or <c>Footer (section 2, first page)</c>), else <see langword="null"/>.
/// </param>
/// <remarks>
///     A single record with kind-selected payloads keeps the block sequence uniform and cheap to
///     walk, which is what lets one writer render every kind and one reader per backend populate
///     it. Immutable and thread-safe.
/// </remarks>
internal sealed record WordBlock(
    WordBlockKind Kind,
    IReadOnlyList<WordInline>? Inlines = null,
    int HeadingLevel = 0,
    WordListInfo? List = null,
    WordTableModel? Table = null,
    WordImageRef? Image = null,
    string? Label = null);

/// <summary>
///     A reference to an embedded image, carrying the exact stored bytes and their provenance.
/// </summary>
/// <param name="Bytes">The complete image file bytes, exactly as the document stored them.</param>
/// <param name="MediaType">The image media type (for example <c>image/png</c>), from the part content type.</param>
/// <param name="PreferredName">
///     The base name Core slugs the file from — the selected image text of any usable source, or the
///     media part name, or <see langword="null"/> to let Core name it from its ordinal alone.
/// </param>
/// <param name="SourceRef">The part URI within the package, recorded as provenance, or <see langword="null"/>.</param>
/// <param name="AltText">
///     Descriptive alt text to assert in markdown, present only when a genuinely descriptive source
///     was chosen (an author description, title, caption, or meaningful object name); otherwise
///     <see langword="null"/> so a neutral placeholder is emitted instead of implying a description.
/// </param>
/// <param name="Description">
///     The chosen text recorded in the manifest as image metadata, present for descriptive and
///     contextual (heading) sources alike; <see langword="null"/> when only the media name was
///     available.
/// </param>
/// <param name="DescriptionSource">
///     The camelCase provenance of <paramref name="Description"/> (for example <c>description</c> or
///     <c>heading</c>), or <see langword="null"/> when there is no description.
/// </param>
/// <remarks>
///     An Open XML image part stores a complete image file byte-for-byte, so a reference always
///     describes bytes that can be written through unchanged as a passthrough. The naming, alt-text,
///     and manifest-description fields are populated from the shared image-text policy so a heading
///     is used to help name the file yet never asserted as if it described the picture. Immutable
///     and thread-safe.
/// </remarks>
internal sealed record WordImageRef(
    byte[] Bytes,
    string MediaType,
    string? PreferredName,
    string? SourceRef,
    string? AltText = null,
    string? Description = null,
    string? DescriptionSource = null);

/// <summary>
///     The kind of a block in a <see cref="WordDocumentModel"/>.
/// </summary>
/// <remarks>
///     The block vocabulary is the pivot that keeps the entire markdown mapping — the part where
///     Word must beat PDF —
///     testable from a hand-built model with no document and no Open XML SDK. Kept
///     deliberately small: the consumer is a language model, so only the structure that carries
///     meaning is modeled.
/// </remarks>
internal enum WordBlockKind
{
    /// <summary>A heading, whose level is carried by <see cref="WordBlock.HeadingLevel"/>.</summary>
    Heading,

    /// <summary>A plain paragraph of inline runs.</summary>
    Paragraph,

    /// <summary>A list item, whose level and ordering are carried by <see cref="WordBlock.List"/>.</summary>
    ListItem,

    /// <summary>A table, carried by <see cref="WordBlock.Table"/>.</summary>
    Table,

    /// <summary>An embedded image, carried by <see cref="WordBlock.Image"/>.</summary>
    Image,

    /// <summary>An explicit page break, rendered as a thematic break.</summary>
    PageBreak,

    /// <summary>
    ///     A document-control subsection (a surviving header or footer), whose origin label is
    ///     carried by <see cref="WordBlock.Label"/>.
    /// </summary>
    DocumentControl
}

/// <summary>
///     A single inline run of text with the minimal formatting the markdown mapping preserves.
/// </summary>
/// <param name="Text">The literal text of the run. Escaped by the writer unless <paramref name="Raw"/> is set.</param>
/// <param name="Bold">Whether the run is bold, rendered as <c>**…**</c>.</param>
/// <param name="Italic">Whether the run is italic, rendered as <c>*…*</c>.</param>
/// <param name="Href">A hyperlink target, rendered as <c>[text](href)</c>, or <see langword="null"/> for plain text.</param>
/// <param name="Raw">
///     When <see langword="true"/>, <paramref name="Text"/> is already markdown and is emitted
///     verbatim without escaping (used for footnote reference markers such as <c>[^1]</c>).
/// </param>
/// <remarks>
///     Inline formatting is deliberately minimal — bold, italic, and hyperlinks only. A language
///     model consumer gains nothing from colors, fonts, or strikethrough, which would add tokens
///     without adding meaning. Instances are immutable and thread-safe.
/// </remarks>
internal sealed record WordInline(string Text, bool Bold = false, bool Italic = false,
    string? Href = null, bool Raw = false);

/// <summary>
///     The list membership of a <see cref="WordBlockKind.ListItem"/> block.
/// </summary>
/// <param name="Level">The zero-based nesting level, indented two spaces per level in the output.</param>
/// <param name="Ordered">
///     <see langword="true"/> for an ordered list (rendered <c>1. </c>), <see langword="false"/>
///     for a bulleted list (rendered <c>- </c>). Determined from the <c>numbering.xml</c>
///     <c>w:numFmt</c>: any format other than <c>bullet</c> is ordered.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordListInfo(int Level, bool Ordered);

/// <summary>
///     A backend-neutral table model: a grid of cells plus the accounting the caller needs to
///     report every flattening honestly.
/// </summary>
/// <param name="Rows">
///     The table's rows, each a list of cells. The reader emits a rectangular-enough grid: a
///     horizontally merged (<c>w:gridSpan</c>) or vertically merged (<c>w:vMerge</c>) continuation
///     cell is present but empty, so column alignment is preserved even though the merge itself
///     cannot be expressed in GitHub-flavored markdown.
/// </param>
/// <param name="FirstRowIsHeader">
///     <see langword="true"/> when Word itself marked the first row a header (<c>w:tblHeader</c>).
///     When <see langword="false"/> the writer still uses row one as the header, because GFM
///     requires a header row.
/// </param>
/// <param name="MergedCellCount">
///     The number of cells emptied by a horizontal or vertical merge, so the emitter can record how
///     much table structure GFM could not preserve.
/// </param>
/// <param name="NestedTableCount">
///     The number of nested tables flattened into a parent cell as <c>&lt;br&gt;</c>-joined rows,
///     counted into the same structure-loss note.
/// </param>
/// <remarks>
///     Word carries genuine table structure where a PDF flattened it into concatenated runs, so
///     this model is the headline differentiator: it preserves rows and columns and accounts for
///     exactly what markdown could not preserve. Immutable and thread-safe.
/// </remarks>
internal sealed record WordTableModel(
    IReadOnlyList<IReadOnlyList<WordTableCell>> Rows,
    bool FirstRowIsHeader,
    int MergedCellCount,
    int NestedTableCount);

/// <summary>
///     One cell of a <see cref="WordTableModel"/>.
/// </summary>
/// <param name="Content">
///     The cell's inline content. A merge-continuation cell carries an empty list; a cell holding
///     a nested table carries the inner rows pre-joined with <c>&lt;br&gt;</c> markers so the
///     writer can render them without nesting.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record WordTableCell(IReadOnlyList<WordInline> Content);
