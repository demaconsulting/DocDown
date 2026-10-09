using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.TestSupport;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ReviewCommentsWriter"/>, proving the conditional artifact is written
///     only when a document carries reviewer comments, that entries are rendered in the order the
///     extractor reported them, that an unattributed comment is reported as unattributed, and that a
///     multi-line body stays inside its entry.
/// </summary>
/// <remarks>
///     These tests drive a real <see cref="ExtractionSink"/> (the writer's documented source of
///     recorded comments) over a prepared <see cref="ScratchFolder"/>, finalize with
///     <see cref="ReviewCommentsWriter.WriteAsync"/>, and reconcile the reported result against what
///     is actually on disk, so a claimed path is never believed without the file behind it.
/// </remarks>
public class ReviewCommentsWriterTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a document with no reviewer comments produces no file and an honest empty result.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_NoComments_WritesNoFileAndReportsNone()
    {
        // Arrange: a sink on which no reviewer comment was ever reported
        using var temp = new TempScratch();
        var sink = NewSink(temp);

        // Act: finalize the review-comments document
        var result = await ReviewCommentsWriter.WriteAsync(sink, Ct);

        // Assert: nothing is claimed and nothing is on disk
        Assert.Null(result.Path);
        Assert.Equal(0, result.Count);
        Assert.False(File.Exists(ReviewCommentsFile(sink)));
    }

    /// <summary>
    ///     Proves a single reported comment produces the document with its one entry.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_SingleComment_WritesDocumentWithEntry()
    {
        // Arrange: a sink holding one reviewer comment
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Ada", "Clarify this paragraph.", "\u00a7Overview \u2014 \"the system\u2026\""));

        // Act: finalize the review-comments document
        var result = await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the artifact is claimed, counted, headed, and carries the entry verbatim
        Assert.Equal("review-comments.md", result.Path);
        Assert.Equal(1, result.Count);
        Assert.StartsWith("# Review comments\n\n", document, StringComparison.Ordinal);
        Assert.Contains(
            "- **Ada** (\u00a7Overview \u2014 \"the system\u2026\"): Clarify this paragraph.\n",
            document,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves many reported comments are written in the order the extractor reported them.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_ManyComments_WritesEntriesInReportOrder()
    {
        // Arrange: a sink holding three comments reported in a deliberate, non-alphabetical order
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Zoe", "First remark.", "Page 1"));
        sink.ReportReviewComment(new DocumentComment("Ada", "Second remark.", "Page 2"));
        sink.ReportReviewComment(new DocumentComment("Ada", "Third remark.", "Page 3"));

        // Act: finalize the review-comments document
        var result = await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var lines = (await ReadReviewCommentsAsync(sink)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert: every comment is present, counted, and in report order rather than re-sorted
        Assert.Equal(3, result.Count);
        Assert.Equal("# Review comments", lines[0]);
        Assert.Equal("- **Zoe** (Page 1): First remark.", lines[1]);
        Assert.Equal("- **Ada** (Page 2): Second remark.", lines[2]);
        Assert.Equal("- **Ada** (Page 3): Third remark.", lines[3]);
    }

    /// <summary>
    ///     Proves a comment the document leaves unattributed is reported as unattributed.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_CommentWithoutAuthor_ReportsUnattributed()
    {
        // Arrange: a sink holding a comment the document records no author for
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment(null, "Who wrote this?", "Slide 4"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the absent author is stated, not guessed and not silently dropped
        Assert.Contains("- **Unattributed** (Slide 4): Who wrote this?\n", document, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a multi-line comment body stays within its single markdown entry.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_MultiLineBody_KeepsEntryOnOneLine()
    {
        // Arrange: a sink holding a comment whose body spans lines in both line-ending conventions
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Ada", "First line.\r\nSecond line.\nThird line.", "Sheet1!B7"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the words survive on a single entry line, so the body cannot read as document text
        Assert.Contains(
            "- **Ada** (Sheet1!B7): First line. Second line. Third line.\n",
            document,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a body with leading or trailing spaces reaches the file unchanged, because
    ///     flattening normalizes line breaks and must not also trim significant whitespace an
    ///     extractor reported verbatim.
    /// </summary>
    /// <remarks>
    ///     A backend's verbatim-body contract (for example, a PDF annotation's padded
    ///     <c>/Contents</c> entry) is only honored end-to-end if this writer does not quietly
    ///     take back whitespace the extractor deliberately preserved.
    /// </remarks>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_PaddedBody_PreservesLeadingAndTrailingSpaces()
    {
        // Arrange: a sink holding a comment whose body carries deliberate leading/trailing spaces
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Ada", "  padded on both sides  ", "Page 1"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the padding survives exactly as reported, not trimmed away
        Assert.Contains(
            "- **Ada** (Page 1):   padded on both sides  \n",
            document,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a multi-line author or location stays within its single markdown entry, not only a
    ///     multi-line body.
    /// </summary>
    /// <remarks>
    ///     A document-provided author or a Word heading can itself carry a line break, which would
    ///     otherwise split one comment across multiple markdown lines just as surely as a multi-line
    ///     body would.
    /// </remarks>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_MultiLineAuthorAndLocation_KeepsEntryOnOneLine()
    {
        // Arrange: a sink holding a comment whose author and location both span lines
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Ada\nLovelace", "Clarify this.", "\u00a7Overview\r\n\u2014 part two"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the entry stays on one line, with the author and location flattened like the body
        Assert.Contains(
            "- **Ada Lovelace** (\u00a7Overview \u2014 part two): Clarify this.\n",
            document,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves markdown-significant characters in a body, author, or location are escaped where
    ///     the markdown is written, while what the extractor reported stays exactly as the document
    ///     recorded it.
    /// </summary>
    /// <remarks>
    ///     The two artifacts make different promises and both must be kept. <c>manifest.json</c>
    ///     promises the comment text as the document records it, so the sink's recorded body carries
    ///     a reviewer's literal <c>*urgent*</c>. <c>review-comments.md</c> is markdown, so the same
    ///     text must not read there as emphasis nobody asked for. Escaping here, once, is what lets
    ///     every backend report raw text; asserting both halves in one test is what keeps a future
    ///     change from satisfying one promise by breaking the other.
    ///     <para>
    ///         <c>&amp;</c> and <c>~</c> are covered too: neither restructures the list, but an
    ///         unescaped <c>&amp;amp;</c> would render as a bare <c>&amp;</c> and an unescaped
    ///         <c>~~x~~</c> struck through, either of which silently alters a reviewer's words.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_MarkdownCharacters_EscapedInFileButNotInRecord()
    {
        // Arrange: a comment whose author, location, and body all carry markdown-significant text
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment(
            "A*da", "This is *urgent* — see `scope` and [1] \\ <tag> &amp; ~~old~~.", "§Scope_2"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the written markdown escapes what could restructure or re-render the entry
        Assert.Contains(
            "- **A\\*da** (§Scope\\_2): This is \\*urgent\\* — see \\`scope\\` and \\[1\\] \\\\ \\<tag> "
            + "\\&amp; \\~\\~old\\~\\~.\n",
            document,
            StringComparison.Ordinal);

        // Assert: and what the extractor reported — which is what the manifest carries — is untouched
        var recorded = Assert.Single(sink.ReviewComments);
        Assert.Equal("This is *urgent* — see `scope` and [1] \\ <tag> &amp; ~~old~~.", recorded.Body);
        Assert.Equal("A*da", recorded.Author);
        Assert.Equal("§Scope_2", recorded.Location);
    }

    /// <summary>
    ///     Proves text carrying no markdown-significant character is written through unchanged, so
    ///     the escaping adds no backslashes of its own.
    /// </summary>
    /// <remarks>
    ///     The escape set is deliberately the inline-structural one. A body sits mid-line, where a
    ///     <c>#</c>, a <c>-</c>, a <c>|</c>, or a trailing <c>.</c> cannot take effect, and escaping
    ///     them is exactly what once made a comment read as <c>Please clarify the scope\.</c>.
    /// </remarks>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_StructuralCharacters_AreNotEscaped()
    {
        // Arrange: a comment carrying characters that are structural only at the start of a line
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        sink.ReportReviewComment(new DocumentComment("Ada", "Please clarify the scope.", "Sheet1!B7"));

        // Act: finalize the review-comments document
        await ReviewCommentsWriter.WriteAsync(sink, Ct);
        var document = await ReadReviewCommentsAsync(sink);

        // Assert: the entry reads as the reviewer wrote it, with no backslashes added
        Assert.Contains(
            "- **Ada** (Sheet1!B7): Please clarify the scope.\n", document, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", document, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a null sink is rejected rather than producing a partial artifact.
    /// </summary>
    [Fact]
    public async Task ReviewCommentsWriter_WriteAsync_NullSink_Throws()
    {
        // Arrange / Act / Assert: the missing source of comments is refused at the boundary
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ReviewCommentsWriter.WriteAsync(null!, Ct));
    }

    /// <summary>
    ///     Creates a sink over a freshly prepared scratch folder.
    /// </summary>
    /// <param name="temp">The temporary folder that owns the scratch folder.</param>
    /// <returns>A sink ready to record comments and finalize them.</returns>
    /// <remarks>Uses a real folder because the writer's contract includes what lands on disk.</remarks>
    private static ExtractionSink NewSink(TempScratch temp)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        return new ExtractionSink(folder, new ExtractionOptions());
    }

    /// <summary>
    ///     Resolves the absolute path the review-comments document would occupy.
    /// </summary>
    /// <param name="sink">The sink whose folder holds the document.</param>
    /// <returns>The absolute path of <c>review-comments.md</c>, whether or not it exists.</returns>
    /// <remarks>Used to prove the file's absence as well as its presence.</remarks>
    private static string ReviewCommentsFile(ExtractionSink sink) =>
        Path.Combine(sink.Folder.AbsolutePath, "review-comments.md");

    /// <summary>
    ///     Reads the finalized review-comments document from the sink's scratch folder.
    /// </summary>
    /// <param name="sink">The sink whose folder holds the document.</param>
    /// <returns>The text of <c>review-comments.md</c>.</returns>
    /// <remarks>Reads the exact bytes the writer produced so assertions see the on-disk form.</remarks>
    private static async Task<string> ReadReviewCommentsAsync(ExtractionSink sink) =>
        await File.ReadAllTextAsync(ReviewCommentsFile(sink), Ct);
}
