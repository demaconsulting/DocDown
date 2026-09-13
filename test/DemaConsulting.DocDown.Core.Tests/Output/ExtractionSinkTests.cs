using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ExtractionSink"/>, the sole write path, proving image path
///     allocation and SHA-256 deduplication, page and part path allocation, image suppression,
///     the honesty-stream reports, and dense Core-assigned gap identifiers.
/// </summary>
/// <remarks>
///     These tests drive a real sink over a prepared <see cref="ScratchFolder"/> (its documented
///     dependency) and inspect the internal recorded state visible via <c>InternalsVisibleTo</c>.
///     Each is named for the unit requirement it evidences: image path allocation, image
///     deduplication, page path allocation, part path allocation, image suppression, records and
///     reports, and gap-identifier allocation, plus the image-transform provenance the manifest
///     records for every written image.
/// </remarks>
public class ExtractionSinkTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves image ordinals are dense, one-based, and allocated in call order (AllocatesImagePaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_ThreeDistinctImages_AllocatesDenseOrdinalPaths()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add three images with distinct bytes
        var first = await AddImageAsync(sink, [1], "image/png");
        var second = await AddImageAsync(sink, [2], "image/png");
        var third = await AddImageAsync(sink, [3], "image/png");

        // Assert: the paths carry dense one-based ordinals in call order and each file exists on disk
        Assert.Equal("images/0001-image.png", first);
        Assert.Equal("images/0002-image.png", second);
        Assert.Equal("images/0003-image.png", third);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(sink.Folder.AbsolutePath, "images")).Length);
    }

    /// <summary>
    ///     Proves the file extension follows the written media type (AllocatesImagePaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_JpegMediaType_UsesJpgExtension()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add an image declared as JPEG
        var path = await AddImageAsync(sink, [9, 8, 7], "image/jpeg");

        // Assert: the extension reflects the bytes' media type, not a requested format
        Assert.EndsWith(".jpg", path, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a vector media type is written with its true extension (EMF, WMF, SVG) rather than
    ///     the <c>.bin</c> fallback, so an extracted vector asset is named for what it is.
    /// </summary>
    /// <param name="mediaType">The vector image media type.</param>
    /// <param name="expectedExtension">The extension the written file must carry.</param>
    [Theory]
    [InlineData("image/x-emf", ".emf")]
    [InlineData("image/emf", ".emf")]
    [InlineData("image/x-wmf", ".wmf")]
    [InlineData("image/wmf", ".wmf")]
    [InlineData("image/svg+xml", ".svg")]
    public async Task ExtractionSink_AddImageAsync_VectorMediaType_UsesTrueExtension(
        string mediaType, string expectedExtension)
    {
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        var path = await AddImageAsync(sink, [1, 2, 3, 4], mediaType);

        Assert.EndsWith(expectedExtension, path, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the allocated image name begins with the four-digit ordinal prefix and a hyphen (AllocatesImagePaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_AllocatedName_BeginsWithOrdinalPrefix()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add an image and read the allocated file name
        var path = await AddImageAsync(sink, [42], "image/png");
        var fileName = path["images/".Length..];

        // Assert: the name starts with four digits and a hyphen, the prefix that also defuses reserved names
        Assert.True(BeginsWithOrdinalPrefix(fileName), $"'{fileName}' does not begin with the {{ordinal:D4}}- prefix.");
    }

    /// <summary>
    ///     Proves byte-identical images are stored once and reference-counted (ImageDeduplication).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_IdenticalBytesTwice_DeduplicatesToOneFile()
    {
        // Arrange: a real sink and two identical image payloads
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var bytes = new byte[] { 5, 6, 7, 8 };

        // Act: add the same bytes twice
        var firstPath = await AddImageAsync(sink, bytes, "image/png");
        var secondPath = await AddImageAsync(sink, bytes, "image/png");

        // Assert: one file, one manifest entry, two references, and the same returned relative path
        Assert.Equal(firstPath, secondPath);
        var record = Assert.Single(sink.Images);
        Assert.Equal(2, record.References);
        Assert.Single(Directory.GetFiles(Path.Combine(sink.Folder.AbsolutePath, "images")));
    }

    /// <summary>
    ///     Proves a deduplicated add merges its page associations into the surviving record — the
    ///     union of the source pages and the OR of the template flag — so no referrer is lost, and the
    ///     scalar source page stays the deterministic lowest referrer.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_DuplicateBytes_MergesReferrerSets()
    {
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var bytes = new byte[] { 5, 6, 7, 8 };

        using var first = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(first, new ImageHint(null, "image/png", SourcePages: [3]), Ct);
        using var second = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(
            second, new ImageHint(null, "image/png", SourcePages: [1], ReferencedByTemplate: true), Ct);

        var record = Assert.Single(sink.Images);
        Assert.Equal([1, 3], record.SourcePages);
        Assert.Equal(1, record.SourcePage);
        Assert.True(record.ReferencedByTemplate);
    }

    /// <summary>
    ///     Proves the scalar source page is derived as the first entry of the referrer set when only
    ///     the set is supplied, so a consumer reading the scalar still sees a stable referrer. The
    ///     writer supplies the set already sorted, so the first entry is the lowest referrer.
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_SourcePagesOnly_DerivesScalarFirst()
    {
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        using var stream = new MemoryStream([1, 2, 3, 4], writable: false);
        await sink.AddImageAsync(stream, new ImageHint(null, "image/png", SourcePages: [2, 5, 9]), Ct);

        var record = Assert.Single(sink.Images);
        Assert.Equal(2, record.SourcePage);
        Assert.Equal([2, 5, 9], record.SourcePages);
    }

    /// <summary>
    ///     Proves a rendered page is named by its one-based document page number (AllocatesPagePaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddPageAsync_PageNumber_NamesFileByDocumentPage()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: render document page seven
        var path = await AddPageAsync(sink, 7, [1, 2, 3]);

        // Assert: the page file is named by its document page number and exists on disk
        Assert.Equal("pages/page0007.png", path);
        Assert.True(File.Exists(Path.Combine(sink.Folder.AbsolutePath, "pages", "page0007.png")));
    }

    /// <summary>
    ///     Proves a non-positive page number is rejected as a caller error (AllocatesPagePaths boundary).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddPageAsync_ZeroPageNumber_ThrowsArgumentOutOfRange()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        using var png = new MemoryStream([1]);

        // Act + Assert: page numbers are one-based, so zero is invalid
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await sink.AddPageAsync(0, png, Ct));
    }

    /// <summary>
    ///     Proves a titled part path carries its ordinal, kind, and slug and defers the write (AllocatesPartPaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddContentPartAsync_TitledPart_AllocatesOrdinalKindSlugPath()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add a titled sheet part
        var path = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Sheet, 5, "Q1 Budget"), "# Q1\n", Ct);

        // Assert: the path reflects the Core-allocated ordinal, the kind, and the slug, and no file is written yet
        Assert.Equal("parts/0001-sheet-q1-budget.md", path);
        Assert.Single(sink.Parts);
        Assert.False(File.Exists(Path.Combine(sink.Folder.AbsolutePath, "parts", "0001-sheet-q1-budget.md")));
    }

    /// <summary>
    ///     Proves an untitled part falls back to the ordinal-and-kind path (AllocatesPartPaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddContentPartAsync_UntitledPart_AllocatesOrdinalKindPath()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add a part with no title
        var path = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Slide, 2, null), "# slide\n", Ct);

        // Assert: an empty slug falls back to the ordinal-and-kind form
        Assert.Equal("parts/0001-slide.md", path);
    }

    /// <summary>
    ///     Proves part ordinals are Core-assigned in call order, ignoring the advisory value (AllocatesPartPaths).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddContentPartAsync_ConflictingAdvisoryOrdinals_AllocatesDenseOrdinals()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add two parts whose advisory ordinals are both 99
        var first = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Section, 99, "One"), "a", Ct);
        var second = await sink.AddContentPartAsync(new ContentPart(ContentPartKind.Section, 99, "Two"), "b", Ct);

        // Assert: Core allocates dense one-based ordinals regardless of the extractor's advisory numbers
        Assert.Equal("parts/0001-section-one.md", first);
        Assert.Equal("parts/0002-section-two.md", second);
    }

    /// <summary>
    ///     Proves image writing is suppressed, recorded once, and signalled with an empty path (ImageSuppression).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_ImagesDisabled_SuppressesAndRecordsOnce()
    {
        // Arrange: a sink whose options disable embedded images
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions { IncludeEmbeddedImages = false });

        // Act: attempt to add two images while suppression is in force
        var firstPath = await AddImageAsync(sink, [1], "image/png");
        var secondPath = await AddImageAsync(sink, [2], "image/png");

        // Assert: nothing is written, an empty path is returned, and the DD0201 suppression is recorded exactly once
        Assert.Equal(string.Empty, firstPath);
        Assert.Equal(string.Empty, secondPath);
        Assert.Empty(sink.Images);
        Assert.True(sink.ImagesSuppressed);
        Assert.Single(sink.Diagnostics, diagnostic => diagnostic.Code == "DD0201");
    }

    /// <summary>
    ///     Proves the sink records document info, diagnostics, environment facts, and found counts (RecordsReports).
    /// </summary>
    [Fact]
    public void ExtractionSink_Reports_RecordedForLaterSerialization()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report a stream of honesty facts, with a superseding document-info report
        sink.ReportDocumentInfo(new DocumentInfo("Draft"));
        sink.ReportDocumentInfo(new DocumentInfo("Final"));
        sink.ReportDiagnostic(new ExtractionDiagnostic("DD9999", DiagnosticSeverity.Info, "note"));
        sink.ReportEnvironmentFact(new EnvironmentFact("TestBackend", "gpu", "absent", false));
        sink.ReportFound(GapKind.Images, 4);

        // Assert: later document info wins and every reported stream is retained for the writers
        Assert.Equal("Final", sink.DocumentInfo?.Title);
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "DD9999");
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "gpu");
        Assert.Equal(4, sink.FoundCounts[GapKind.Images]);
    }

    /// <summary>
    ///     Proves a negative found count is rejected as a caller error (RecordsReports boundary).
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportFound_NegativeCount_ThrowsArgumentOutOfRange()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act + Assert: a negative denominator is meaningless and rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => sink.ReportFound(GapKind.Images, -1));
    }

    /// <summary>
    ///     Proves repeated reports of one content-feature label accumulate in first-reported order
    ///     and that a zero count never reaches the output (RecordsReports).
    /// </summary>
    /// <remarks>
    ///     Accumulation lets a per-part backend report each part separately; dropping zeros is what
    ///     keeps the summary's content outline from ever printing a line of zeroes.
    /// </remarks>
    [Fact]
    public void ExtractionSink_ReportContentFeature_RepeatedLabels_AccumulateAndDropZeroCounts()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report one label twice, a second label once, and a zero count
        sink.ReportContentFeature(new ContentFeature("tables", 2));
        sink.ReportContentFeature(new ContentFeature("comments", 5));
        sink.ReportContentFeature(new ContentFeature("tables", 3));
        sink.ReportContentFeature(new ContentFeature("footnotes", 0));

        // Assert: labels accumulate, first-reported order survives, and the zero is absent
        Assert.Collection(
            sink.ContentFeatures,
            feature => Assert.Equal(new ContentFeature("tables", 5), feature),
            feature => Assert.Equal(new ContentFeature("comments", 5), feature));
    }

    /// <summary>
    ///     Proves a blank label and a negative count are rejected as caller errors (RecordsReports boundary).
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportContentFeature_InvalidFeature_Throws()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act + Assert: an unnamed feature and a negative count are both meaningless
        Assert.Throws<ArgumentException>(() => sink.ReportContentFeature(new ContentFeature("  ", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sink.ReportContentFeature(new ContentFeature("tables", -1)));
    }

    /// <summary>
    ///     Proves a content feature agrees in number with its count, using the explicit singular form
    ///     when the label does not pluralize by a trailing <c>s</c>.
    /// </summary>
    /// <remarks>
    ///     "1 headings" in a file meant to be read by a model reads as a defect and undermines trust
    ///     in every count beside it, so agreement is asserted rather than assumed.
    /// </remarks>
    [Fact]
    public void ContentFeature_AgreeingLabel_CountOfOne_UsesSingularForm()
    {
        // Arrange / Act / Assert: regular, irregular, and plural labels all agree with their count
        Assert.Equal("table", new ContentFeature("tables", 1).AgreeingLabel);
        Assert.Equal("tables", new ContentFeature("tables", 2).AgreeingLabel);
        Assert.Equal("cell carrying a formula", new ContentFeature("cells carrying a formula", 1, "cell carrying a formula").AgreeingLabel);
        Assert.Equal("cells carrying a formula", new ContentFeature("cells carrying a formula", 4, "cell carrying a formula").AgreeingLabel);
    }

    /// <summary>
    ///     Proves gap identifiers are dense, emission-ordered, and overwrite any caller value (AllocatesGapIdentifiers).
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportGap_MultipleGaps_AssignsDenseIdentifiersOverwritingCallerIds()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report two gaps, one carrying a caller-supplied identifier that must be overwritten
        sink.ReportGap(new ExtractionGap("caller-chosen", GapKind.Images, "images/", GapScope.Unavailable, "no decoder"));
        sink.ReportGap(new ExtractionGap(string.Empty, GapKind.Pages, "pages/", GapScope.NotAttempted, "not requested"));

        // Assert: the identifiers are the dense GAP-1, GAP-2 sequence in emission order
        Assert.Equal(["GAP-1", "GAP-2"], sink.Gaps.Select(gap => gap.Id));
    }

    /// <summary>
    ///     Proves a gap reported with a blank reason is repaired with a Core reason and a diagnostic (AllocatesGapIdentifiers).
    /// </summary>
    [Fact]
    public void ExtractionSink_ReportGap_BlankReason_SubstitutesReasonAndRecordsDiagnostic()
    {
        // Arrange: a sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: report a gap whose reason is only whitespace
        sink.ReportGap(new ExtractionGap("x", GapKind.Text, "content.md", GapScope.PartiallyExtracted, "   "));

        // Assert: a blank reason is not accepted verbatim; Core supplies one and records a DD0701 warning
        var gap = Assert.Single(sink.Gaps);
        Assert.False(string.IsNullOrWhiteSpace(gap.Reason));
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "DD0701");
    }

    /// <summary>
    ///     Proves an image added with no transform hint is recorded as a passthrough (ImageTransformRecorded).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_NoTransformHint_RecordsPassthrough()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add an image whose hint says nothing about how the bytes were produced
        using var stream = new MemoryStream([1, 2, 3]);
        await sink.AddImageAsync(stream, new ImageHint("figure", "image/png"), Ct);

        // Assert: Core defaults the provenance to passthrough, because it wrote the bytes verbatim
        var record = Assert.Single(sink.Images);
        Assert.Equal(ImageTransform.Passthrough, record.Transform);
    }

    /// <summary>
    ///     Proves a re-encoded image carries the distinct decoded-to-PNG label (ImageTransformRecorded).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_DecodedToPngHint_RecordsDecodedToPng()
    {
        // Arrange: a real sink over a prepared scratch folder
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());

        // Act: add an image the extractor states it decoded and re-encoded as PNG
        using var stream = new MemoryStream([4, 5, 6]);
        await sink.AddImageAsync(
            stream, new ImageHint("figure", "image/png", Transform: ImageTransform.DecodedToPng), Ct);

        // Assert: the reported transform is recorded verbatim and is distinct from the default
        var record = Assert.Single(sink.Images);
        Assert.Equal(ImageTransform.DecodedToPng, record.Transform);
        Assert.NotEqual(ImageTransform.Passthrough, record.Transform);
    }

    /// <summary>
    ///     Proves deduplication keeps the first record's transform (ImageTransformRecorded boundary).
    /// </summary>
    [Fact]
    public async Task ExtractionSink_AddImageAsync_DuplicateBytes_KeepsFirstRecordedTransform()
    {
        // Arrange: a real sink and one image payload added twice with conflicting provenance claims
        using var temp = new TempScratch();
        var sink = NewSink(temp, new ExtractionOptions());
        var bytes = new byte[] { 7, 7, 7, 7 };

        // Act: add the same bytes first as a passthrough and then as a decode-and-re-encode
        using var first = new MemoryStream(bytes);
        await sink.AddImageAsync(first, new ImageHint(null, "image/png", Transform: ImageTransform.Passthrough), Ct);
        using var second = new MemoryStream(bytes);
        await sink.AddImageAsync(second, new ImageHint(null, "image/png", Transform: ImageTransform.DecodedToPng), Ct);

        // Assert: the single stored record keeps its first provenance, because identical bytes
        // cannot honestly carry two different provenances
        var record = Assert.Single(sink.Images);
        Assert.Equal(ImageTransform.Passthrough, record.Transform);
        Assert.Equal(2, record.References);
    }

    /// <summary>
    ///     Creates a sink over a freshly prepared scratch folder.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="options">The options governing the sink's write decisions.</param>
    /// <returns>The prepared sink.</returns>
    /// <remarks>Prepares the scratch folder through the safety gate the sink writes behind.</remarks>
    private static ExtractionSink NewSink(TempScratch temp, ExtractionOptions options)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        return new ExtractionSink(folder, options);
    }

    /// <summary>
    ///     Adds an image to the sink from an in-memory byte payload.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="mediaType">The image media type.</param>
    /// <returns>The allocated relative path.</returns>
    /// <remarks>Wraps the stream lifetime so each call disposes its own source.</remarks>
    private static async Task<string> AddImageAsync(ExtractionSink sink, byte[] bytes, string mediaType)
    {
        using var stream = new MemoryStream(bytes);
        return await sink.AddImageAsync(stream, new ImageHint(null, mediaType), Ct);
    }

    /// <summary>
    ///     Adds a rendered page to the sink from an in-memory byte payload.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <param name="pageNumber">The one-based document page number.</param>
    /// <param name="bytes">The page image bytes.</param>
    /// <returns>The allocated relative path.</returns>
    /// <remarks>Wraps the stream lifetime so each call disposes its own source.</remarks>
    private static async Task<string> AddPageAsync(ExtractionSink sink, int pageNumber, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await sink.AddPageAsync(pageNumber, stream, Ct);
    }

    /// <summary>
    ///     Determines whether a file name begins with four digits and a hyphen.
    /// </summary>
    /// <param name="fileName">The file name to inspect.</param>
    /// <returns><see langword="true"/> when the name starts with the <c>{ordinal:D4}-</c> prefix.</returns>
    /// <remarks>Encodes the Core naming invariant the reserved-name protection depends on.</remarks>
    private static bool BeginsWithOrdinalPrefix(string fileName) =>
        fileName.Length >= 5
        && char.IsDigit(fileName[0]) && char.IsDigit(fileName[1])
        && char.IsDigit(fileName[2]) && char.IsDigit(fileName[3])
        && fileName[4] == '-';
}
