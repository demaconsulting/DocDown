using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Subsystem-integration tests for the Output subsystem, exercising
///     <see cref="ScratchFolder"/>, <see cref="ExtractionSink"/>, <see cref="ContentWriter"/>,
///     <see cref="ManifestWriter"/>, <see cref="MetadataWriter"/>, and <see cref="SummaryWriter"/>
///     together at the subsystem boundary.
/// </summary>
public class OutputTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a successful write creates the always-present summary, manifest, and metadata files.
    /// </summary>
    [Fact]
    public async Task Output_ArtifactLayout_SuccessfulWrite_CreatesRootArtifacts()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing only text
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: the root artifacts exist and the layout contract holds
        Assert.True(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.True(File.Exists(Path.Combine(scratch, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(scratch, "metadata.json")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the summary carries the new mandatory sections.
    /// </summary>
    [Fact]
    public async Task Output_SummaryContent_Written_ContainsMandatorySections()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and read the summary
        await RunPipelineAsync(temp, scratch, WriteText);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the fixed sections of the reduced summary are present
        Assert.Contains("DocDown Extraction Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Scratch folder", summary, StringComparison.Ordinal);
        Assert.Contains("Document metadata", summary, StringComparison.Ordinal);
        Assert.Contains("Could not read", summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the manifest parses and declares the new schema version.
    /// </summary>
    [Fact]
    public async Task Output_ManifestContent_Written_ParsesWithSchemaVersionTwoPointZero()
    {
        // Arrange: a scratch folder and a simple text write
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline and parse the manifest
        await RunPipelineAsync(temp, scratch, WriteText);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var root = document.RootElement;

        // Assert: the manifest uses the reduced schema and omits the removed artifacts block
        Assert.Equal("2.0", root.GetProperty("schemaVersion").GetString());
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("notes", out _));
        Assert.False(root.TryGetProperty("artifacts", out _));
    }

    /// <summary>
    ///     Proves written text produces <c>content.md</c>.
    /// </summary>
    [Fact]
    public async Task Output_ContentDocument_TextWritten_ProducesContentMd()
    {
        // Arrange: a scratch folder and a text write with distinctive content
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing text
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: content.md exists and carries the written text
        var contentPath = Path.Combine(scratch, "content.md");
        Assert.True(File.Exists(contentPath));
        Assert.Contains("Hello from the output subsystem", await File.ReadAllTextAsync(contentPath, Ct), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves identical image bytes are stored once and reference-counted.
    /// </summary>
    [Fact]
    public async Task Output_ImageResources_DuplicateBytes_AreDeduplicated()
    {
        // Arrange: a scratch folder and a write that adds the same image bytes twice
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline writing duplicate images
        await RunPipelineAsync(temp, scratch, WriteDuplicateImages);

        // Assert: only one file exists and the manifest records two references
        Assert.Single(Directory.GetFiles(Path.Combine(scratch, "images")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = document.RootElement.GetProperty("images");
        Assert.Equal(1, images.GetArrayLength());
        Assert.Equal(2, images[0].GetProperty("references").GetInt32());
    }

    /// <summary>
    ///     Proves a rendered page is named by its document page number.
    /// </summary>
    [Fact]
    public async Task Output_PageResources_PageWritten_IsNamedByPageNumber()
    {
        // Arrange: a scratch folder and a write that renders page three
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline rendering a page
        await RunPipelineAsync(temp, scratch, WritePage);

        // Assert: the page file is named by its 1-based page number and recorded in the manifest
        Assert.True(File.Exists(Path.Combine(scratch, "pages", "page0003.png")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        Assert.Equal(3, document.RootElement.GetProperty("pages")[0].GetProperty("pageNumber").GetInt32());
    }

    /// <summary>
    ///     Proves a reported note reaches both the summary and the manifest.
    /// </summary>
    [Fact]
    public async Task Output_Notes_Reported_AppearInSummaryAndManifest()
    {
        // Arrange: a scratch folder and a write that reports one note
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline
        await RunPipelineAsync(temp, scratch, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nWith a note.\n", CancellationToken.None);
            sink.ReportNote(new ExtractionNote("One embedded image could not be decoded and was skipped."));
        });

        // Assert: both human- and machine-readable outputs carry the note
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        Assert.Contains("One embedded image could not be decoded and was skipped.", summary, StringComparison.Ordinal);
        Assert.Equal(
            "One embedded image could not be decoded and was skipped.",
            Assert.Single(manifest.RootElement.GetProperty("notes").EnumerateArray().ToList()).GetString());
    }

    /// <summary>
    ///     Proves a malicious image name is contained under the images folder.
    /// </summary>
    [Fact]
    public async Task Output_PathSafety_MaliciousImageName_IsContained()
    {
        // Arrange: a prepared scratch folder and sink, plus a hostile preferred image name
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, new ExtractionOptions());
        using var bytes = new MemoryStream([1, 2, 3, 4], writable: false);

        // Act: add an image whose preferred name attempts traversal
        var relativePath = await sink.AddImageAsync(bytes, new ImageHint("../../etc/passwd", "image/png"), Ct);

        // Assert: the allocated path stays under images/ and lands inside the scratch folder
        Assert.StartsWith("images/", relativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("..", relativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(folder.AbsolutePath, relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    ///     Proves two runs with a fixed timestamp produce byte-identical output.
    /// </summary>
    [Fact]
    public async Task Output_Determinism_FixedTimestamp_ProducesByteIdenticalOutput()
    {
        // Arrange: a stable scratch folder written twice with the same fixed timestamp
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the pipeline twice, snapshotting the first run's files
        await RunPipelineAsync(temp, scratch, WriteText);
        var firstSummary = Path.Combine(temp.Path, "first-summary.txt");
        var firstManifest = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(scratch, "summary.txt"), firstSummary);
        File.Copy(Path.Combine(scratch, "manifest.json"), firstManifest);
        await RunPipelineAsync(temp, scratch, WriteText);

        // Assert: both outputs are byte-identical between runs
        ContractAssert.FileEquals(firstSummary, Path.Combine(scratch, "summary.txt"));
        ContractAssert.FileEquals(firstManifest, Path.Combine(scratch, "manifest.json"));
    }

    /// <summary>
    ///     Proves two images with different provenance are each recorded honestly in the manifest.
    /// </summary>
    [Fact]
    public async Task Output_ImageProvenance_MixedTransforms_ManifestRecordsEachHonestly()
    {
        // Arrange: a scratch folder and a write that adds one passthrough and one re-encoded image
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the output pipeline
        await RunPipelineAsync(temp, scratch, WriteMixedTransformImages);

        // Assert: the two images are labeled distinctly in allocation order
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = document.RootElement.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal("passthrough", images[0].GetProperty("transform").GetString());
        Assert.Equal("decodedToPng", images[1].GetProperty("transform").GetString());
    }

    /// <summary>
    ///     Runs the output pipeline for a write action.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="scratchPath">The requested scratch folder path.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <returns>A task that completes when the pipeline finishes.</returns>
    private static async Task RunPipelineAsync(
        TempScratch temp,
        string scratchPath,
        Func<IExtractionSink, ValueTask> write)
    {
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };
        var folder = ScratchFolder.Prepare(scratchPath, options.ScratchFolder);
        var sink = new ExtractionSink(folder, options);

        // Let the scenario write through the sink, then finalize the standard artifacts
        await write(sink);
        var content = await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);
        var report = BuildReport(temp, options);
        await ManifestWriter.WriteAsync(folder, sink, report, content, Ct);
        await MetadataWriter.WriteAsync(folder, sink, Ct);
        await SummaryWriter.WriteAsync(folder, sink, report, content, Ct);
    }

    /// <summary>
    ///     Builds a consistent extraction report for driving the output writers.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <returns>A report whose selected backend is consistent across the output writers.</returns>
    private static ExtractionReport BuildReport(TempScratch temp, ExtractionOptions options)
    {
        var descriptor = new ExtractorDescriptor("text", "Text (stub)", [DocumentFormat.Text], 0);
        var environment = new ExtractionEnvironment("test-os", "x64", "test-runtime", "test-rid", []);
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(
            ExtractionOutcome.Produced,
            source,
            "0000",
            detection,
            descriptor,
            environment,
            options,
            FixedTimestamp,
            null,
            "DemaConsulting.DocDown.TestSupport");
    }

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nHello from the output subsystem.\n", CancellationToken.None);

    /// <summary>
    ///     Writes text and two identical images to exercise deduplication.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WriteDuplicateImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith images.\n", CancellationToken.None);
        var bytes = new byte[] { 9, 8, 7, 6, 5 };
        using var first = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(first, new ImageHint("logo", "image/png"), CancellationToken.None);
        using var second = new MemoryStream(bytes, writable: false);
        await sink.AddImageAsync(second, new ImageHint("logo", "image/png"), CancellationToken.None);
    }

    /// <summary>
    ///     Writes text and renders document page three.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WritePage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith a rendered page.\n", CancellationToken.None);
        using var png = new MemoryStream([1, 2, 3, 4, 5, 6], writable: false);
        await sink.AddPageAsync(3, png, CancellationToken.None);
    }

    /// <summary>
    ///     Writes one passthrough image and one re-encoded image.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WriteMixedTransformImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nTwo images.\n", CancellationToken.None);
        using (var one = new MemoryStream([10, 20, 30, 40], writable: false))
        {
            await sink.AddImageAsync(one, new ImageHint("photo", "image/jpeg"), CancellationToken.None);
        }

        using (var two = new MemoryStream([11, 21, 31, 41], writable: false))
        {
            await sink.AddImageAsync(
                two,
                new ImageHint("chart", "image/png", Transform: ImageTransform.DecodedToPng),
                CancellationToken.None);
        }
    }
}
