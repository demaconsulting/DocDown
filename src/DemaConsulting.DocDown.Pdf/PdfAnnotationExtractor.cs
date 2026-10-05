using System.Globalization;
using DocDown.Core;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace DocDown.Pdf;

/// <summary>
///     Reads the reviewer commentary a PDF carries as annotations, so those remarks reach the
///     review-comments artifact instead of being lost or mixed into the document's own content.
/// </summary>
/// <remarks>
///     <para>
///         A reviewer's remark is not part of the document — it is commentary <em>about</em> the
///         document. DocDown therefore keeps the two apart: page text goes to <c>content.md</c>, and
///         what a reviewer wrote in the margin travels to Core as a review comment. This unit is the
///         PDF half of that separation; it reads annotations and hands back the author, the body, and
///         the page each remark sits on, and it never writes markdown.
///     </para>
///     <para>
///         <strong>Which annotation types count as reviewer commentary is an explicit decision, not a
///         default.</strong> A PDF annotation is any interactive object layered over a page, and most
///         annotation types carry no human remark at all: a <c>Link</c> is navigation, a
///         <c>Widget</c> is a form control, and <c>PrinterMark</c>, <c>TrapNet</c>, and
///         <c>Watermark</c> are production artifacts. Treating every annotation as a comment would
///         fill <c>review-comments.md</c> with machinery. The included set is therefore stated as a
///         table in <see cref="CommentaryAnnotationTypes"/>, and is the set of PDF <em>markup</em>
///         annotations (ISO 32000-1 Section 12.5.6.2) — the annotations a PDF reader itself shows in
///         its comments pane — together with <c>Popup</c>. Everything outside that table is excluded
///         entirely, whatever it contains.
///     </para>
///     <para>
///         Within the included set, an annotation is kept only when its <c>/Contents</c> entry
///         carries text. That single rule is what makes the markup types behave sensibly: a
///         highlight drawn with no remark attached is a reading aid rather than a comment, and a
///         <c>Popup</c> with no text of its own is merely the window that displays its parent's
///         remark, so emitting either would invent a comment nobody wrote. A highlight or popup that
///         <em>does</em> carry text is a genuine remark and is kept.
///     </para>
///     <para>
///         The author is read from the annotation dictionary's <c>/T</c> entry, because PdfPig's
///         <see cref="Annotation"/> exposes no author member of its own. A PDF may store that entry
///         as either a literal or a hexadecimal string, so both are read; an annotation that names no
///         author yields <see langword="null"/> rather than a placeholder, because "the document did
///         not say who wrote this" and "someone called Unknown wrote this" are different claims and
///         only the first is true.
///     </para>
///     <para>
///         Annotation reading is contained per page: a page whose annotations cannot be read is
///         counted and explained in a plain note rather than being allowed to abort an extraction
///         whose text and images were perfectly readable. Stateless and thread-safe; the collected
///         results live for the duration of a single call.
///     </para>
/// </remarks>
internal static class PdfAnnotationExtractor
{
    /// <summary>
    ///     The annotation types whose <c>/Contents</c> entry is a reviewer's remark.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Stated as a table rather than left to be reconstructed from branches, because this is
    ///         the decision the whole unit turns on and the one a reviewer most needs to check. The
    ///         rows are the PDF markup annotations (ISO 32000-1 Section 12.5.6.2) plus
    ///         <c>Popup</c>, grouped by why they are here:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <c>Text</c>, <c>FreeText</c>, <c>Popup</c> — purpose-built commentary. A
    ///                 sticky note, an on-page text box, and a comment window exist only to carry
    ///                 words a person wrote.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>Highlight</c>, <c>Underline</c>, <c>Squiggly</c>, <c>StrikeOut</c> — text
    ///                 markup. The mark itself is not a comment, but reviewers routinely attach a
    ///                 remark to one, and the blank-content rule keeps the bare marks out.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>Line</c>, <c>Square</c>, <c>Circle</c>, <c>Polygon</c>, <c>PolyLine</c>,
    ///                 <c>Stamp</c>, <c>Caret</c>, <c>Ink</c>, <c>FileAttachment</c> — drawn or
    ///                 attached markup, included for the same reason and under the same rule. A
    ///                 "Please revise" typed into a stamp is a review comment however it was drawn.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Deliberately absent: <c>Link</c>, <c>Widget</c>, <c>Screen</c>, <c>Sound</c>,
    ///         <c>Movie</c>, <c>PrinterMark</c>, <c>TrapNet</c>, <c>Watermark</c>, <c>Artwork3D</c>,
    ///         and <c>Other</c>. None of those is a remark about the document, so text found on one
    ///         is machinery rather than commentary.
    ///     </para>
    /// </remarks>
    private static readonly IReadOnlySet<AnnotationType> CommentaryAnnotationTypes =
        new HashSet<AnnotationType>
        {
            // Purpose-built commentary
            AnnotationType.Text,
            AnnotationType.FreeText,
            AnnotationType.Popup,

            // Text markup, kept only when a remark is attached
            AnnotationType.Highlight,
            AnnotationType.Underline,
            AnnotationType.Squiggly,
            AnnotationType.StrikeOut,

            // Drawn and attached markup, kept only when a remark is attached
            AnnotationType.Line,
            AnnotationType.Square,
            AnnotationType.Circle,
            AnnotationType.Polygon,
            AnnotationType.PolyLine,
            AnnotationType.Stamp,
            AnnotationType.Caret,
            AnnotationType.Ink,
            AnnotationType.FileAttachment
        };

