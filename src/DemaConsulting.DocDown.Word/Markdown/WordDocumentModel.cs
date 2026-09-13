using DocDown.Core;

namespace DocDown.Word.Markdown;


/// <summary>
///     The backend-neutral model of a whole Word document: the body flow plus the metadata,
///     document-control content, and the counts the extractor needs to report every decision.
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
///     The number of tracked-change revisions rendered in the accepted view, so the caller can
///     report <c>WORD0008 TrackedChangesAccepted</c> with the count when it is non-zero.
/// </param>
/// <param name="HeaderFooterPartsFound">The total number of header and footer parts found across all sections.</param>
/// <param name="HeaderFooterPartsEmpty">
///     The number of header and footer parts omitted because they carried no content at all. Recorded
///     as an informational diagnostic, never a gap: an empty part is an expected authoring artifact
///     and drops no content, so it neither degrades the run nor marks <c>content.md</c> partial.
/// </param>
/// <param name="HeaderFooterPartsPageFurniture">
///     The number of header and footer parts omitted because they carried only page-numbering fields
///     (document furniture). Recorded as an informational diagnostic, never a gap: page furniture is
///     structural repetition, not document content, so omitting it drops nothing and neither degrades
///     the run nor marks <c>content.md</c> partial.
/// </param>
/// <param name="EmptyTablesSkipped">The number of tables skipped because they held no cell content, so the caller can note <c>WORD0003</c>.</param>
/// <param name="Metadata">
///     What the document asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model). Carried on the
///     model so the emitter can report it once through the sink for <c>metadata.json</c>.
/// </param>
/// <remarks>
///     The counts live on the model rather than being recomputed because only the reader, walking
///     the document once, can observe them; the extractor turns them into gaps and diagnostics.
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
