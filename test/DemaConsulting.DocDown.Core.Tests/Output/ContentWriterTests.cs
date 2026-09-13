using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ContentWriter"/>, proving the single-flow document layout, the
///     multi-part index layout, that the split mode is honored, that extractor page markers are
///     passed through untouched, and that an empty flow is honestly reported as absent.
/// </summary>
/// <remarks>
///     These tests drive a real <see cref="ExtractionSink"/> (the writer's documented source of
///     buffered content and parts) over a prepared <see cref="ScratchFolder"/>, then finalize with
///     <see cref="ContentWriter.WriteAsync"/> and reconcile against disk. Each is named for the unit
///     requirement it evidences: single-flow document, part index, split-mode honoring, page
///     markers, and absence explanation.
/// </remarks>
public class ContentWriterTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a single buffered flow is written verbatim to content.md (SingleFlowDocument).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_SingleBufferedFlow_WritesContentVerbatim()
    {
        // Arrange: a sink holding a single flow of buffered markdown, no parts
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        const string markdown = "# Title\n\nA single flow of content.\n";
        await sink.WriteContentAsync(markdown, Ct);

        // Act: finalize the content document
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, "Title", Ct);

        // Assert: content.md carries the buffered text verbatim, is present, and no part files were written
        Assert.Equal("content.md", result.ContentPath);
        Assert.True(result.ContentPresent);
        Assert.Empty(result.PartPaths);
        Assert.Equal(markdown, await ReadContentAsync(sink));
    }

    /// <summary>
    ///     Proves a multi-part document produces an index plus per-part files (PartIndex).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_MultipleParts_ProducesIndexAndPartFiles()
    {
        // Arrange: a sink holding two content parts
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Sheet, 1, "Budget"), "# Budget\n", Ct);
        await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Sheet, 2, "Forecast"), "# Forecast\n", Ct);

        // Act: finalize in the auto split mode, which indexes multi-part content
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, "Workbook", Ct);
        var index = await ReadContentAsync(sink);

        // Assert: the index heads the document, counts the parts, links each, and the part files exist on disk
        Assert.Equal(2, result.PartPaths.Count);
        Assert.Contains("# Workbook", index, StringComparison.Ordinal);
        Assert.Contains("2 parts", index, StringComparison.Ordinal);
        Assert.Contains("](parts/0001-sheet-budget.md)", index, StringComparison.Ordinal);
        Assert.All(result.PartPaths, part =>
            Assert.True(File.Exists(Path.Combine(sink.Folder.AbsolutePath, part.Replace('/', Path.DirectorySeparatorChar)))));
    }

    /// <summary>
    ///     Proves Single mode concatenates parts into one document with no parts folder (SplitModeHonored).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_SingleMode_ConcatenatesPartsWithoutPartsFolder()
    {
        // Arrange: a sink holding two content parts
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Section, 1, "Intro"), "Intro body.\n", Ct);
        await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Section, 2, "Detail"), "Detail body.\n", Ct);

        // Act: finalize in Single mode, which concatenates parts under headings
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Single, "Report", Ct);
        var content = await ReadContentAsync(sink);

        // Assert: the parts became sections of one document and no parts/ folder was created
        Assert.Empty(result.PartPaths);
        Assert.Contains("## Intro", content, StringComparison.Ordinal);
        Assert.Contains("Detail body.", content, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(sink.Folder.AbsolutePath, "parts")));
    }

    /// <summary>
    ///     Proves PerPart mode always produces the index layout with separate part files (SplitModeHonored).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_PerPartMode_ProducesIndexWithPartFiles()
    {
        // Arrange: a sink holding a single content part
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Slide, 1, "Opening"), "Opening body.\n", Ct);

        // Act: finalize in PerPart mode, which always indexes
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.PerPart, "Deck", Ct);

        // Assert: a part file was written under parts/ and linked from the index
        Assert.Single(result.PartPaths);
        Assert.True(Directory.Exists(Path.Combine(sink.Folder.AbsolutePath, "parts")));
    }

    /// <summary>
    ///     Proves the extractor's page markers are passed through content.md untouched (PageMarkers).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_SingleFlowWithPageMarkers_PreservesMarkers()
    {
        // Arrange: a single flow whose text carries the extractor's page markers
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        const string markdown = "<!-- docdown:page 1 -->\nPage one.\n<!-- docdown:page 2 -->\nPage two.\n";
        await sink.WriteContentAsync(markdown, Ct);

        // Act: finalize the single-flow document
        await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, null, Ct);
        var content = await ReadContentAsync(sink);

        // Assert: both page markers survive verbatim because the writer invents nothing
        Assert.Contains("<!-- docdown:page 1 -->", content, StringComparison.Ordinal);
        Assert.Contains("<!-- docdown:page 2 -->", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an empty flow still writes content.md but reports it as absent (AbsenceExplained).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_EmptyFlow_WritesFileButReportsAbsent()
    {
        // Arrange: a sink with no buffered content and no parts
        using var temp = new TempScratch();
        var sink = NewSink(temp);

        // Act: finalize with nothing to write
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, null, Ct);

        // Assert: content.md exists but is honestly reported absent so an explaining gap is forced
        Assert.True(File.Exists(Path.Combine(sink.Folder.AbsolutePath, "content.md")));
        Assert.False(result.ContentPresent);
    }

    /// <summary>
    ///     Proves a null sink is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public async Task ContentWriter_WriteAsync_NullSink_ThrowsArgumentNullException()
    {
        // Act + Assert: the buffered content source is mandatory
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ContentWriter.WriteAsync(null!, ContentSplitMode.Auto, "Title", Ct));
    }

    /// <summary>
    ///     Proves an image link an extractor emits root-relative resolves on disk from a part file
    ///     under <c>parts/</c> in the index layout (Auto) — the root-cause defect regression.
    /// </summary>
    /// <remarks>
    ///     Exercises the real sink write path end to end: a real image is added (allocating
    ///     <c>images/…</c>), a part references it with the sink-allocated root-relative link, and after
    ///     finalization every link in every <c>parts/*.md</c> is resolved against its own directory.
    ///     A string assertion cannot catch the dangling <c>parts/images/…</c> resolution this pins.
    /// </remarks>
    [Fact]
    public async Task ContentWriter_WriteAsync_PartImageLink_ResolvesOnDiskInAutoLayout()
    {
        // Arrange: a real image and a part that links it with the sink-allocated root-relative path
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        var imagePath = await AddImageAsync(sink, "chart");
        Assert.StartsWith("images/", imagePath, StringComparison.Ordinal);
        await sink.AddContentPartAsync(
            new ContentPart(ContentPartKind.Sheet, 1, "Charts"), $"# Charts\n\n![Chart]({imagePath})\n", Ct);

        // Act: finalize in Auto, which indexes the part under parts/
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, "Workbook", Ct);

        // Assert: a part file was written and its image link resolves on disk from its own directory
        Assert.Single(result.PartPaths);
        var resolved = MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(sink.Folder.AbsolutePath);
        Assert.True(resolved >= 1);
        Assert.Contains("![Chart](../images/", await ReadPartAsync(sink, result.PartPaths[0]), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the same root-relative image link resolves on disk under the PerPart layout.
    /// </summary>
    /// <remarks>
    ///     PerPart always indexes, so this pins that every backend routing content into <c>parts/</c>
    ///     under <c>--split per-part</c> gets on-disk-resolvable links from the single Core fix.
    /// </remarks>
    [Fact]
    public async Task ContentWriter_WriteAsync_PartImageLink_ResolvesOnDiskInPerPartLayout()
    {
        // Arrange: a real image and a slide part linking it root-relative
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        var imagePath = await AddImageAsync(sink, "figure");
        await sink.AddContentPartAsync(
            new ContentPart(ContentPartKind.Slide, 1, "Opening"), $"![Figure]({imagePath})\n", Ct);

        // Act: finalize in PerPart, which always indexes into parts/
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.PerPart, "Deck", Ct);

        // Assert: the part image link resolves against its own directory
        Assert.Single(result.PartPaths);
        var resolved = MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(sink.Folder.AbsolutePath);
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Proves a single-flow root <c>content.md</c> keeps the extractor's root-relative link, which
    ///     resolves from the root and is not rewritten.
    /// </summary>
    /// <remarks>
    ///     Guards the boundary of the fix: the rewrite is confined to the parts layout, so a
    ///     root-level document's already-correct links must be left untouched and still resolve.
    /// </remarks>
    [Fact]
    public async Task ContentWriter_WriteAsync_SingleFlowImageLink_ResolvesFromRootUnchanged()
    {
        // Arrange: a real image and a single buffered flow that links it root-relative
        using var temp = new TempScratch();
        var sink = NewSink(temp);
        var imagePath = await AddImageAsync(sink, "logo");
        await sink.WriteContentAsync($"# Doc\n\n![Logo]({imagePath})\n", Ct);

        // Act: finalize as a single flow (no parts)
        var result = await ContentWriter.WriteAsync(sink, ContentSplitMode.Auto, "Doc", Ct);
        var content = await ReadContentAsync(sink);

        // Assert: content.md kept the root-relative link verbatim and it resolves from the root
        Assert.Empty(result.PartPaths);
        Assert.Contains($"![Logo]({imagePath})", content, StringComparison.Ordinal);
        var resolved = MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(sink.Folder.AbsolutePath);
        Assert.True(resolved >= 1);
    }

    /// <summary>
    ///     Creates a sink over a freshly prepared scratch folder.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <returns>The prepared sink.</returns>
    /// <remarks>Prepares the scratch folder through the safety gate the content writer writes behind.</remarks>
    private static ExtractionSink NewSink(TempScratch temp)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        return new ExtractionSink(folder, new ExtractionOptions());
    }

    /// <summary>
    ///     Adds a small real image through the sink and returns its allocated root-relative path.
    /// </summary>
    /// <param name="sink">The sink that allocates the path and performs the write.</param>
    /// <param name="preferredName">The preferred file name used to derive the image slug.</param>
    /// <returns>The forward-slash, root-relative <c>images/…</c> path the sink allocated.</returns>
    /// <remarks>
    ///     Writes a few bytes to disk under <c>images/</c> so a link that references the returned path
    ///     has a real target to resolve against; the exact bytes are irrelevant to link resolution.
    /// </remarks>
    private static async ValueTask<string> AddImageAsync(ExtractionSink sink, string preferredName)
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4]);
        return await sink.AddImageAsync(stream, new ImageHint(preferredName, "image/png"), Ct);
    }

    /// <summary>
    ///     Reads a finalized part file from the sink's scratch folder.
    /// </summary>
    /// <param name="sink">The sink whose folder holds the part file.</param>
    /// <param name="partRelativePath">The forward-slash relative path of the part file.</param>
    /// <returns>The text of the part file as written to disk.</returns>
    /// <remarks>Reads the exact bytes the writer produced so assertions see the rewritten link form.</remarks>
    private static async Task<string> ReadPartAsync(ExtractionSink sink, string partRelativePath) =>
        await File.ReadAllTextAsync(
            Path.Combine(sink.Folder.AbsolutePath, partRelativePath.Replace('/', Path.DirectorySeparatorChar)), Ct);

    /// <summary>
    ///     Reads the finalized content document from the sink's scratch folder.
    /// </summary>
    /// <param name="sink">The sink whose folder holds the content document.</param>
    /// <returns>The text of <c>content.md</c>.</returns>
    /// <remarks>Reads the exact bytes the writer produced so assertions see the on-disk form.</remarks>
    private static async Task<string> ReadContentAsync(ExtractionSink sink) =>
        await File.ReadAllTextAsync(Path.Combine(sink.Folder.AbsolutePath, "content.md"), Ct);
}
