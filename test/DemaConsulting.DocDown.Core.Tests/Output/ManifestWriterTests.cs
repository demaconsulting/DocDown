using System.Text;
using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ManifestWriter"/>, proving the schema version, the machine-readable
///     twin, the completeness ledger, the gap-synthesis invariant, the complete flag, and the
///     deterministic JSON encoding.
/// </summary>
/// <remarks>
///     These tests drive a real <see cref="ExtractionSink"/> (a documented dependency) over a
///     prepared <see cref="ScratchFolder"/>, reconcile with <see cref="ManifestWriter.Reconcile"/>,
///     and serialize with <see cref="ManifestWriter.WriteAsync"/>. Each is named for the unit
///     requirement it evidences: schema version, machine twin, completeness ledger, gap synthesis,
///     complete flag, deterministic JSON, and image-transform projection.
/// </remarks>
public class ManifestWriterTests
{
    /// <summary>A fixed timestamp used to make the serialized manifest byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the manifest declares the pinned schema version (SchemaVersion).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_AnyRun_DeclaresSchemaVersionOnePointTwo()
    {
        // Arrange + Act: a clean text run serialized to a manifest
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteText);
        using var document = await ParseManifestAsync(folder);

        // Assert: the manifest pins the 1.2 schema version the contract verifier expects
        Assert.Equal("1.2", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    /// <summary>
    ///     Proves the manifest is the machine twin naming the tool and selected backend (MachineTwin).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_SuccessfulRun_RecordsToolAndBackend()
    {
        // Arrange + Act: a clean text run serialized and re-read through the source-generated context
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteText);
        var json = await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct);
        var manifest = JsonSerializer.Deserialize(json, DocDownJsonContext.Default.ExtractionManifest);

