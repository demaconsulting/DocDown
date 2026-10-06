using DemaConsulting.DocDown.Office.Tests.Word.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Word;
using DocDown.Word.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Word;

/// <summary>
///     System-level integration tests for the DocDownWord extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against documents generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real document and confirms the invariant layout is
///     written, so reported notes and inventory match what is on disk.
/// </remarks>
public class DocDownWordTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a generated document produces the full contract layout with a real table and a
    ///     document-control section, and no incomplete-step notes.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_GeneratedDocx_ProducesContractLayout()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "clean.docx", DocxFixtures.CleanDocument());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);
        Assert.Equal("word-openxml", result.SelectedExtractor?.Id);

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("| --- | --- |", content, StringComparison.Ordinal);
        Assert.Contains("## Document Control", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the summary's content outline makes a document's author-attributed comments
    ///     discoverable, rather than reporting only how many characters the content runs to.
    /// </summary>
    /// <remarks>
    ///     This is the defect that prompted the change: an agent asked to find reviewer commentary in
    ///     a draft carrying dozens of author-attributed comments went to an entirely different
    ///     document and answered by inference, because <c>summary.txt</c> said nothing but
    ///     "36,509 characters of text". The outline must name the comments and their comment authors.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithComment_SummaryOutlineNamesTheComments()
    {
        // Arrange / Act: extract a document carrying one author-attributed comment
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "commented.docx", DocxFixtures.DocumentWithComment());

        // Assert: the summary's content outline names the comment and its comment author, and the
        // manifest carries the same counts for a machine reader
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("1 comment", summary, StringComparison.Ordinal);
        Assert.Contains("1 distinct comment author", summary, StringComparison.Ordinal);

        var manifest = await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct);
        Assert.Contains("\"contentFeatures\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"comments\"", manifest, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a document's reviewer comments reach the dedicated review-comments artifact with
    ///     their location hints, and leave the extracted content untouched.
    /// </summary>
    /// <remarks>
    ///     Comments used to be appended to <c>content.md</c> under a <c>## Comments</c> heading, which
    ///     left a consumer unable to tell the author's words from a reviewer's. This drives the whole
    ///     engine so the move is proven where it is observable: on disk.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithComment_WritesReviewCommentsNotContent()
    {
        // Arrange / Act: extract a document whose single comment is anchored under a heading
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "commented.docx", DocxFixtures.DocumentWithComment());

        // Assert: the comment is in review-comments.md with its author and location hint
        var reviewComments = await File.ReadAllTextAsync(Path.Combine(scratch, "review-comments.md"), Ct);
        Assert.Contains("**Reviewer**", reviewComments, StringComparison.Ordinal);
        Assert.Contains("§Overview", reviewComments, StringComparison.Ordinal);
        Assert.Contains("clarify", reviewComments, StringComparison.Ordinal);

        // Assert: and nowhere in the extracted content
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.DoesNotContain("## Comments", content, StringComparison.Ordinal);
        Assert.DoesNotContain("clarify", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a document carrying a comment with no text still extracts completely, and that the
    ///     document's real comment still reaches the review-comments artifact.
    /// </summary>
    /// <remarks>
    ///     This is the regression guard for a defect that destroyed whole extractions: an empty
    ///     <c>w:comment</c> is schema-valid and arises whenever a reviewer inserts a comment and
    ///     types nothing, but it reached Core as a blank-bodied review comment, Core rejected it by
    ///     contract, and the fault propagated until the document was reported
    ///     <see cref="ExtractionOutcome.Unreadable"/> with no <c>content.md</c> at all. A trivial
    ///     unextractable item must never cost the content that extracted perfectly, so this drives
    ///     the whole engine and asserts on what is on disk.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithEmptyComment_StillProducesContentAndRealComment()
    {
        // Arrange / Act: extract a document whose two comments are one real remark and one empty one
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(
            temp, "partly-commented.docx", DocxFixtures.DocumentWithEmptyAndRealComments());

        // Assert: the extraction produced output rather than failing on the empty comment
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Body text that must survive extraction", content, StringComparison.Ordinal);

        // Assert: the real comment still reached the artifact, and the empty one left no entry
        var reviewComments = await File.ReadAllTextAsync(Path.Combine(scratch, "review-comments.md"), Ct);
        Assert.Contains("**Reviewer**", reviewComments, StringComparison.Ordinal);
        Assert.Contains("clarify", reviewComments, StringComparison.Ordinal);
        Assert.DoesNotContain("Silent Reviewer", reviewComments, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves embedded images are written and linked from the content.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "images.docx", DocxFixtures.DocumentWithImage());

        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        var written = Assert.Single(images);
        Assert.EndsWith(".png", written, StringComparison.Ordinal);
        Assert.Equal(DocxFixtures.PngBytes(), await File.ReadAllBytesAsync(written, Ct));

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("](images/", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves an engineering-style document preserves its revision in the document-control section.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_EngineeringStyleDocx_PreservesRevisionInDocumentControl()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "engineering.docx", DocxFixtures.EngineeringStyleDocument());

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("## Document Control", content, StringComparison.Ordinal);
        Assert.Contains("Revision", content, StringComparison.Ordinal);
        Assert.Contains("CONFIDENTIAL", content, StringComparison.Ordinal);

        // Page-number furniture is omitted silently; that commentary is no longer surfaced as a note
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);
        Assert.DoesNotContain("Page 1 of", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a document embedding a chart reports it as a note naming the loss, instead of
    ///     dropping a chart that carries no image blip and would otherwise leave no trace at all.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_DocxWithChart_ReportsChartNote()
    {
        // Arrange / Act: extract a document carrying one embedded chart
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "charted.docx", DocxFixtures.DocumentWithChart());

        // Assert: the chart is counted and explained rather than silently absent
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Contains(result.Notes, note => note.Message.Contains("1 charts", StringComparison.Ordinal));
        Assert.Contains(result.Notes, note => note.Message.Contains("does not read chart parts", StringComparison.Ordinal));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the Open XML backend is the one selected for a modern document.
    /// </summary>
    [Fact]
    public async Task DocDownWord_Extract_Docx_SelectsOpenXml()
    {
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, "clean.docx", DocxFixtures.CleanDocument());
        var scratch = Path.Combine(temp.Path, "out");

        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);

        Assert.Equal("word-openxml", result.SelectedExtractor?.Id);
    }

    /// <summary>
    ///     Proves a legacy binary document fails with a structured refusal whose prose states plainly
    ///     that the format is unsupported.
    /// </summary>
    /// <remarks>
    ///     This package reads Open XML documents and nothing else, so a <c>.doc</c> has no reader
    ///     here and never will. The value of the test is the shape of the refusal: a structured
    ///     failure with the full output layout still written and no exception reaching the caller —
    ///     never a silent omission and never an install directive the reader cannot act on.
    /// </remarks>
    [Fact]
    public async Task DocDownWord_Extract_LegacyDoc_IsUnreadableWithUnsupportedFormatExplanation()
    {
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, "report.doc", DocxFixtures.LegacyDocBytes());
        var scratch = Path.Combine(temp.Path, "out");

        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);

        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.Null(result.SelectedExtractor);
        Assert.NotNull(result.Failure);
        Assert.Equal("No registered extractor supports the detected format.", result.Failure.Summary);
        Assert.Contains("doc", result.Failure.Explanation, StringComparison.Ordinal);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Explanation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft Office", result.Failure.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Explanation, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Extracts a fixture through the full Word system and returns the folder and result.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The fixture file name.</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <returns>The extraction folder and result.</returns>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes)
    {
        var engine = new DocDownBuilder().AddWord().Build();
        var input = WriteFixture(temp, name, bytes);
        var scratch = Path.Combine(temp.Path, "out");
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, FixedOptions(), Ct);
        return (scratch, result);
    }

    /// <summary>
    ///     Creates options carrying the fixed timestamp for reproducible output.
    /// </summary>
    /// <returns>The options.</returns>
    private static ExtractionOptions FixedOptions() => new();

    /// <summary>
    ///     Writes a fixture to disk.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The file name, whose extension drives format detection.</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <returns>The written path.</returns>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
