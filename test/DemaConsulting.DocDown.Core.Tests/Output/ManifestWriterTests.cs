using System.Text;
using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ManifestWriter"/>, proving the reduced schema shape, the status
///     values, note serialization, failure serialization, requested options, and deterministic JSON
///     encoding.
/// </summary>
public class ManifestWriterTests
{
    /// <summary>A fixed timestamp used to make the serialized manifest byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the manifest declares schema version 2.0.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_AnyRun_DeclaresSchemaVersionTwoPointZero()
    {
        // Arrange / Act: a produced text run serialized to a manifest
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, WriteText);
        using var document = await ParseManifestAsync(folder);

        // Assert: the reduced manifest shape pins the 2.0 schema
        Assert.Equal("2.0", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    /// <summary>
    ///     Proves a produced run records the tool, selected backend, scratch path, and produced status.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_ProducedRun_RecordsToolBackendScratchAndStatus()
    {
        // Arrange / Act: a clean text run serialized and round-tripped
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, WriteText);
        var json = await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct);
        var manifest = JsonSerializer.Deserialize(json, DocDownJsonContext.Default.ExtractionManifest);

        // Assert: the manifest records the tool, selected backend, scratch folder, and produced status
        Assert.NotNull(manifest);
        Assert.Equal("DocDown", manifest.Tool.Name);
        Assert.Equal("text", manifest.Extractor?.Id);
        Assert.Equal(folder.AbsolutePath, manifest.ScratchFolder);
        Assert.Equal("produced", manifest.Status);
    }

    /// <summary>
    ///     Proves an unreadable run serializes the failure block and unreadable status.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_UnreadableRun_RecordsFailureAndUnreadableStatus()
    {
        // Arrange: an unreadable report with no selected extractor
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        var failure = new ExtractionFailure(
            "No available extractor can process the detected format in this environment.",
            "No available extractor can process the detected format in this environment.\nDetected format: text (text/plain) - detected by file extension");
        var report = BuildReport(temp, Options(), ExtractionOutcome.Unreadable, null, failure);

        // Act: serialize the unreadable manifest
        await ManifestWriter.WriteAsync(folder, sink, report, null, Ct);
        using var document = await ParseManifestAsync(folder);

        // Assert: the failure is recorded verbatim and the extractor block is null
        Assert.Equal("unreadable", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("extractor").ValueKind);
        Assert.Equal(
            "No available extractor can process the detected format in this environment.",
            document.RootElement.GetProperty("failure").GetProperty("summary").GetString());
    }

    /// <summary>
    ///     Proves notes are serialized as a string array in emission order.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_NotesReported_SerializesMessagesInOrder()
    {
        // Arrange: a produced run that records two notes
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nWith notes.\n", CancellationToken.None);
            sink.ReportNote(new ExtractionNote("The first note."));
            sink.ReportNote(new ExtractionNote("The second note."));
        });

        // Act: parse the manifest
        using var document = await ParseManifestAsync(folder);
        var notes = document.RootElement.GetProperty("notes").EnumerateArray().Select(note => note.GetString()).ToArray();

        // Assert: notes are plain strings, ordered as emitted
        Assert.Equal(["The first note.", "The second note."], notes);
    }

    /// <summary>
    ///     Proves the requested-options block contains only the surviving fields.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_RequestedOptions_ContainsReducedFieldSet()
    {
        // Arrange: a produced run with distinctive requested options
        using var temp = new TempScratch();
        var options = new ExtractionOptions
        {
            TimestampUtc = FixedTimestamp,
            RenderPages = true,
            Pages = new PageRange(2, 4),
            IncludeEmbeddedImages = false,
            ImageOutput = ImageOutputMode.ForcePng,
            MaxImageDimensionPx = 600,
            MaxImageBytes = 1024,
            PageRenderDpi = 300,
            ContentSplit = ContentSplitMode.PerPart,
            ScratchFolder = ScratchFolderMode.Overwrite
        };
        var folder = await RunProducedAsync(temp, WriteText, options);

        // Act: parse the manifest
        using var document = await ParseManifestAsync(folder);
        var requested = document.RootElement.GetProperty("requestedOptions");

        // Assert: the surviving fields are present and the removed fields stay absent
        Assert.True(requested.GetProperty("renderPages").GetBoolean());
        Assert.Equal("2-4", requested.GetProperty("pages").GetString());
        Assert.False(requested.GetProperty("includeEmbeddedImages").GetBoolean());
        Assert.Equal("forcePng", requested.GetProperty("imageOutput").GetString());
        Assert.Equal(600, requested.GetProperty("maxImageDimensionPx").GetInt32());
        Assert.Equal(1024, requested.GetProperty("maxImageBytes").GetInt64());
        Assert.Equal(300, requested.GetProperty("pageRenderDpi").GetInt32());
        Assert.Equal("perPart", requested.GetProperty("contentSplit").GetString());
        Assert.Equal("overwrite", requested.GetProperty("scratchFolder").GetString());
        Assert.False(requested.TryGetProperty("preferredExtractorId", out _));
        Assert.False(requested.TryGetProperty("requireCapabilities", out _));
    }

    /// <summary>
    ///     Proves the removed top-level manifest keys are absent from the new schema.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_ProducedRun_OmitsRemovedTopLevelKeys()
    {
        // Arrange / Act: a clean produced run serialized to a manifest
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, WriteText);
        using var document = await ParseManifestAsync(folder);
        var root = document.RootElement;

        // Assert: the reduced manifest no longer serializes the removed blocks
        Assert.False(root.TryGetProperty("complete", out _));
        Assert.False(root.TryGetProperty("selection", out _));
        Assert.False(root.TryGetProperty("artifacts", out _));
        Assert.False(root.TryGetProperty("gaps", out _));
        Assert.False(root.TryGetProperty("diagnostics", out _));
    }

    /// <summary>
    ///     Proves the extractor block contains the reduced field set with no capabilities.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_SelectedExtractor_SerializesReducedExtractorShape()
    {
        // Arrange / Act: a produced run serialized to a manifest
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, WriteText);
        using var document = await ParseManifestAsync(folder);
        var extractor = document.RootElement.GetProperty("extractor");

        // Assert: the extractor block records identity, package, and priority only
        Assert.Equal("text", extractor.GetProperty("id").GetString());
        Assert.Equal("Text (stub)", extractor.GetProperty("displayName").GetString());
        Assert.Equal("DemaConsulting.DocDown.TestSupport", extractor.GetProperty("package").GetString());
        Assert.Equal(0, extractor.GetProperty("priority").GetInt32());
        Assert.False(extractor.TryGetProperty("capabilities", out _));
    }

    /// <summary>
    ///     Proves the JSON encoding is deterministic: byte-identical, no BOM, and <c>\n</c>-only.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalNoBomJson()
    {
        // Arrange: a prepared folder, sink, and fixed report
        using var temp = new TempScratch();
        var options = Options();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        await WriteText(sink);
        var report = BuildReport(temp, options, ExtractionOutcome.Produced, SuccessExtractor(), null);
        var content = await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);

        // Act: serialize twice into the same folder, snapshotting the first output
        await ManifestWriter.WriteAsync(folder, sink, report, content, Ct);
        var firstSnapshot = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(folder.AbsolutePath, "manifest.json"), firstSnapshot);
        await ManifestWriter.WriteAsync(folder, sink, report, content, Ct);
        var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct);

        // Assert: the bytes are stable, BOM-free, and use Unix line endings only
        ContractAssert.FileEquals(firstSnapshot, Path.Combine(folder.AbsolutePath, "manifest.json"));
        Assert.False(manifestBytes.Length >= 3 && manifestBytes[0] == 0xEF && manifestBytes[1] == 0xBB && manifestBytes[2] == 0xBF);
        Assert.DoesNotContain("\r", Encoding.UTF8.GetString(manifestBytes), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a re-encoded image reaches the manifest as the camelCase <c>decodedToPng</c> value.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_DecodedToPngImage_SerializesCamelCaseTransform()
    {
        // Arrange / Act: a run whose single image was decoded and re-encoded as PNG
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, WriteDecodedImage);
        using var document = await ParseManifestAsync(folder);

        // Assert: the image transform is serialized with the expected camelCase value
        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("decodedToPng", image.GetProperty("transform").GetString());
    }

    /// <summary>
    ///     Proves image referrer sets, template provenance, and descriptions reach the manifest.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_ImageMetadata_SerializesReferrersTemplateAndDescription()
    {
        // Arrange / Act: a run whose single image carries every optional manifest-facing field
        using var temp = new TempScratch();
        var folder = await RunProducedAsync(temp, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nWith a described image.\n", CancellationToken.None);
            using var bytes = new MemoryStream([9, 8, 7, 6], writable: false);
            await sink.AddImageAsync(
                bytes,
                new ImageHint(
                    "figure",
                    "image/png",
                    SourcePages: [3, 7],
                    ReferencedByTemplate: true,
                    Description: "A wiring diagram",
                    DescriptionSource: "caption"),
                CancellationToken.None);
        });
        using var document = await ParseManifestAsync(folder);

        // Assert: the manifest records the full image metadata set honestly
        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal([3, 7], image.GetProperty("sourcePages").EnumerateArray().Select(element => element.GetInt32()).ToArray());
        Assert.Equal(3, image.GetProperty("sourcePage").GetInt32());
        Assert.True(image.GetProperty("referencedByTemplate").GetBoolean());
        Assert.Equal("A wiring diagram", image.GetProperty("description").GetString());
        Assert.Equal("caption", image.GetProperty("descriptionSource").GetString());
    }

    /// <summary>
    ///     Runs the content and manifest pipeline for a produced scenario.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <param name="options">The effective options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The prepared folder whose manifest was written.</returns>
    private static async Task<ScratchFolder> RunProducedAsync(
        TempScratch temp,
        Func<IExtractionSink, ValueTask> write,
        ExtractionOptions? options = null)
    {
        var effective = options ?? Options();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), effective.ScratchFolder);
        var sink = new ExtractionSink(folder, effective);
        await write(sink);
        var content = await ContentWriter.WriteAsync(sink, effective.ContentSplit, "Document", Ct);
        var report = BuildReport(temp, effective, ExtractionOutcome.Produced, SuccessExtractor(), null);
        await ManifestWriter.WriteAsync(folder, sink, report, content, Ct);
        return folder;
    }

    /// <summary>
    ///     Parses the serialized manifest from a scratch folder.
    /// </summary>
    /// <param name="folder">The scratch folder whose manifest is read.</param>
    /// <returns>The parsed JSON document.</returns>
    private static async Task<JsonDocument> ParseManifestAsync(ScratchFolder folder) =>
        JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct));

    /// <summary>
    ///     Builds a consistent extraction report for the manifest writer.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <param name="outcome">The recorded outcome.</param>
    /// <param name="selected">The selected extractor, or <see langword="null"/>.</param>
    /// <param name="failure">The failure, or <see langword="null"/>.</param>
    /// <returns>The assembled report.</returns>
    private static ExtractionReport BuildReport(
        TempScratch temp,
        ExtractionOptions options,
        ExtractionOutcome outcome,
        ExtractorDescriptor? selected,
        ExtractionFailure? failure)
    {
        var environment = new ExtractionEnvironment("TestOS", "X64", "test-runtime", "test-rid", []);
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(
            outcome,
            source,
            "0000",
            detection,
            selected,
            environment,
            options,
            FixedTimestamp,
            failure,
            selected is null ? null : "DemaConsulting.DocDown.TestSupport");
    }

    /// <summary>
    ///     Creates the selected extractor used by the produced scenarios.
    /// </summary>
    /// <returns>The selected extractor descriptor.</returns>
    private static ExtractorDescriptor SuccessExtractor() =>
        new("text", "Text (stub)", [DocumentFormat.Text], 0);

    /// <summary>
    ///     Creates the fixed options used across the manifest scenarios.
    /// </summary>
    /// <returns>Options stamped with the fixed timestamp.</returns>
    private static ExtractionOptions Options() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nManifest writer content.\n", CancellationToken.None);

    /// <summary>
    ///     Writes text and one image the extractor reports as decoded and re-encoded as PNG.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    private static async ValueTask WriteDecodedImage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith a re-encoded image.\n", CancellationToken.None);
        using var bytes = new MemoryStream([3, 1, 4, 1, 5], writable: false);
        await sink.AddImageAsync(
            bytes,
            new ImageHint("figure", "image/png", Transform: ImageTransform.DecodedToPng),
            CancellationToken.None);
    }
}