        // Assert: the manifest round-trips and records the DocDown tool, the backend, and the scratch path
        Assert.NotNull(manifest);
        Assert.Equal("DocDown", manifest.Tool.Name);
        Assert.Equal("text", manifest.Extractor?.Id);
        Assert.Equal(folder.AbsolutePath, manifest.ScratchFolder);
    }

    /// <summary>
    ///     Proves the completeness ledger records every artifact slot and marks written content present (CompletenessLedger).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Reconcile_TextWritten_MarksLedgerSlotsAndContentPresent()
    {
        // Arrange + Act: a clean text run reconciled
        using var temp = new TempScratch();
        var (_, reconciliation) = await RunAsync(temp, WriteText);
        var ledger = reconciliation.Ledger;

        // Assert: the always-present slots and the written content are present in the ledger
        Assert.Equal(ArtifactStatus.Present, ledger.Summary.Status);
        Assert.Equal(ArtifactStatus.Present, ledger.Manifest.Status);
        Assert.Equal(ArtifactStatus.Present, ledger.Content.Status);
    }

    /// <summary>
    ///     Proves the manifest serializes every ledger slot (CompletenessLedger).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_LedgerBlock_ContainsEverySlot()
    {
        // Arrange + Act: a clean text run serialized
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteText);
        using var document = await ParseManifestAsync(folder);
        var artifacts = document.RootElement.GetProperty("artifacts");

        // Assert: the ledger block carries all five slots
        Assert.True(artifacts.TryGetProperty("summary", out _));
        Assert.True(artifacts.TryGetProperty("manifest", out _));
        Assert.True(artifacts.TryGetProperty("content", out _));
        Assert.True(artifacts.TryGetProperty("images", out _));
        Assert.True(artifacts.TryGetProperty("pages", out _));
    }

    /// <summary>
    ///     Proves an unexplained absence is given a synthesized gap and a DD0701 diagnostic (GapSynthesis).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Reconcile_UnexplainedAbsence_SynthesizesGapWithDiagnostic()
    {
        // Arrange + Act: a run that claims images were found but writes and explains none
        using var temp = new TempScratch();
        var (_, reconciliation) = await RunAsync(temp, WriteSilentImages);

        // Assert: reconciliation makes the silent hole structurally impossible with a gap and a DD0701 warning
        Assert.NotEmpty(reconciliation.Gaps);
        Assert.Contains(reconciliation.Gaps, gap => gap.Target == "images/");
        Assert.Contains(reconciliation.Diagnostics, diagnostic => diagnostic.Code == "DD0701");
    }

    /// <summary>
    ///     Proves the synthesized gap is serialized into the manifest with a reason (GapSynthesis).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_SynthesizedGap_AppearsInManifestWithReason()
    {
        // Arrange + Act: a silent-images run serialized
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteSilentImages);
        using var document = await ParseManifestAsync(folder);
        var gaps = document.RootElement.GetProperty("gaps");

        // Assert: the manifest carries at least one gap, each with a non-empty reason
        Assert.True(gaps.GetArrayLength() > 0);
        Assert.All(gaps.EnumerateArray(), gap =>
            Assert.False(string.IsNullOrWhiteSpace(gap.GetProperty("reason").GetString())));
    }

    /// <summary>
    ///     Proves the complete flag is true with no gaps and false with gaps (CompleteFlag).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_CompleteFlag_EqualsWhetherGapsAreEmpty()
    {
        // Arrange + Act: a clean run and a gapped run, both serialized
        using var cleanTemp = new TempScratch();
        var (cleanFolder, _) = await RunAsync(cleanTemp, WriteText);
        using var gappedTemp = new TempScratch();
        var (gappedFolder, _) = await RunAsync(gappedTemp, WriteSilentImages);
        using var cleanDocument = await ParseManifestAsync(cleanFolder);
        using var gappedDocument = await ParseManifestAsync(gappedFolder);

        // Assert: complete tracks exactly whether the gap list is empty
        Assert.True(cleanDocument.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(0, cleanDocument.RootElement.GetProperty("gaps").GetArrayLength());
        Assert.False(gappedDocument.RootElement.GetProperty("complete").GetBoolean());
        Assert.True(gappedDocument.RootElement.GetProperty("gaps").GetArrayLength() > 0);
    }

    /// <summary>
    ///     Proves the JSON is deterministic: byte-identical, no BOM, and no carriage returns (DeterministicJson).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalNoBomJson()
    {
        // Arrange: a prepared folder, sink, and fixed report and reconciliation
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await WriteText(sink);
        var report = BuildReport(temp, Options());
        var content = await ContentWriter.WriteAsync(sink, Options().ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);

        // Act: serialize twice into the same folder, snapshotting the first output
        await ManifestWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        var firstSnapshot = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(folder.AbsolutePath, "manifest.json"), firstSnapshot);
        await ManifestWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct);

        // Assert: the two serializations are byte-identical, carry no BOM, and use \n line endings only
        ContractAssert.FileEquals(firstSnapshot, Path.Combine(folder.AbsolutePath, "manifest.json"));
        Assert.False(manifestBytes.Length >= 3 && manifestBytes[0] == 0xEF && manifestBytes[1] == 0xBB && manifestBytes[2] == 0xBF);
        Assert.DoesNotContain("\r", Encoding.UTF8.GetString(manifestBytes), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a re-encoded image reaches the manifest as the camelCase <c>decodedToPng</c> value
    ///     (ImageTransformProjected).
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Write_DecodedToPngImage_SerializesCamelCaseTransform()
    {
        // Arrange + Act: a run whose single image was decoded and re-encoded as PNG
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteDecodedImage);
        using var document = await ParseManifestAsync(folder);

        // Assert: the value the schema always documented is now actually producible end to end
        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("decodedToPng", image.GetProperty("transform").GetString());
    }

    /// <summary>
    ///     Proves the multi-referrer page associations reach the manifest: <c>sourcePages</c> lists
    ///     every referrer, <c>sourcePage</c> is its deterministic first, and <c>referencedByTemplate</c>
    ///     records template provenance — the honest slide/template/orphan distinction.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Write_ImageWithReferrers_SerializesSourcePagesAndTemplateFlag()
    {
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nWith a reused image.\n", CancellationToken.None);
            using var bytes = new MemoryStream([4, 3, 2, 1]);
            await sink.AddImageAsync(bytes, new ImageHint("figure", "image/png",
                SourcePages: [3, 7], ReferencedByTemplate: true), CancellationToken.None);
        });
        using var document = await ParseManifestAsync(folder);

        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal([3, 7], image.GetProperty("sourcePages").EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.Equal(3, image.GetProperty("sourcePage").GetInt32());
        Assert.True(image.GetProperty("referencedByTemplate").GetBoolean());
    }

    /// <summary>
    ///     Proves an image description and its provenance reach the manifest as image metadata.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Write_ImageWithDescription_SerializesDescriptionAndSource()
    {
        // Arrange + Act: a run whose single image carries an authored description and its source
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nWith a described image.\n", CancellationToken.None);
            using var bytes = new MemoryStream([9, 8, 7, 6]);
            await sink.AddImageAsync(bytes, new ImageHint("figure", "image/png",
                Description: "A wiring diagram", DescriptionSource: "caption"), CancellationToken.None);
        });
        using var document = await ParseManifestAsync(folder);

        // Assert: both the description and its provenance are recorded verbatim
        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal("A wiring diagram", image.GetProperty("description").GetString());
        Assert.Equal("caption", image.GetProperty("descriptionSource").GetString());
    }

    /// <summary>
    ///     Proves an image with no description records an explicit null rather than an invented one.
    /// </summary>
    [Fact]
    public async Task ManifestWriter_Write_ImageWithoutDescription_SerializesNullDescription()
    {
        // Arrange + Act: a run whose single image was given no description in its hint
        using var temp = new TempScratch();
        var (folder, _) = await RunAsync(temp, WriteDecodedImage);
        using var document = await ParseManifestAsync(folder);

        // Assert: the absence is honest — an explicit null, never a guessed description
        var image = Assert.Single(document.RootElement.GetProperty("images").EnumerateArray().ToList());
        Assert.Equal(JsonValueKind.Null, image.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, image.GetProperty("descriptionSource").ValueKind);
    }

    /// <summary>
    ///     Proves every declared image transform projects to a distinct camelCase manifest string
    ///     (ImageTransformProjected).
    /// </summary>
    /// <remarks>
    ///     This is the symmetry gate. The compiler already guarantees that no extractor can produce a
    ///     transform outside the enumeration, so "producible implies documented" holds structurally.
    ///     This test closes the other direction — "documented implies producible" — by driving every
    ///     declared member through the real writer, so a future member added without a projection
    ///     fails here rather than at a consumer's manifest.
    /// </remarks>
    [Fact]
    public async Task ManifestWriter_TransformString_EveryImageTransformValue_Projects()
    {
        // Arrange: one distinctly-byte-valued image per declared transform so none is deduplicated
        var transforms = Enum.GetValues<ImageTransform>();
        using var temp = new TempScratch();

        // Act: write every transform through the real sink and serialize the manifest
        var (folder, _) = await RunAsync(temp, async sink =>
        {
            await sink.WriteContentAsync("# Document\n\nEvery transform.\n", CancellationToken.None);
            for (var index = 0; index < transforms.Length; index++)
            {
                using var bytes = new MemoryStream([(byte)index, 0xAA, 0xBB]);
                await sink.AddImageAsync(
                    bytes, new ImageHint(null, "image/png", Transform: transforms[index]), CancellationToken.None);
            }
        });
        using var document = await ParseManifestAsync(folder);

        // Assert: every member produced a non-empty, camelCase, distinct manifest string
        var serialized = document.RootElement.GetProperty("images").EnumerateArray()
            .Select(image => image.GetProperty("transform").GetString()!).ToList();
        Assert.Equal(transforms.Length, serialized.Count);
        Assert.All(serialized, value =>
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.True(char.IsLower(value[0]), $"'{value}' is not camelCase.");
        });
        Assert.Equal(transforms.Length, serialized.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Proves reconciling with a null sink is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public void ManifestWriter_Reconcile_NullSink_ThrowsArgumentNullException()
    {
        // Arrange: a report but no sink
        using var temp = new TempScratch();
        var report = BuildReport(temp, Options());

        // Act + Assert: the recorded-state source is mandatory
        Assert.Throws<ArgumentNullException>(() => ManifestWriter.Reconcile(null!, report, null));
    }

    /// <summary>
    ///     Runs the sink, content, and manifest pipeline for a write action.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <returns>The prepared folder and the reconciliation output.</returns>
    /// <remarks>Drives the real writers so each test inspects an authentic manifest.</remarks>
    private static async Task<(ScratchFolder Folder, ReconciliationResult Reconciliation)> RunAsync(
        TempScratch temp, Func<IExtractionSink, ValueTask> write)
    {
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, Options());
        await write(sink);
        var report = BuildReport(temp, Options());
        var content = await ContentWriter.WriteAsync(sink, Options().ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await ManifestWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        return (folder, reconciliation);
    }

    /// <summary>
    ///     Parses the serialized manifest from a scratch folder.
    /// </summary>
    /// <param name="folder">The scratch folder whose manifest is read.</param>
    /// <returns>The parsed JSON document.</returns>
    /// <remarks>The caller disposes the returned document.</remarks>
    private static async Task<JsonDocument> ParseManifestAsync(ScratchFolder folder) =>
        JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder.AbsolutePath, "manifest.json"), Ct));

    /// <summary>
    ///     Builds a consistent extraction report for the manifest writer.
    /// </summary>
    /// <param name="temp">The owning temporary folder used to materialize a source document.</param>
    /// <param name="options">The effective options the report records.</param>
    /// <returns>The assembled report whose selection names the text backend.</returns>
    /// <remarks>Uses a fixed timestamp and a real source file so the serialization is deterministic and disposable.</remarks>
    private static ExtractionReport BuildReport(TempScratch temp, ExtractionOptions options)
    {
        var descriptor = new ExtractorDescriptor("text", "Text (stub)", [DocumentFormat.Text], ExtractorCapabilities.Text, 0);
        var trace = new[] { new CandidateVerdict("text", "Text (stub)", 0, CandidateOutcome.Selected, "selected for the test") };
        var selection = new SelectionResult(descriptor, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.Text, trace, null);
        var environment = new ExtractionEnvironment("TestOS", "X64", "test-runtime", "test-rid", []);
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        return new ExtractionReport(ExtractionOutcome.Succeeded, source, "0000", detection, selection, environment, options, FixedTimestamp, null);
    }

    /// <summary>
    ///     Creates the fixed options used across the manifest scenarios.
    /// </summary>
    /// <returns>Options stamped with the fixed timestamp.</returns>
    /// <remarks>The fixed timestamp makes the deterministic-JSON assertion meaningful.</remarks>
    private static ExtractionOptions Options() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    /// <remarks>The canonical clean write used by the schema, ledger, and complete-flag scenarios.</remarks>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nManifest writer content.\n", CancellationToken.None);

    /// <summary>
    ///     Writes text and claims images were found without writing or explaining them.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Leaves an unexplained image absence that reconciliation must catch with a synthesized gap.</remarks>
    private static async ValueTask WriteSilentImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nText present, images missing.\n", CancellationToken.None);
        sink.ReportFound(GapKind.Images, 2);
    }

    /// <summary>
    ///     Writes text and one image the extractor reports as decoded and re-encoded as PNG.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Exercises the non-default provenance path so the projected string can be asserted.</remarks>
    private static async ValueTask WriteDecodedImage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nWith a re-encoded image.\n", CancellationToken.None);
        using var bytes = new MemoryStream([3, 1, 4, 1, 5]);
        await sink.AddImageAsync(
            bytes, new ImageHint("figure", "image/png", Transform: ImageTransform.DecodedToPng), CancellationToken.None);
    }
}
