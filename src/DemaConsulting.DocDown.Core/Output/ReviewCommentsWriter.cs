using System.Text;

namespace DocDown.Core;

/// <summary>
///     Finalizes <c>review-comments.md</c>, the reviewer commentary a document carries, from the
///     comments the sink recorded during extraction.
/// </summary>
/// <remarks>
///     <para>
///         Reviewer commentary is editorial conversation <em>about</em> a document rather than the
///         document's own content, so it is written to its own artifact instead of being interleaved
///         into <c>content.md</c>. A consumer that wants the document reads <c>content.md</c>; a
///         consumer that wants the review reads this file; neither has to filter the other out.
///     </para>
///     <para>
///         The file is written <strong>only</strong> when the document actually carries reviewer
///         comments. An empty <c>review-comments.md</c> would assert that a review exists and was
///         empty, which is a different claim from "this document was never commented on", so the
///         absence of comments is reported as the absence of the file (and, in <c>summary.txt</c>,
///         as an explicit "not present" line) rather than as an empty document.
///     </para>
///     <para>
///         The writer invents no content: the author, body, and location of each entry come from the
///         extractor verbatim, and entries are emitted in the order the extractor reported them —
///         the same discipline <see cref="ContentWriter"/> applies to parts. The only transformation
///         applied is markdown escaping, which happens here because this is where markdown is
///         written: the extractors report the text as the document records it, and
///         <c>manifest.json</c> carries exactly that, while this file escapes what would otherwise
///         restructure an entry. It performs filesystem
///         I/O through the scratch-folder gate and holds no state, so it is safe to call from the
///         single extraction flow; it is not designed for concurrent invocation against the same
///         folder.
///     </para>
/// </remarks>
public static class ReviewCommentsWriter
{
    /// <summary>The fixed relative name of the review-comments document.</summary>
    /// <remarks>
    ///     Constant because the name is part of the output contract — the one artifact of that
    ///     contract whose <em>presence</em>, but never whose name, varies with the document.
    /// </remarks>
    private const string ReviewCommentsFileName = "review-comments.md";

    /// <summary>The heading the document opens with.</summary>
    /// <remarks>
    ///     Stated once so the file is a self-describing markdown document when read on its own,
    ///     matching how <see cref="ContentWriter"/>'s index layout opens with a heading.
    /// </remarks>
    private const string Heading = "# Review comments";

    /// <summary>The author label used when a comment carries no author.</summary>
    /// <remarks>
    ///     An unattributed comment is reported as unattributed rather than attributed to a guessed
    ///     or invented name, and rather than having its entry silently reshaped.
    /// </remarks>
    private const string UnattributedAuthor = "Unattributed";

    /// <summary>
    ///     Writes the review-comments document when the document carries reviewer comments, and
    ///     writes nothing when it does not.
    /// </summary>
    /// <param name="sink">The sink holding the recorded review comments. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A <see cref="ReviewCommentsWriteResult"/> carrying the relative path of the written file
    ///     and the number of comments it records, or <see cref="ReviewCommentsWriteResult.Path"/> of
    ///     <see langword="null"/> and a <see cref="ReviewCommentsWriteResult.Count"/> of zero when no
    ///     comments were recorded and therefore no file was written.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Writes one markdown list entry per comment in the order the extractor reported them.
    ///     Performs filesystem I/O only when at least one comment was recorded.
    /// </remarks>
    public static async ValueTask<ReviewCommentsWriteResult> WriteAsync(
        ExtractionSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var comments = sink.ReviewComments;

        // No comments means no review happened, which is told by the file's absence rather than by
        // an empty file asserting an empty review
        if (comments.Count == 0)
        {
            return new ReviewCommentsWriteResult(null, 0);
        }

        var builder = new StringBuilder();
        builder.Append(Heading).Append('\n').Append('\n');
        foreach (var comment in comments)
        {
            AppendEntry(builder, comment);
        }

        await sink.Folder.WriteTextAsync(ReviewCommentsFileName, builder.ToString(), cancellationToken)
            .ConfigureAwait(false);
        return new ReviewCommentsWriteResult(ReviewCommentsFileName, comments.Count);
    }

    /// <summary>
    ///     Appends one comment as a single markdown list entry.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="comment">The comment to render.</param>
    /// <remarks>
    ///     The shape is <c>- **Author** (Location): Body</c>: the author is emphasized so a reader
    ///     scanning the file can follow one reviewer's thread, and the location precedes the body so
    ///     the entry reads as a statement about a place in the document. Every field is escaped on
    ///     the way in, because this is where markdown is written. Pure.
    /// </remarks>
    private static void AppendEntry(StringBuilder builder, DocumentComment comment)
    {
        var author = string.IsNullOrWhiteSpace(comment.Author) ? UnattributedAuthor : comment.Author;
        builder.Append("- **").Append(EscapeInline(author)).Append("** (")
            .Append(EscapeInline(comment.Location)).Append("): ")
            .Append(EscapeInline(Flatten(comment.Body))).Append('\n');
    }

