using System.Globalization;
using System.Text;

namespace DocDown.Core;

/// <summary>
///     Finalizes <c>content.md</c> and, when the layout calls for it, the per-part files under
///     <c>parts/</c>, from the content the sink buffered during extraction.
/// </summary>
/// <remarks>
///     <para>
///         <c>content.md</c> is always the single entry point, but its shape depends on the content
///         and the requested split mode: a single-flow document writes its whole text (with the
///         extractor's <c>&lt;!-- docdown:page N --&gt;</c> markers passed through untouched); a
///         multi-part document writes an index that links each part; and
///         <see cref="ContentSplitMode.Single"/> concatenates the parts under headings with no
///         <c>parts/</c> folder. Deferring this decision to write time is why the sink buffers parts
///         instead of writing them eagerly.
///     </para>
///     <para>
///         The writer invents no content: page markers originate in the extractor's markdown, and
///         headings for concatenated parts use the part titles the extractor supplied. It performs
///         filesystem I/O through the scratch-folder gate and holds no state, so it is safe to call
///         from the single extraction flow; it is not designed for concurrent invocation against the
///         same folder.
///     </para>
/// </remarks>
public static class ContentWriter
{
    /// <summary>The fixed relative name of the content entry-point document.</summary>
    /// <remarks>Constant because the name is part of the invariant output contract and never varies.</remarks>
    private const string ContentFileName = "content.md";

