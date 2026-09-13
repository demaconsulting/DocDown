using System.Text.RegularExpressions;

namespace DocDown.Core;

/// <summary>
///     Rewrites the root-relative resource links an extractor emits (<c>images/…</c>, <c>pages/…</c>)
///     so they resolve from a part file's own location under <c>parts/</c>.
/// </summary>
/// <remarks>
///     <para>
///         Extractors emit the sink-allocated, root-relative resource path (for example
///         <c>![alt](images/0001-image.png)</c>) because path allocation belongs to the sink, not the
///         extractor. That path resolves correctly from <c>content.md</c> at the scratch root, but a
///         part file materialized under <c>parts/</c> is one directory deeper, so a Markdown renderer
///         (or an LLM agent) resolves <c>images/…</c> relative to <c>parts/</c> — a folder that does
///         not exist. Adjusting the root-relative path for the destination file's location is a
///         path concern, and path allocation belongs to the sole write path, so this rewrite runs
///         there rather than in each emitter.
///     </para>
///     <para>
///         The rewrite is deliberately narrow: it only touches Markdown link targets that begin with
///         one of the sink's resource-folder prefixes (<c>images/</c>, <c>pages/</c>), for both the
///         image (<c>![alt](…)</c>) and plain-link (<c>[text](…)</c>) forms. External targets
///         (<c>http(s):</c>, <c>mailto:</c>), in-document anchors (<c>#…</c>), and targets that are
///         already relative (<c>../</c>, <c>./</c>) are left untouched. The class is a pure function
///         with no I/O and holds no state, so it is safe to call from the single write path.
///     </para>
/// </remarks>
internal static class PartResourceLinkRewriter
{
    /// <summary>The sink resource-folder prefixes whose links are rewritten, mirroring the sink's folders.</summary>
    /// <remarks>
    ///     These are exactly the folders <see cref="ExtractionSink"/> allocates resource paths into
    ///     (<c>images/</c> for embedded images, <c>pages/</c> for rendered pages); anything else is a
    ///     content link the rewrite must not disturb.
    /// </remarks>
    private const string ResourceFolders = "images/|pages/";

    /// <summary>
    ///     Matches a Markdown link opener immediately followed by a root-relative resource-folder path.
    /// </summary>
    /// <remarks>
    ///     Group 1 captures the opener (<c>[text](</c> or <c>![alt](</c>) and group 2 captures the
    ///     resource folder prefix; the replacement re-emits the opener, inserts the computed
    ///     <c>../</c> prefix, and re-emits the folder so only the path root moves. Anchoring to
    ///     <c>](</c> keeps the match to real link targets and never fires on prose that merely
    ///     mentions <c>images/</c>. Compiled because the write path can run it once per part file.
    ///     A match timeout is supplied because this pattern runs over document-derived content: the
    ///     pattern itself has no nested quantifier and so carries no realistic backtracking risk, but
    ///     an explicit bound keeps that property from depending on the pattern staying simple.
    /// </remarks>
    private static readonly Regex ResourceLinkPattern = new(
        @"(!?\[[^\]]*\]\()(" + ResourceFolders + ")",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    /// <summary>
    ///     Returns <paramref name="markdown"/> with every root-relative resource link made relative to
    ///     the directory that holds the part file named by <paramref name="partRelativePath"/>.
    /// </summary>
    /// <param name="markdown">The part's markdown body. Must not be null.</param>
    /// <param name="partRelativePath">
    ///     The forward-slash, scratch-root-relative path the sink allocated for the part file (for
    ///     example <c>parts/0001-sheet-budget.md</c>). Must not be null. Its directory depth (the
    ///     number of path segments before the file name) determines how many <c>../</c> levels are
    ///     inserted, so the rewrite stays correct even if the part layout is ever nested; today the
    ///     flat <c>parts/&lt;name&gt;.md</c> layout always yields exactly one <c>../</c>.
    /// </param>
    /// <returns>
    ///     The markdown with resource links prefixed by the required <c>../</c> levels; the original
    ///     text unchanged when the part sits at the root (depth zero) or carries no resource links.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="markdown"/> or <paramref name="partRelativePath"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Pure and side-effect free: no filesystem, network, or shared state. Only targets beginning
    ///     with a sink resource folder are moved; <c>http(s):</c>, <c>mailto:</c>, <c>#anchor</c>, and
    ///     already-relative targets never match the pattern and so pass through verbatim, which also
    ///     makes the rewrite idempotent for already-relative content.
    /// </remarks>
    public static string Rewrite(string markdown, string partRelativePath)
    {
        // A part path and body are always supplied by the write path; guard so a null fails clearly
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(partRelativePath);

        // Depth is the number of directories above the part file; a root-level part needs no prefix
        var depth = DirectoryDepth(partRelativePath);
        if (depth == 0)
        {
            return markdown;
        }

        // One "../" per directory level reaches back up to the scratch root before the resource folder
        var prefix = string.Concat(Enumerable.Repeat("../", depth));

        // Re-emit each matched opener and resource folder with the "../" prefix spliced between them
        return ResourceLinkPattern.Replace(markdown, match => match.Groups[1].Value + prefix + match.Groups[2].Value);
    }

    /// <summary>
    ///     Counts the directory levels above the file named by a forward-slash relative path.
    /// </summary>
    /// <param name="relativePath">The forward-slash relative path whose depth is measured.</param>
    /// <returns>The number of path segments before the file name; zero for a root-level file.</returns>
    /// <remarks>
    ///     Splits on the forward slash the sink always uses and drops empty segments so a stray leading
    ///     or doubled slash cannot inflate the count. The final segment is the file name and is not a
    ///     directory level, so the depth is the remaining segment count.
    /// </remarks>
    private static int DirectoryDepth(string relativePath)
    {
        // Keep only real segments; the last is the file name, so the rest are the directory levels
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length <= 1 ? 0 : segments.Length - 1;
    }
}
