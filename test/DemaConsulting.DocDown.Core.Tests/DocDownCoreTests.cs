using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests;

/// <summary>
///     System-level integration tests for the DocDown.Core extraction system, driven end to end
///     through <see cref="DocDownEngine"/> with synthetic stub backends.
/// </summary>
public class DocDownCoreTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a successful text extraction produces the standard layout.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_TextDocument_ProducesContractLayout()
    {
        // Arrange: a plain-text input document and an available text backend
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction into a fresh scratch folder
        var result = await engine.ExtractAsync(temp.CreateFile("document.txt", "hello world"), scratch, FixedOptions(), Ct);

        // Assert: the layout is produced with no failure and the root artifacts exist
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Null(result.Failure);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a detected docx document with no Word backend fails honestly, naming the Word package.
    /// </summary>
    [Fact]
    public async Task DocDownCore_ExtractAsync_DocxWithoutWordBackend_FailsHonestlyNamingThePackage()
    {
        // Arrange: only a text backend is registered, and the input is named as a Word document
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction against the detected docx format
        var result = await engine.ExtractAsync(
            temp.CreateFile("report.docx", "arbitrary bytes that are not a real package"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the unreadable failure names the format and its providing package
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains("docx", result.Failure.Explanation, StringComparison.Ordinal);
        Assert.Contains("DemaConsulting.DocDown.Word", result.Failure.Explanation, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var source = manifest.RootElement.GetProperty("source");
        Assert.Equal("docx", source.GetProperty("format").GetString());
        Assert.Equal("extension", source.GetProperty("detectionBasis").GetString());
    }

    /// <summary>
    ///     Proves a legacy binary <c>.doc</c> input fails with the fixed unsupported-format wording.
    /// </summary>
    [Fact]
    public async Task DocDownCore_ExtractAsync_DocWithoutWordBackend_FailsNoExtractorNamingLegacyFormat()
    {
        // Arrange: only a text backend is registered, and the input is named as a legacy Word document
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction against the detected legacy format
        var result = await engine.ExtractAsync(
            temp.CreateFile("report.doc", "arbitrary bytes that are not a real document"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the failure states the legacy-binary fact and names no package
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Explanation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DemaConsulting.DocDown.Word", result.Failure.Explanation, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves an unavailable backend's reason appears verbatim in the summary when it is the only option.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary()
    {
        // Arrange: the single text backend is unavailable with a distinctive reason
        using var temp = new TempScratch();
        const string reason = "the native text renderer 'libtxt 4.2' is not installed on this host";
        var engine = BuildEngine(StubExtractor.Unavailable("text", [DocumentFormat.Text], reason));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with no available backend
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the summary carries the unavailability reason verbatim
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.Contains(reason, summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves requesting page rendering an available backend cannot provide records a note while still producing output.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_PagesRequestedButUnsupported_ProducesWithNote()
    {
        // Arrange: the available backend offers text only, but the caller requests rendered pages
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var options = FixedOptions();
        options.RenderPages = true;
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with an unsatisfied render request
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            options,
            Ct);

        // Assert: the standard layout is produced and the missing renderer is described as a note
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Contains(
            result.Notes,
            note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(scratch, "pages")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves an unrecognized format returns an unreadable result with unknown detection.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_UnknownFormat_ReturnsUnreadableResult()
    {
        // Arrange: an input whose extension and content match no known format
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction against the unknown document
        var result = await engine.ExtractAsync(
            temp.CreateFile("mystery.xyz", "no recognizable signature here"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the failure is unreadable and the detected format is unknown
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.True(result.DetectedFormat.Format.IsUnknown);
        Assert.Contains("could not be recognized", result.Failure?.Summary, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a throwing backend still writes the summary and manifest with an explanation.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_Failure_StillWritesSummaryAndManifest()
    {
        // Arrange: a backend that throws partway through extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Failing("failing", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction so the backend faults
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the run is unreadable but still writes the full layout
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Failure?.Explanation));
        Assert.False(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves two runs into the same folder with a fixed timestamp are byte-identical.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_RepeatedRun_IsByteIdentical()
    {
        // Arrange: a deterministic engine, a fixed timestamp, and a stable scratch folder
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run twice into the same scratch folder, snapshotting the first run's files
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        var firstSummary = Path.Combine(temp.Path, "first-summary.txt");
        var firstManifest = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(scratch, "summary.txt"), firstSummary);
        File.Copy(Path.Combine(scratch, "manifest.json"), firstManifest);
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: both outputs are byte-identical between runs
        ContractAssert.FileEquals(firstSummary, Path.Combine(scratch, "summary.txt"));
        ContractAssert.FileEquals(firstManifest, Path.Combine(scratch, "manifest.json"));
    }

    /// <summary>
    ///     Proves mutating the options after one call does not affect that call's result.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult()
    {
        // Arrange: one options instance, two scratch folders, and a backend that writes one image
        using var temp = new TempScratch();
        var engine = BuildEngine(new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = WriteImageAsync
        });
        var input = temp.CreateFile("document.txt", "hello world");
        var firstScratch = Path.Combine(temp.Path, "out1");
        var secondScratch = Path.Combine(temp.Path, "out2");
        var options = FixedOptions();
        options.IncludeEmbeddedImages = true;

        // Act: run once, mutate the options, then run again
        await engine.ExtractAsync(input, firstScratch, options, Ct);
        options.IncludeEmbeddedImages = false;
        await engine.ExtractAsync(input, secondScratch, options, Ct);

        // Assert: the first run kept its original snapshot while the second reflects the mutation
        Assert.Equal(1, ReadImageCount(firstScratch));
        Assert.Equal(0, ReadImageCount(secondScratch));
    }

    /// <summary>
    ///     Proves the self-test seam includes Core's own current case names.
    /// </summary>
    [Fact]
    public void DocDownCore_GetSelfTestCases_IncludesCurrentCoreCases()
    {
        // Arrange: an engine with one available self-validating backend
        var backend = StubExtractor.Available("backend", [DocumentFormat.Text]);
        backend.SelfTestCases.Add(new SelfTestCase("backend.case", "backend", _ => SelfTestResult.Passed(TimeSpan.Zero)));
        var engine = BuildEngine(backend);

        // Act: enumerate the self-test suite
        var cases = engine.GetSelfTestCases();

        // Assert: the two Core cases and the backend case are all present
        Assert.Contains(cases, testCase => testCase.Name == "core.layout-invariance");
        Assert.Contains(cases, testCase => testCase.Name == "core.manifest-schema");
        Assert.Contains(cases, testCase => testCase.Name == "backend.case");
    }

    /// <summary>
    ///     Proves the manifest states, per image, how that image was produced.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_ImageProvenance_ManifestRecordsTransformPerImage()
    {
        // Arrange: a backend scripted to add one verbatim image and one re-encoded image
        using var temp = new TempScratch();
        var engine = BuildEngine(new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = WriteImagesOfDifferingProvenanceAsync
        });
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction end to end through the engine
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the manifest records distinct per-image provenance
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal("passthrough", images[0].GetProperty("transform").GetString());
        Assert.Equal("image/jpeg", images[0].GetProperty("mediaType").GetString());
        Assert.Equal("decodedToPng", images[1].GetProperty("transform").GetString());
        Assert.Equal("image/png", images[1].GetProperty("mediaType").GetString());
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Builds an engine from the given extractors.
    /// </summary>
    /// <param name="extractors">The extractors to register.</param>
    /// <returns>The built engine.</returns>
    private static DocDownEngine BuildEngine(params IDocumentExtractor[] extractors)
    {
        var builder = new DocDownBuilder();
        foreach (var extractor in extractors)
        {
            builder.AddExtractor(extractor);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates options with a fixed timestamp for deterministic output.
    /// </summary>
    /// <returns>Options stamped with a fixed UTC timestamp.</returns>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Writes one image so the image-suppression option is observable in end-to-end output.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="context">The extraction context to write through.</param>
    /// <returns>An outcome of <see cref="ExtractionOutcome.Produced"/>.</returns>
    private static async ValueTask<ExtractionOutcome> WriteImageAsync(DocumentSource source, IExtractionContext context)
    {
        _ = source;
        await context.Sink.WriteContentAsync("# Document\n\nWith an image.\n", context.CancellationToken);
        using var image = new MemoryStream([1, 2, 3, 4], writable: false);
        await context.Sink.AddImageAsync(image, new ImageHint("figure", "image/png"), context.CancellationToken);
        return ExtractionOutcome.Produced;
    }

    /// <summary>
    ///     Reads the manifest image count from a completed extraction folder.
    /// </summary>
    /// <param name="scratchFolder">The scratch folder whose manifest is read.</param>
    /// <returns>The number of image entries recorded in the manifest.</returns>
    private static int ReadImageCount(string scratchFolder)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(scratchFolder, "manifest.json")));
        return manifest.RootElement.GetProperty("images").GetArrayLength();
    }

    /// <summary>
    ///     Writes text plus one passthrough image and one decoded-to-PNG image through the sink.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="context">The extraction context to write through.</param>
    /// <returns>An outcome of <see cref="ExtractionOutcome.Produced"/>.</returns>
    private static async ValueTask<ExtractionOutcome> WriteImagesOfDifferingProvenanceAsync(
        DocumentSource source,
        IExtractionContext context)
    {
        _ = source;
        await context.Sink.WriteContentAsync("# Document\n\nTwo images.\n", context.CancellationToken);

        using (var first = new MemoryStream([10, 20, 30, 40], writable: false))
        {
            await context.Sink.AddImageAsync(first, new ImageHint("photo", "image/jpeg"), context.CancellationToken);
        }

        using (var second = new MemoryStream([11, 21, 31, 41], writable: false))
        {
            await context.Sink.AddImageAsync(
                second,
                new ImageHint("chart", "image/png", Transform: ImageTransform.DecodedToPng),
                context.CancellationToken);
        }

        return ExtractionOutcome.Produced;
    }
}