    /// <summary>
    ///     Finalizes the content document and any part files, returning what was written.
    /// </summary>
    /// <param name="sink">The sink holding the buffered content and parts. Must not be null.</param>
    /// <param name="mode">The content split mode governing the output shape.</param>
    /// <param name="documentTitle">
    ///     The document title used for the index heading, or <see langword="null"/> to fall back to
    ///     the sink's reported document title and then to a generic label.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A <see cref="ContentWriteResult"/> describing the content path, whether any text was
    ///     produced, the character count, and the relative paths of any part files written.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Chooses the single-flow layout whenever no parts were buffered (so buffered single content
    ///     is never lost), the concatenated layout for <see cref="ContentSplitMode.Single"/> with
    ///     parts, and the index layout otherwise. Always writes <c>content.md</c>. Performs
    ///     filesystem I/O.
    /// </remarks>
    public static async ValueTask<ContentWriteResult> WriteAsync(
        ExtractionSink sink, ContentSplitMode mode, string? documentTitle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var folder = sink.Folder;
        var parts = sink.Parts;
        var title = ResolveTitle(documentTitle, sink);

        // With no parts there is nothing to split, so a single flow preserves any buffered content
        if (parts.Count == 0)
        {
            return await WriteSingleFlowAsync(folder, sink.BufferedContent, cancellationToken).ConfigureAwait(false);
        }

        // Single mode concatenates parts into one document; the other modes index separate part files
        return mode == ContentSplitMode.Single
            ? await WriteConcatenatedAsync(folder, parts, cancellationToken).ConfigureAwait(false)
            : await WriteIndexAsync(folder, parts, title, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes a single-flow <c>content.md</c> containing the buffered markdown verbatim.
    /// </summary>
    /// <param name="folder">The scratch folder to write into.</param>
    /// <param name="content">The buffered single-flow markdown.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write result for the single-flow layout.</returns>
    /// <remarks>
    ///     Page markers embedded by the extractor are preserved because the content is written
    ///     unchanged. Presence is judged on non-whitespace content so an empty flow is honestly
    ///     reported as absent text.
    /// </remarks>
    private static async ValueTask<ContentWriteResult> WriteSingleFlowAsync(
        ScratchFolder folder, string content, CancellationToken cancellationToken)
    {
        // Write the flow exactly as buffered; the extractor owns markers and formatting
        await folder.WriteTextAsync(ContentFileName, content, cancellationToken).ConfigureAwait(false);
        var present = !string.IsNullOrWhiteSpace(content);
        return new ContentWriteResult(ContentFileName, present, content.Length, []);
    }

    /// <summary>
    ///     Writes a concatenated <c>content.md</c> with each part under a <c>##</c> heading and no
    ///     <c>parts/</c> folder.
    /// </summary>
    /// <param name="folder">The scratch folder to write into.</param>
    /// <param name="parts">The buffered parts in document order.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write result for the concatenated layout.</returns>
    /// <remarks>
    ///     Used for <see cref="ContentSplitMode.Single"/>: the parts become sections of one document,
    ///     which keeps small multi-part documents in a single readable file. Part markdown, including
    ///     any page markers, is passed through unchanged.
    /// </remarks>
    private static async ValueTask<ContentWriteResult> WriteConcatenatedAsync(
        ScratchFolder folder, IReadOnlyList<RecordedPart> parts, CancellationToken cancellationToken)
    {
        // Build one document by heading each part and appending its body
        var builder = new StringBuilder();
        var characters = 0;
        foreach (var part in parts)
        {
            builder.Append("## ").Append(HeadingFor(part)).Append('\n').Append('\n');
            builder.Append(part.Markdown.TrimEnd('\n')).Append('\n').Append('\n');
            characters += part.CharacterCount;
        }

        var text = builder.ToString().TrimEnd('\n') + "\n";
        await folder.WriteTextAsync(ContentFileName, text, cancellationToken).ConfigureAwait(false);
        return new ContentWriteResult(ContentFileName, characters > 0, characters, []);
    }

    /// <summary>
    ///     Writes the index <c>content.md</c> plus one file per part under <c>parts/</c>.
    /// </summary>
    /// <param name="folder">The scratch folder to write into.</param>
    /// <param name="parts">The buffered parts in document order.</param>
    /// <param name="title">The document title for the index heading.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write result for the index layout, listing the part paths written.</returns>
    /// <remarks>
    ///     Used for <see cref="ContentSplitMode.Auto"/> with parts and for
    ///     <see cref="ContentSplitMode.PerPart"/>: the index keeps a large, sectioned document
    ///     navigable and lets a consumer read parts selectively. Each part file is written through the
    ///     scratch-folder gate at the path the sink allocated. Because a part file lives under
    ///     <c>parts/</c> rather than at the scratch root, the extractor's root-relative resource links
    ///     (<c>images/…</c>, <c>pages/…</c>) are rewritten by <see cref="PartResourceLinkRewriter"/> to
    ///     the equivalent <c>../</c>-prefixed path so they resolve on disk from the part's own
    ///     directory; only this layout applies the rewrite, since the single-flow and concatenated
    ///     layouts write <c>content.md</c> at the root where the original links are already correct.
    ///     The reported <see cref="ContentWriteResult.CharacterCount"/> still reflects the extractor
    ///     body the sink buffered — the write-time <c>../</c> prefix is a path adjustment, not content.
    /// </remarks>
    private static async ValueTask<ContentWriteResult> WriteIndexAsync(
        ScratchFolder folder, IReadOnlyList<RecordedPart> parts, string title, CancellationToken cancellationToken)
    {
        // Write each part to its own file so the index links resolve on disk, rewriting the extractor's
        // root-relative resource links to the part's directory so they resolve from parts/ as well
        var partPaths = new List<string>(parts.Count);
        var characters = 0;
        foreach (var part in parts)
        {
            var markdown = PartResourceLinkRewriter.Rewrite(part.Markdown, part.Path);
            await folder.WriteTextAsync(part.Path, markdown, cancellationToken).ConfigureAwait(false);
            partPaths.Add(part.Path);
            characters += part.CharacterCount;
        }

        // Build the index: a title, a part-count line, then a document-ordered list of links
        var builder = new StringBuilder();
        builder.Append("# ").Append(title).Append('\n').Append('\n');
        builder.Append(parts.Count.ToString(CultureInfo.InvariantCulture))
            .Append(parts.Count == 1 ? " part" : " parts").Append('\n').Append('\n');
        foreach (var part in parts)
        {
            builder.Append("- [").Append(HeadingFor(part)).Append("](").Append(part.Path).Append(")\n");
        }

        await folder.WriteTextAsync(ContentFileName, builder.ToString(), cancellationToken).ConfigureAwait(false);
        return new ContentWriteResult(ContentFileName, true, characters, partPaths);
    }

    /// <summary>
    ///     Resolves the document title used for the index heading.
    /// </summary>
    /// <param name="documentTitle">The explicitly supplied title, or <see langword="null"/>.</param>
    /// <param name="sink">The sink whose reported document info provides a fallback title.</param>
    /// <returns>A non-empty title.</returns>
    /// <remarks>
    ///     Prefers the caller-supplied title, then the extractor's reported document title, then a
    ///     generic label, so the index always has a heading even when metadata is unknown.
    /// </remarks>
    private static string ResolveTitle(string? documentTitle, ExtractionSink sink)
    {
        // Fall through the available sources so a missing title never yields an empty heading
        if (!string.IsNullOrWhiteSpace(documentTitle))
        {
            return documentTitle;
        }

        var reported = sink.DocumentInfo?.Title;
        return string.IsNullOrWhiteSpace(reported) ? "Document" : reported;
    }

    /// <summary>
    ///     Produces the display heading for a part.
    /// </summary>
    /// <param name="part">The part to label.</param>
    /// <returns>The part title when present; otherwise a kind-and-ordinal label.</returns>
    /// <remarks>
    ///     A titleless part still gets a meaningful, deterministic heading (for example
    ///     <c>Sheet 2</c>) so index links and section headings are never blank.
    /// </remarks>
    private static string HeadingFor(RecordedPart part)
    {
        // Prefer the extractor's title; fall back to a capitalized kind and the stable ordinal
        if (!string.IsNullOrWhiteSpace(part.Title))
        {
            return part.Title;
        }

        var kind = part.Kind.Length == 0
            ? "Part"
            : char.ToUpperInvariant(part.Kind[0]) + part.Kind[1..];
        return $"{kind} {part.Ordinal.ToString(CultureInfo.InvariantCulture)}";
    }
}

/// <summary>
///     The outcome of finalizing the content document: what was written and how much.
/// </summary>
/// <param name="ContentPath">
///     The relative path of the content document (<c>content.md</c>), or <see langword="null"/> when
///     no content document was written.
/// </param>
/// <param name="ContentPresent">
///     <see langword="true"/> when the content document carries actual textual content; used to set
///     the content ledger status.
/// </param>
/// <param name="CharacterCount">The total number of characters of textual content produced.</param>
/// <param name="PartPaths">
///     The relative paths of the part files written under <c>parts/</c>, empty when the layout did
///     not split content into files.
/// </param>
/// <remarks>
///     Returned to the engine so it can populate the result and to <see cref="ManifestWriter"/> and
///     <see cref="SummaryWriter"/> so the ledger and summary reflect exactly what
///     <see cref="ContentWriter"/> produced. Immutable and thread-safe.
/// </remarks>
public sealed record ContentWriteResult(
    string? ContentPath, bool ContentPresent, int CharacterCount, IReadOnlyList<string> PartPaths);