    /// <summary>The literal characters escaped in an entry's author, location, and body.</summary>
    /// <remarks>
    ///     <para>
    ///         Deliberately the inline-structural set only. Each entry's text sits mid-line, after
    ///         <c>): </c>, where a <c>#</c>, a <c>-</c>, a <c>|</c>, or a trailing <c>.</c> cannot
    ///         take effect as a heading, a list marker, a table separator, or an ordered-list
    ///         number — so escaping them would litter the output with backslashes that mean nothing,
    ///         which is precisely what made one backend's comment text read as
    ///         <c>Please clarify the scope\.</c>. What <em>can</em> restructure an entry from the
    ///         middle of a line is emphasis, code spans, link syntax, inline HTML, and the escape
    ///         character itself, so exactly those are escaped.
    ///     </para>
    ///     <para>
    ///         The backslash is listed first in intent as well as position: escaping it keeps a body
    ///         that genuinely contains one from producing a different escape in the output.
    ///     </para>
    /// </remarks>
    private static readonly HashSet<char> EscapedCharacters = ['\\', '`', '*', '_', '[', ']', '<'];

    /// <summary>
    ///     Escapes the markdown-significant characters that could restructure an entry.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The text with each significant character backslash-escaped.</returns>
    /// <remarks>
    ///     <para>
    ///         Escaping happens here, once, where markdown is written, rather than in each backend.
    ///         That keeps every backend reporting the comment text <em>as the document records
    ///         it</em> — which is what <c>manifest.json</c> promises its consumers — while still
    ///         producing a <c>review-comments.md</c> a markdown parser reads as one entry per
    ///         comment. A reviewer who wrote <c>*urgent*</c> gets those asterisks back, rather than
    ///         emphasis they never asked for.
    ///     </para>
    ///     <para>Pure.</para>
    /// </remarks>
    private static string EscapeInline(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            if (EscapedCharacters.Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Flattens a comment body onto a single line.
    /// </summary>
    /// <param name="body">The comment body as the extractor reported it.</param>
    /// <returns>The body with its line breaks replaced by single spaces.</returns>
    /// <remarks>
    ///     A multi-line body would otherwise break out of its list entry and read as separate
    ///     document text, so the line breaks (not the words) are normalized away; nothing is
    ///     truncated or summarized. Pure.
    /// </remarks>
    private static string Flatten(string body)
    {
        // Normalize CRLF first so a Windows-authored body does not collapse into a double space
        var flattened = body.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return flattened.Trim();
    }
}

/// <summary>
///     The outcome of finalizing the review-comments document: whether one was written and how many
///     comments it records.
/// </summary>
/// <param name="Path">
///     The relative path of the review-comments document (<c>review-comments.md</c>), or
///     <see langword="null"/> when the document carried no reviewer comments and no file was
///     written.
/// </param>
/// <param name="Count">
///     The number of reviewer comments written, which is zero exactly when
///     <paramref name="Path"/> is <see langword="null"/>.
/// </param>
/// <remarks>
///     Returned to the engine so it can populate the result, and to <see cref="ManifestWriter"/> and
///     <see cref="SummaryWriter"/> so the manifest's accounted paths and the summary's layout block
///     reflect exactly what <see cref="ReviewCommentsWriter"/> produced. Immutable and thread-safe.
/// </remarks>
public sealed record ReviewCommentsWriteResult(string? Path, int Count);

/// <summary>
///     One reviewer comment or annotation a document carries, as an extractor reports it.
/// </summary>
/// <param name="Author">
///     The name the document records for the comment's author, or <see langword="null"/> when the
///     document records none. Never guessed from other metadata.
/// </param>
/// <param name="Body">The comment text as the document records it. Must not be null or blank.</param>
/// <param name="Location">
///     A single, precomputed, human-readable display string naming where in the document the comment
///     sits. Must not be null or blank.
/// </param>
/// <remarks>
///     <para>
///         <paramref name="Location"/> is deliberately one display string rather than a structured
///         address. The in-scope formats locate a comment in genuinely different terms — Word by the
///         enclosing heading and the commented-on words, Excel by a sheet and cell, PowerPoint and
///         PDF by an ordinal — so any shared structure would be mostly-null fields that each consumer
///         would have to re-render anyway. The backend that knows the format composes the phrase
///         once, in its own vocabulary, and Core carries it verbatim.
///     </para>
///     <para>
///         The per-format conventions are: Word <c>§Heading — "snippet…"</c>; Excel
///         <c>Sheet1!B7</c>; PowerPoint <c>Slide {n}</c>; PDF <c>Page {n}</c>. A backend that cannot
///         resolve a location states that plainly in the same string rather than reporting a blank.
///     </para>
///     <para>Instances are immutable and thread-safe.</para>
/// </remarks>
public sealed record DocumentComment(string? Author, string Body, string Location);
