using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests;

/// <summary>
///     System-level integration tests for the DocDown.Core extraction system, driven end to end
///     through <see cref="DocDownEngine"/> with configurable stub backends.
/// </summary>
/// <remarks>
///     These tests exercise the whole pipeline — detection, selection, orchestration, and output —
///     and assert the library's honesty guarantees. The degraded-by-default matrix is treated as
///     the common case: most rows below produce an incomplete or failed run, and every extraction
///     test ends by confirming the contract verifier finds no violations, so a reported gap always
///     matches the filesystem.
/// </remarks>
public class DocDownCoreTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a fully successful text extraction produces the complete contract layout with no gaps.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_TextDocument_ProducesContractLayout()
    {
        // Arrange: a fully capable text backend and a plain-text input document
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available(
            "text", [DocumentFormat.Text], ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction into a fresh scratch folder
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run succeeds, is complete, has no gaps, and writes the full honest layout
        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Gaps);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that when no backend supports the detected format the run fails but still writes the layout.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_NoBackendForFormat_FailsAndStillWritesLayout()
    {
        // Arrange: the only backend supports docx, but the input is plain text
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("docx", [DocumentFormat.Docx], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction against an unsupported format
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run fails with the no-extractor failure yet still writes the honest layout
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a .docx input with no Word backend fails honestly, naming the package that provides it.
    /// </summary>
    [Fact]
    public async Task DocDownCore_ExtractAsync_DocxWithoutWordBackend_FailsHonestlyNamingThePackage()
    {
        // Arrange: only a text backend is registered, and the input is named as a Word document
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("report.docx", "arbitrary bytes that are not a real package");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction, which must recognize docx by extension and then find no backend
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the failure is the no-extractor kind with the documented code
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Equal("DD0402", result.Failure.Code);

        // Assert: the remedy is genuinely helpful — it names the package that provides the backend
        Assert.Contains("docx", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.Contains("DemaConsulting.DocDown.Word", result.Failure.Remedy, StringComparison.Ordinal);

        // Assert: the manifest records the format and that the extension is what identified it
        var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var source = manifest.RootElement.GetProperty("source");
        Assert.Equal("docx", source.GetProperty("format").GetString());
        Assert.Equal("extension", source.GetProperty("detectionBasis").GetString());

        // Assert: a failed run still writes the full layout and still verifies clean
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);

        // Assert: the human-readable summary carries the same honest remedy
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("DemaConsulting.DocDown.Word", summary, StringComparison.Ordinal);

        // Assert: the end-to-end surface never tells the user to install a package that cannot be
        // installed, and makes no claim that would expire when the extractor packages ship
        Assert.DoesNotContain("Install the DemaConsulting.DocDown.Word", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not yet available", summary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves a legacy binary .doc input now fails with the structured no-extractor-for-format
    ///     failure (DD0402) rather than the unrecognized-format failure (DD0401), naming the format
    ///     and stating plainly that DocDown does not support legacy binary formats.
    /// </summary>
    /// <remarks>
    ///     This is the end-to-end proof of Increment 0: once <c>.doc</c> is detectable by extension,
    ///     the engine reaches selection and reports a format-specific failure instead of an
    ///     unrecognized-format one. The DD0401 exclusion is asserted explicitly because that shift
    ///     is the entire point of making the legacy binaries detectable. The remedy promises
    ///     nothing, because no DocDown package extracts a legacy binary.
    /// </remarks>
    [Fact]
    public async Task DocDownCore_ExtractAsync_DocWithoutWordBackend_FailsNoExtractorNamingLegacyFormat()
    {
        // Arrange: only a text backend is registered, and the input is named as a legacy Word document
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("report.doc", "arbitrary bytes that are not a real document");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction, which must recognize doc by extension and then find no backend
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the failure is the no-extractor kind with the documented code, NOT unrecognized
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Equal("DD0402", result.Failure.Code);
        Assert.NotEqual("DD0401", result.Failure.Code);

        // Assert: the remedy names the format and declares the format unsupported, promising nothing
        Assert.Contains("doc", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Remedy,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DemaConsulting.DocDown.Word", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft Office", result.Failure.Remedy, StringComparison.Ordinal);

        // Assert: the manifest records the legacy format and that the extension is what identified it
        var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var source = manifest.RootElement.GetProperty("source");
        Assert.Equal("doc", source.GetProperty("format").GetString());
        Assert.Equal("extension", source.GetProperty("detectionBasis").GetString());

        // Assert: a failed run still writes the full layout and still verifies clean
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that an unavailable backend's reason appears verbatim in the summary when it is the only option.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary()
    {
        // Arrange: the single text backend is unavailable with a distinctive reason
        using var temp = new TempScratch();
        const string reason = "the native text renderer 'libtxt 4.2' is not installed on this host";
        var engine = BuildEngine(StubExtractor.Unavailable("text", [DocumentFormat.Text], reason));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with no available backend
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run fails and the unavailability reason is present verbatim in the summary
        Assert.Equal(ExtractionFailureKind.NoAvailableExtractor, result.Failure?.Kind);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains(reason, summary, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that when the higher-fidelity backend is unavailable the run falls back and records the exclusion.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_HigherFidelityUnavailable_FallsBackAndRecordsExclusion()
    {
        // Arrange: a preferred high-priority backend is unavailable and a lower-priority one is available
        using var temp = new TempScratch();
        const string reason = "the high-fidelity renderer requires a GPU that is not present";
        var high = new StubExtractor
        {
            Id = "high",
            DisplayName = "high (stub)",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text,
            Priority = 100,
            AvailabilityMode = StubAvailabilityMode.Unavailable,
            UnavailableReason = reason
        };
        var low = StubExtractor.Available("low", [DocumentFormat.Text], ExtractorCapabilities.Text);
        var engine = BuildEngine(high, low);
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction so selection must fall back to the available backend
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the available backend ran and the trace records the higher-fidelity exclusion and its reason
        Assert.Equal("low", result.SelectedExtractor?.Id);
        var exclusion = Assert.Single(result.SelectionTrace, verdict => verdict.ExtractorId == "high");
        Assert.Equal(CandidateOutcome.Unavailable, exclusion.Outcome);
        Assert.Contains(reason, exclusion.Detail, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that requesting page rendering an available backend cannot provide degrades with an explained gap.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_PagesRequestedButUnsupported_DegradesWithGap()
    {
        // Arrange: the available backend offers text only, but the caller requests rendered pages
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var options = FixedOptions();
        options.RenderPages = true;
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with an unsatisfiable page-render request
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the run degrades, the pages folder is absent, and a pages gap explains why
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Gaps, gap => gap.Kind == GapKind.Pages);
        Assert.False(Directory.Exists(Path.Combine(scratch, "pages")));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that an unsatisfiable required capability fails hard rather than silently degrading.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_RequiredCapabilitiesUnavailable_FailsWithoutDegrading()
    {
        // Arrange: the caller demands rendered pages as a hard requirement the text backend cannot meet
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var options = FixedOptions();
        options.RequireCapabilities = ExtractorCapabilities.RenderedPages;
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with an unsatisfiable hard capability requirement
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the run fails outright and does not fall back to a degraded partial extraction
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal(ExtractionFailureKind.RequiredCapabilitiesUnavailable, result.Failure?.Kind);
        Assert.Null(result.SelectedExtractor);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that a caller override naming an unavailable backend fails and never falls back.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_OverrideUnavailable_FailsAndNeverFallsBack()
    {
        // Arrange: the overridden backend is unavailable while a second backend is available
        using var temp = new TempScratch();
        var primary = StubExtractor.Unavailable("primary", [DocumentFormat.Text], "the primary backend is offline");
        var secondary = StubExtractor.Available("secondary", [DocumentFormat.Text], ExtractorCapabilities.Text);
        var engine = BuildEngine(primary, secondary);
        var options = FixedOptions();
        options.PreferredExtractorId = "primary";
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction forcing the unavailable backend
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the run fails on the override, stays in override mode, and never uses the available backend
        Assert.Equal(ExtractionFailureKind.RequestedExtractorUnavailable, result.Failure?.Kind);
        Assert.Equal(SelectionMode.CallerOverride, result.SelectionMode);
        Assert.Null(result.SelectedExtractor);
        Assert.False(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that a backend that stays silent about an absence still yields an explained manifest.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_UnexplainedAbsence_CoreSynthesizesGap()
    {
        // Arrange: a silent backend claims images were found but writes and explains none
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Silent("silent", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction so Core must synthesize the missing explanation
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: Core synthesized the unexplained-absence diagnostic and the manifest remains honest
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0701");
        Assert.Contains(result.Gaps, gap => gap.Target == "images/");
        Assert.False(result.IsComplete);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that an unrecognized format fails with the format-not-recognized diagnostic.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_UnknownFormat_FailsWithDiagnostic()
    {
        // Arrange: an input whose extension and content match no known format
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("mystery.xyz", "no recognizable signature here");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction against an unrecognized document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run fails with the format-not-recognized failure and its fixed code
        Assert.Equal(ExtractionFailureKind.FormatNotRecognized, result.Failure?.Kind);
        Assert.Equal("DD0401", result.Failure?.Code);
        Assert.True(result.DetectedFormat.Format.IsUnknown);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that a throwing backend still writes the summary and manifest with an explanation.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_Failure_StillWritesSummaryAndManifest()
    {
        // Arrange: a backend that throws partway through extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Failing("failing", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction so the backend faults
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run fails but the layout and a displayable explanation are still written
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal(ExtractionFailureKind.ExtractorFailed, result.Failure?.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Failure?.Explanation));
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the honesty invariant: across many outcomes the reported gaps always match the filesystem.
    /// </summary>
    /// <param name="scenario">The scenario key selecting the backend and options to run.</param>
    [Theory]
    [InlineData("success")]
    [InlineData("degraded-pages")]
    [InlineData("silent-absence")]
    [InlineData("failure")]
    [InlineData("no-backend")]
    public async Task DocDownCore_Extract_AnyOutcome_ReportedGapsMatchFilesystem(string scenario)
    {
        // Arrange: build the engine, options, and input document for the requested scenario
        using var temp = new TempScratch();
        var (engine, options, fileName) = BuildScenario(scenario);
        var input = temp.CreateFile(fileName, "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction for this outcome
        await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: whatever the outcome, the contract verifier finds no discrepancy with disk
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the summary names the absolute scratch path — the single most important product line.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_Summary_ContainsAbsoluteScratchPath()
    {
        // Arrange: a successful text extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction and read the produced summary
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the summary contains the absolute scratch folder the result reports
        Assert.Contains(result.ScratchFolder, summary, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(result.ScratchFolder));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the summary names the selected backend so provenance is recorded.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_Summary_NamesSelectedBackend()
    {
        // Arrange: a successful extraction by a distinctively named backend
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction and read the produced summary
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the summary names the backend the result says ran
        Assert.Contains(result.SelectedExtractor!.Id, summary, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the manifest round-trips as JSON with its mandatory fields populated.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_Manifest_RoundTrips()
    {
        // Arrange: a successful text extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction and parse the produced manifest
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var root = document.RootElement;

        // Assert: the manifest parses and its mandatory identity fields are populated
        Assert.Equal("1.2", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("DocDown", root.GetProperty("tool").GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("scratchFolder").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("extractedAtUtc").GetString()));
        Assert.True(root.TryGetProperty("artifacts", out _));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that two runs into the same folder with a fixed timestamp are byte-identical (golden-file determinism).
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_RepeatedRun_IsByteIdentical()
    {
        // Arrange: a deterministic engine, a fixed timestamp, and a stable scratch folder
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run twice into the same scratch folder, snapshotting the first run's files
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);
        var firstSummary = Path.Combine(temp.Path, "first-summary.txt");
        var firstManifest = Path.Combine(temp.Path, "first-manifest.json");
        File.Copy(Path.Combine(scratch, "summary.txt"), firstSummary);
        File.Copy(Path.Combine(scratch, "manifest.json"), firstManifest);
        await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: both the summary and the manifest are byte-identical between runs
        ContractAssert.FileEquals(firstSummary, Path.Combine(scratch, "summary.txt"));
        ContractAssert.FileEquals(firstManifest, Path.Combine(scratch, "manifest.json"));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that changing only the timestamp changes only the timestamp line (determinism scope).
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_RepeatedRunWithSystemClock_DiffersOnlyInTimestamp()
    {
        // Arrange: two runs whose only difference is the clock reading stamped into the output
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");
        var earlier = new ExtractionOptions { TimestampUtc = FixedTimestamp };
        var later = new ExtractionOptions { TimestampUtc = FixedTimestamp.AddHours(1) };

        // Act: run twice with different clock readings, capturing each summary
        await engine.ExtractAsync(input, scratch, earlier, Ct);
        var firstSummary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        await engine.ExtractAsync(input, scratch, later, Ct);
        var secondSummary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the summaries differ, but only on the extracted-timestamp line
        Assert.NotEqual(firstSummary, secondSummary);
        Assert.Equal(StripTimestampLine(firstSummary), StripTimestampLine(secondSummary));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves that mutating the options after a call does not affect that call's result (clone-on-entry).
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult()
    {
        // Arrange: one options instance and two scratch folders for two independent runs
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var firstScratch = Path.Combine(temp.Path, "out1");
        var secondScratch = Path.Combine(temp.Path, "out2");
        var options = FixedOptions();
        options.IncludeEmbeddedImages = true;

        // Act: run once, then mutate the same options object and run again into a second folder
        await engine.ExtractAsync(input, firstScratch, options, Ct);
        options.IncludeEmbeddedImages = false;
        await engine.ExtractAsync(input, secondScratch, options, Ct);

        // Assert: the first run kept the snapshot it entered with while the second reflects the mutation
        Assert.True(ReadIncludeEmbeddedImages(firstScratch));
        Assert.False(ReadIncludeEmbeddedImages(secondScratch));
        ContractAssert.NoViolations(firstScratch);
        ContractAssert.NoViolations(secondScratch);
    }

    /// <summary>
    ///     Proves that the backend-status introspection reports availability and the unavailability reason.
    /// </summary>
    [Fact]
    public void DocDownCore_GetBackendStatus_ReportsAvailabilityAndReason()
    {
        // Arrange: an engine with one available and one unavailable backend
        const string reason = "the pdf backend needs a native library that is missing";
        var available = StubExtractor.Available("available", [DocumentFormat.Text], ExtractorCapabilities.Text);
        var unavailable = StubExtractor.Unavailable("unavailable", [DocumentFormat.Pdf], reason);
        var engine = BuildEngine(available, unavailable);

        // Act: query the flattened backend status
        var statuses = engine.GetBackendStatus();

        // Assert: each backend's availability and reason are reported accurately
        var availableStatus = Assert.Single(statuses, status => status.Id == "available");
        Assert.True(availableStatus.IsAvailable);
        Assert.Null(availableStatus.UnavailableReason);
        var unavailableStatus = Assert.Single(statuses, status => status.Id == "unavailable");
        Assert.False(unavailableStatus.IsAvailable);
        Assert.Equal(reason, unavailableStatus.UnavailableReason);
    }

    /// <summary>
    ///     Proves that the self-test seam includes Core's own cases and keeps skipped distinct from failed.
    /// </summary>
    [Fact]
    public void DocDownCore_GetSelfTestCases_IncludesCoreCases()
    {
        // Arrange: an unavailable self-validating backend whose case would fail if it ran
        using var temp = new TempScratch();
        var backend = StubExtractor.Unavailable("backend", [DocumentFormat.Text], "backend is unavailable here");
        backend.SelfTestCases.Add(new SelfTestCase(
            "backend.case", "backend",
            _ => SelfTestResult.Failed("this case must not run when the backend is unavailable", TimeSpan.Zero)));
        var engine = BuildEngine(backend);
        var context = new SelfTestContext(temp.Path, CancellationToken.None);

        // Act: enumerate the cases and run one core case and the unavailable backend's case
        var cases = engine.GetSelfTestCases();
        var coreResult = cases.First(testCase => testCase.Category == "core").Run(context);
        var backendResult = cases.First(testCase => testCase.Category == "backend").Run(context);

        // Assert: three core cases exist, a core case passes, and the backend case is skipped, not failed
        Assert.Equal(3, cases.Count(testCase => testCase.Category == "core"));
        Assert.Equal(SelfTestStatus.Passed, coreResult.Status);
        Assert.Equal(SelfTestStatus.Skipped, backendResult.Status);
        Assert.NotEqual(SelfTestStatus.Failed, backendResult.Status);
    }

    /// <summary>
    ///     Proves the manifest states, per image, how that image was produced.
    /// </summary>
    [Fact]
    public async Task DocDownCore_Extract_ImageProvenance_ManifestRecordsTransformPerImage()
    {
        // Arrange: a backend scripted to add one verbatim image and one it re-encoded as PNG
        using var temp = new TempScratch();
        var extractor = new StubExtractor
        {
            Id = "text",
            DisplayName = "text (stub)",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages,
            ExtractBehavior = WriteImagesOfDifferingProvenanceAsync
        };
        var engine = BuildEngine(extractor);
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction end to end through the engine
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the run succeeds and the manifest carries a per-image provenance claim, not a
        // single blanket label, so a decode-and-re-encode is never reported as a passthrough
        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal("passthrough", images[0].GetProperty("transform").GetString());
        Assert.Equal("image/jpeg", images[0].GetProperty("mediaType").GetString());
        Assert.Equal("decodedToPng", images[1].GetProperty("transform").GetString());
        Assert.Equal("image/png", images[1].GetProperty("mediaType").GetString());
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Writes text plus one passthrough image and one decoded-to-PNG image through the sink.
    /// </summary>
    /// <param name="source">The source document (unused).</param>
    /// <param name="context">The extraction context to write through.</param>
    /// <returns>An outcome of <see cref="ExtractionOutcome.Succeeded"/>.</returns>
    /// <remarks>
    ///     Scripts the two provenance cases a real extractor faces — bytes already in a usable
    ///     encoding, and bytes that had to be decoded and re-encoded — so the system-level test can
    ///     prove both reach the manifest with distinct, truthful labels.
    /// </remarks>
    private static async ValueTask<ExtractionOutcome> WriteImagesOfDifferingProvenanceAsync(
        DocumentSource source, IExtractionContext context)
    {
        var token = context.CancellationToken;
        await context.Sink.WriteContentAsync("# Document\n\nTwo images.\n", token);

        // The stored bytes were already a complete image file and were written unchanged
        using var verbatim = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x01]);
        await context.Sink.AddImageAsync(
            verbatim, new ImageHint("photo", "image/jpeg", Transform: ImageTransform.Passthrough), token);

        // The stored samples had to be decoded and re-encoded before they could be written
        using var reEncoded = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x02]);
        await context.Sink.AddImageAsync(
            reEncoded, new ImageHint("chart", "image/png", Transform: ImageTransform.DecodedToPng), token);

        context.Sink.ReportFound(GapKind.Images, 2);
        return ExtractionOutcome.Succeeded;
    }

    /// <summary>
    ///     Builds an engine registering the supplied extractors in order.
    /// </summary>
    /// <param name="extractors">The extractors to register.</param>
    /// <returns>A configured engine.</returns>
    /// <remarks>Shared setup so each test states only the backends its scenario needs.</remarks>
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
    ///     Creates options carrying the fixed timestamp for reproducible output.
    /// </summary>
    /// <returns>An options instance with a fixed timestamp.</returns>
    /// <remarks>Keeps output byte-stable so determinism assertions are meaningful.</remarks>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Builds the engine, options, and input file name for a named honesty scenario.
    /// </summary>
    /// <param name="scenario">The scenario key.</param>
    /// <returns>The configured engine, options, and input file name.</returns>
    /// <exception cref="ArgumentException">Thrown when the scenario key is unknown.</exception>
    /// <remarks>Centralizes the outcome matrix so the honesty theory stays declarative.</remarks>
    private static (DocDownEngine Engine, ExtractionOptions Options, string FileName) BuildScenario(string scenario)
    {
        var options = FixedOptions();
        switch (scenario)
        {
            case "success":
                return (BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text)),
                    options, "document.txt");
            case "degraded-pages":
                options.RenderPages = true;
                return (BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text)),
                    options, "document.txt");
            case "silent-absence":
                return (BuildEngine(StubExtractor.Silent("silent", [DocumentFormat.Text])), options, "document.txt");
            case "failure":
                return (BuildEngine(StubExtractor.Failing("failing", [DocumentFormat.Text])), options, "document.txt");
            case "no-backend":
                return (BuildEngine(StubExtractor.Available("docx", [DocumentFormat.Docx], ExtractorCapabilities.Text)),
                    options, "document.txt");
            default:
                throw new ArgumentException($"unknown scenario '{scenario}'", nameof(scenario));
        }
    }

    /// <summary>
    ///     Removes the extracted-timestamp line from a summary so clock-dependent output can be compared.
    /// </summary>
    /// <param name="summary">The summary text.</param>
    /// <returns>The summary with the timestamp line removed.</returns>
    /// <remarks>Isolates the one line that legitimately varies with the wall clock.</remarks>
    private static string StripTimestampLine(string summary)
    {
        var lines = summary.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("Extracted (UTC)", StringComparison.Ordinal));
        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Reads the recorded <c>includeEmbeddedImages</c> requested option from a manifest.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder to read.</param>
    /// <returns>The recorded option value.</returns>
    /// <remarks>Used to prove each run snapshotted its own options.</remarks>
    private static bool ReadIncludeEmbeddedImages(string scratchFolder)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(scratchFolder, "manifest.json")));
        return document.RootElement.GetProperty("requestedOptions").GetProperty("includeEmbeddedImages").GetBoolean();
    }
}