    /// <summary>The maximum number of page numbers named in the unreadable-annotations note.</summary>
    /// <remarks>
    ///     Bounds the note text for a pathologically damaged document while still naming enough pages
    ///     to be actionable. The count stated in the note is always exact even when the list of named
    ///     pages is truncated.
    /// </remarks>
    private const int MaxNamedPages = 20;

    /// <summary>
    ///     Collects the reviewer comments carried by the selected pages' annotations.
    /// </summary>
    /// <param name="pages">
    ///     The pages to read, already restricted to any requested page range. Must not be null.
    /// </param>
    /// <param name="sink">The sink any explanatory note is recorded on. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     The comments found, in page order and, within a page, in the order the document lists its
    ///     annotations. Empty when the selected pages carry no commentary annotation with text.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="pages"/> or <paramref name="sink"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is canceled between pages.
    /// </exception>
    /// <remarks>
    ///     Document order is preserved because <c>review-comments.md</c> renders comments in the order
    ///     they were reported, and a reader scanning that file expects to travel through the document
    ///     rather than to jump about it. Side effect: records a note on the sink when a page's
    ///     annotations could not be read; otherwise reads only.
    /// </remarks>
    internal static IReadOnlyList<PdfReviewAnnotation> Extract(
        IReadOnlyList<Page> pages, IExtractionSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(sink);

        var comments = new List<PdfReviewAnnotation>();
        var unreadablePages = new List<int>();

        // Walk the selected pages in order so comments arrive in document order
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryReadAnnotations(page, out var annotations))
            {
                comments.AddRange(annotations
                    .Select(annotation => ToReviewComment(annotation, page.Number))
                    .OfType<PdfReviewAnnotation>());
            }
            else
            {
                unreadablePages.Add(page.Number);
            }
        }

        ReportUnreadableAnnotationsNote(sink, unreadablePages, pages.Count);
        return comments;
    }

    /// <summary>
    ///     Reads one page's annotations, reporting failure as data rather than as an exception.
    /// </summary>
    /// <param name="page">The page to read.</param>
    /// <param name="annotations">The page's annotations when the read succeeded; otherwise empty.</param>
    /// <returns>
    ///     <see langword="true"/> when the page's annotations were read; <see langword="false"/> when
    ///     the document's annotation structures defeated the parser.
    /// </returns>
    /// <remarks>
    ///     The annotations of a page are enumerated lazily, so a malformed annotation dictionary
    ///     surfaces part-way through the walk rather than at the call. Materializing the sequence here
    ///     puts the whole read inside the guard, which is what makes the failure attributable to one
    ///     page. Containment matters because an unguarded fault would turn a PDF whose text and images
    ///     extracted perfectly into an unreadable document on account of a single damaged comment —
    ///     losing far more than the comment was worth. Reads only.
    /// </remarks>
    private static bool TryReadAnnotations(Page page, out IReadOnlyList<Annotation> annotations)
    {
        try
        {
            annotations = page.GetAnnotations().ToList();
            return true;
        }
#pragma warning disable CA1031 // Any parser fault here is reported as a note, never allowed to abort an otherwise good extraction
        catch (Exception)
#pragma warning restore CA1031
        {
            annotations = [];
            return false;
        }
    }

    /// <summary>
    ///     Converts an annotation into a reviewer comment, or rejects it.
    /// </summary>
    /// <param name="annotation">The annotation to judge and convert.</param>
    /// <param name="pageNumber">The 1-based number of the page the annotation sits on.</param>
    /// <returns>
    ///     The comment when the annotation's type is in the commentary table and its content carries
    ///     text; otherwise <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     Both halves of the test are needed and neither subsumes the other: the table keeps
    ///     navigation and form machinery out even when it carries text, and the content test keeps
    ///     bare highlights and empty popup windows out even though their types are included. Judging
    ///     and converting together rather than in two passes is what lets the trimmed body be tested
    ///     and then used, so the body of a returned comment is non-blank by construction. Pure.
    /// </remarks>
    private static PdfReviewAnnotation? ToReviewComment(Annotation annotation, int pageNumber)
    {
        var body = annotation.Content?.Trim();
        return CommentaryAnnotationTypes.Contains(annotation.Type) && !string.IsNullOrEmpty(body)
            ? new PdfReviewAnnotation(pageNumber, ReadAuthor(annotation), body)
            : null;
    }

    /// <summary>
    ///     Reads the annotation's author from its dictionary's <c>/T</c> entry.
    /// </summary>
    /// <param name="annotation">The annotation whose dictionary is inspected.</param>
    /// <returns>
    ///     The author's name, or <see langword="null"/> when the annotation names no author or names
    ///     one with no text in it.
    /// </returns>
    /// <remarks>
    ///     PdfPig's annotation type exposes no author member, so the PDF title entry — which is where
    ///     a reader stores the commenter's name — is read from the raw dictionary. A PDF may hold a
    ///     string as either a literal or a hexadecimal string and both are legal for this entry, so
    ///     both forms are accepted; any other token shape is a malformed value and yields no author
    ///     rather than a rendering of whatever it was. Returning <see langword="null"/> for an absent
    ///     author is deliberate: Core renders an unattributed comment as such, and fabricating a name
    ///     here would put a claim in the output that the document never made. Pure.
    /// </remarks>
    private static string? ReadAuthor(Annotation annotation)
    {
        if (annotation.AnnotationDictionary is not { } dictionary ||
            !dictionary.TryGet(NameToken.T, out var token))
        {
            return null;
        }

        var name = token switch
        {
            StringToken literal => literal.Data,
            HexToken hex => hex.Data,
            _ => null
        };

        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    /// <summary>
    ///     Reports a plain note naming the pages whose annotations could not be read.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="unreadablePages">The 1-based numbers of the pages that defeated the parser.</param>
    /// <param name="selectedPages">The number of pages the extraction walked.</param>
    /// <remarks>
    ///     An attempted step that could not complete is exactly what an
    ///     <see cref="ExtractionNote"/> exists to carry, and comments lost in silence would leave the
    ///     output looking complete while missing a reviewer's words. Nothing is reported when every
    ///     page was read, because a document that simply carries no annotations has nothing
    ///     incomplete about it. Side effect: records a note on the sink.
    /// </remarks>
    private static void ReportUnreadableAnnotationsNote(
        IExtractionSink sink, IReadOnlyList<int> unreadablePages, int selectedPages)
    {
        if (unreadablePages.Count == 0)
        {
            return;
        }

        var named = string.Join(
            ", ",
            unreadablePages.Take(MaxNamedPages).Select(number => number.ToString(CultureInfo.InvariantCulture)));
        var pageList = unreadablePages.Count > MaxNamedPages ? $"{named}, and others" : named;

        sink.ReportNote(new ExtractionNote(
            $"Annotations could not be read on {unreadablePages.Count.ToString(CultureInfo.InvariantCulture)} of "
            + $"{selectedPages.ToString(CultureInfo.InvariantCulture)} selected pages (pages {pageList}); any "
            + "reviewer comments those pages carried are not included."));
    }
}

/// <summary>
///     One reviewer remark read from a PDF annotation.
/// </summary>
/// <param name="PageNumber">The 1-based page the annotation sits on.</param>
/// <param name="Author">The name the annotation records, or <see langword="null"/> when it records none.</param>
/// <param name="Body">The remark's text, trimmed and never blank.</param>
/// <remarks>
///     Carries the page number rather than a formatted location so the caller owns the wording of the
///     location hint, which is a presentation decision shared across formats rather than a PDF one.
///     Immutable and thread-safe.
/// </remarks>
internal sealed record PdfReviewAnnotation(int PageNumber, string? Author, string Body);
