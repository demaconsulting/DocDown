using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ExtractionSink"/>, the sole write path, proving image and page
///     allocation, SHA-256 deduplication, path safety, content-feature recording, and note
///     recording.
/// </summary>
public class ExtractionSinkTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves image ordinals are dense, one-based, and allocated in call order.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_ThreeDistinctImages_AllocatesDenseOrdinalPaths()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add three distinct images
        var first = await AddImageAsync(sink, [1], "image/png");
        var second = await AddImageAsync(sink, [2], "image/png");
        var third = await AddImageAsync(sink, [3], "image/png");

        // Assert: the paths are dense and each file exists
        Assert.Equal("images/0001-image.png", first);
        Assert.Equal("images/0002-image.png", second);
        Assert.Equal("images/0003-image.png", third);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(sink.Folder.AbsolutePath, "images")).Length);
    }

    /// <summary>
    ///     Proves the written extension follows the written media type.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_JpegMediaType_UsesJpgExtension()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add a JPEG image
        var path = await AddImageAsync(sink, [9, 8, 7], "image/jpeg");

        // Assert: the extension reflects the media type
        Assert.EndsWith(".jpg", path, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves vector media types are written with their true extensions.
    /// </summary>
    /// <param name="mediaType">The vector media type.</param>
    /// <param name="expectedExtension">The expected extension.</param>
    [Theory]
    [InlineData("image/x-emf", ".emf")]
    [InlineData("image/emf", ".emf")]
    [InlineData("image/x-wmf", ".wmf")]
    [InlineData("image/wmf", ".wmf")]
    [InlineData("image/svg+xml", ".svg")]
    public async Task ExtractionSink_AddImageAsync_VectorMediaType_UsesTrueExtension(
        string mediaType,
        string expectedExtension)
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add one vector image
        var path = await AddImageAsync(sink, [1, 2, 3, 4], mediaType);

        // Assert: the true extension is preserved
        Assert.EndsWith(expectedExtension, path, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves byte-identical images are stored once and reference-counted.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_IdenticalBytesTwice_DeduplicatesToOneFile()
    {
        // Arrange: a real sink and one byte sequence written twice
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var bytes = new byte[] { 5, 6, 7, 8 };

        // Act: add the same image twice
        var firstPath = await AddImageAsync(sink, bytes, "image/png");
        var secondPath = await AddImageAsync(sink, bytes, "image/png");

        // Assert: the second write reuses the first file and increments the reference count
        Assert.Equal(firstPath, secondPath);
        Assert.Single(sink.Images);
        Assert.Equal(2, sink.Images[0].References);
        Assert.Single(Directory.GetFiles(Path.Combine(sink.Folder.AbsolutePath, "images")));
    }

    /// <summary>
    ///     Proves a deduplicated add merges its referrer set into the surviving record.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_DuplicateBytes_MergesReferrerSets()
    {
        // Arrange: a real sink and one image added twice from different source pages
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var bytes = new byte[] { 5, 6, 7, 8 };

        // Act: add the duplicate image with different referrers
        using var first = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(first, new ImageHint(null, "image/png", SourcePages: [3]), Ct);
        using var second = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(
            second,
            new ImageHint(null, "image/png", SourcePages: [1], ReferencedByTemplate: true),
            Ct);

        // Assert: the deduplicated record keeps the union of the referrers
        Assert.Equal([1, 3], sink.Images[0].SourcePages);
        Assert.Equal(1, sink.Images[0].SourcePage);
        Assert.True(sink.Images[0].ReferencedByTemplate);
    }

    /// <summary>
    ///     Proves rendered pages are named by their one-based document page number.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddPageAsync_PageNumber_NamesFileByDocumentPage()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: render document page seven
        var path = await AddPageAsync(sink, 7, [1, 2, 3]);

        // Assert: the page file is named by the source page number
        Assert.Equal("pages/page0007.png", path);
        Assert.True(File.Exists(Path.Combine(sink.Folder.AbsolutePath, "pages", "page0007.png")));
    }

    /// <summary>
    ///     Proves a non-positive page number is rejected as a caller error.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddPageAsync_ZeroPageNumber_ThrowsArgumentOutOfRange()
    {
        // Arrange: a sink and an invalid page number
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        using var png = new MemoryStream([1]);

        // Act / Assert: page numbers are one-based
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await sink.AddPageAsync(0, png, Ct));
    }

    /// <summary>
    ///     Proves part ordinals are Core-assigned in call order, ignoring the advisory ordinal.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddContentPartAsync_ConflictingAdvisoryOrdinals_AllocatesDenseOrdinals()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add two parts whose advisory ordinals conflict
        var first = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Sheet, 99, "One"), "a", Ct);
        var second = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Sheet, 99, "Two"), "b", Ct);

        // Assert: Core assigns its own dense ordinals
        Assert.Equal("parts/0001-sheet-one.md", first);
        Assert.Equal("parts/0002-sheet-two.md", second);
    }

    /// <summary>
    ///     Proves image writing is suppressed when embedded images are disabled.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_ImagesDisabled_SuppressesWritesAndReturnsEmptyPath()
    {
        // Arrange: a sink whose options disable embedded-image extraction
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions { IncludeEmbeddedImages = false });

        // Act: attempt to add two images
        var firstPath = await AddImageAsync(sink, [1], "image/png");
        var secondPath = await AddImageAsync(sink, [2], "image/png");

        // Assert: nothing is written and the extractor receives no link path
        Assert.Equal(string.Empty, firstPath);
        Assert.Equal(string.Empty, secondPath);
        Assert.Empty(sink.Images);
        Assert.False(Directory.Exists(Path.Combine(sink.Folder.AbsolutePath, "images")));
    }

    /// <summary>
    ///     Proves the sink records document info, metadata, notes, and environment facts for later serialization.
    /// </summary>
    [Fact]
    public void ExtractionSink_Reports_RecordedForLaterSerialization()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var metadata = new DocumentMetadata([], []);

        // Act: report a superseding document-info stream and the remaining honesty facts
        sink.ReportDocumentInfo(new DocumentInfo("Draft"));
        sink.ReportDocumentInfo(new DocumentInfo("Final"));
        sink.ReportDocumentMetadata(metadata);
        sink.ReportNote(new ExtractionNote("A page could not be rendered."));
        sink.ReportEnvironmentFact(new EnvironmentFact("TestBackend", "gpu", "absent", false));

        // Assert: later document info wins and the reported data is retained
        Assert.Equal("Final", sink.DocumentInfo?.Title);
        Assert.Same(metadata, sink.DocumentMetadata);
        Assert.Contains(sink.Notes, note => note.Message == "A page could not be rendered.");
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "gpu");
    }

    /// <summary>
    ///     Proves blank note text is rejected as a caller error.
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportNote_BlankMessage_ThrowsArgumentException()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act / Assert: note messages must contain plain-language content
        Assert.Throws<ArgumentException>(() => sink.ReportNote(new ExtractionNote(" ")));
    }

    /// <summary>
    ///     Proves repeated reports of one content-feature label accumulate and undeclared zero counts are dropped.
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportContentFeature_RepeatedLabels_AccumulateAndDropZeroCounts()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report one label twice, another once, and an undeclared zero
        sink.ReportContentFeature(new ContentFeature("tables", 2));
        sink.ReportContentFeature(new ContentFeature("comments", 5));
        sink.ReportContentFeature(new ContentFeature("tables", 3));
        sink.ReportContentFeature(new ContentFeature("footnotes", 0));

        // Assert: the matching label accumulates and the undeclared zero is omitted
        Assert.Collection(
            sink.ContentFeatures,
            feature => Assert.Equal(new ContentFeature("tables", 5), feature),
            feature => Assert.Equal(new ContentFeature("comments", 5), feature));
    }

    /// <summary>
    ///     Proves a looked-for zero survives and later accumulations keep its looked-for standing.
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportContentFeature_LookedForZero_IsReported()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report a looked-for zero, an undeclared zero, and a later accumulated count
        sink.ReportContentFeature(new ContentFeature(
            "sets of speaker notes",
            0,
            "set of speaker notes",
            LookedFor: true));
        sink.ReportContentFeature(new ContentFeature("worksheets", 0));
        sink.ReportContentFeature(new ContentFeature("comments", 0, LookedFor: true));
        sink.ReportContentFeature(new ContentFeature("comments", 4));

        // Assert: the looked-for zero survives and accumulation preserves its looked-for state
        Assert.Collection(
            sink.ContentFeatures,
            feature => Assert.Equal(
                new ContentFeature("sets of speaker notes", 0, "set of speaker notes", LookedFor: true),
                feature),
            feature => Assert.Equal(new ContentFeature("comments", 4, LookedFor: true), feature));
    }

    /// <summary>
    ///     Proves a blank feature label and a negative count are rejected as caller errors.
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportContentFeature_InvalidFeature_Throws()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act / Assert: unnamed and negative features are invalid
        Assert.Throws<ArgumentException>(() => sink.ReportContentFeature(new ContentFeature("  ", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sink.ReportContentFeature(new ContentFeature("tables", -1)));
    }

    /// <summary>
    ///     Proves a content feature agrees in number with its count.
    /// </summary>
    [Fact]
    public void ContentFeature_AgreeingLabel_CountOfOne_UsesSingularForm()
    {
        // Arrange / Act / Assert: regular and explicit singular forms agree with the count
        Assert.Equal("table", new ContentFeature("tables", 1).AgreeingLabel);
        Assert.Equal("tables", new ContentFeature("tables", 2).AgreeingLabel);
        Assert.Equal("cell carrying a formula", new ContentFeature("cells carrying a formula", 1, "cell carrying a formula").AgreeingLabel);
        Assert.Equal("cells carrying a formula", new ContentFeature("cells carrying a formula", 4, "cell carrying a formula").AgreeingLabel);
    }

    /// <summary>
    ///     Proves a hostile preferred image name is neutralized and stays within the images folder.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_HostilePreferredName_StaysContained()
    {
        // Arrange: a sink and a hostile preferred name attempting traversal
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        using var bytes = new MemoryStream([1, 2, 3, 4], writable: false);

        // Act: add the image
        var path = await sink.AddImageAsync(bytes, new ImageHint("../../etc/passwd", "image/png"), Ct);

        // Assert: the sink allocates a contained relative path and writes inside the scratch folder
        Assert.StartsWith("images/", path, StringComparison.Ordinal);
        Assert.DoesNotContain("..", path, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sink.Folder.AbsolutePath, path.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    ///     Creates a real sink over a prepared scratch folder.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="options">The options to apply.</param>
    /// <returns>The configured sink.</returns>
    private static ExtractionSink NewSink(TempScratch temp, ExtractionOptions options)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        return new ExtractionSink(folder, options);
    }

    /// <summary>
    ///     Adds an image to the sink from an in-memory payload.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="bytes">The image payload.</param>
    /// <param name="mediaType">The written media type.</param>
    /// <returns>The relative image path returned by the sink.</returns>
    private static async ValueTask<string> AddImageAsync(ExtractionSink sink, byte[] bytes, string mediaType)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return await sink.AddImageAsync(stream, new ImageHint("image", mediaType), Ct);
    }

    /// <summary>
    ///     Adds a rendered page to the sink from an in-memory payload.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="pageNumber">The one-based document page number.</param>
    /// <param name="bytes">The PNG payload.</param>
    /// <returns>The relative page path returned by the sink.</returns>
    private static async ValueTask<string> AddPageAsync(ExtractionSink sink, int pageNumber, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return await sink.AddPageAsync(pageNumber, stream, Ct);
    }
}
