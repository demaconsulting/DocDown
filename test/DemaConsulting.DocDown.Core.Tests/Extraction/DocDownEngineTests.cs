using System.Reflection;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="DocDownEngine"/>, exercising the fixed pipeline order, options
///     snapshotting, extractor isolation, structured failure return with a full artifact layout,
///     backend-status reporting, the self-test seam, cancellation propagation, and null-argument
///     guards.
/// </summary>
/// <remarks>
///     These tests drive the engine through configurable stub backends. Each is named for the unit
///     requirement it evidences: pipeline order, options snapshot, extractor isolation, failure
///     returned, failure artifacts, backend status, self-test cases, cancellation, and null-argument
///     rejection.
/// </remarks>
public class DocDownEngineTests
{
    /// <summary>A fixed timestamp used to keep engine output deterministic across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a page-rendering request against a non-paginated format is honored with silence: the
    ///     run succeeds, no pages gap is synthesized, and an informational <c>DD0303</c> diagnostic
    ///     records that rendering did not apply. Task 2 per-format silence.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_NonPaginatedFormat_RenderRequest_SucceedsSilently()
    {
        using var temp = new TempScratch();
        var nonPaginated = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages,
            PageRenderingApplicable = false,
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Succeeded;
            }
        };
        var engine = BuildEngine(nonPaginated);
        var input = temp.CreateFile("document.txt", "hello world");
        var options = FixedOptions();
        options.RenderPages = true;

        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), options, Ct);

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Pages);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0303");
    }

    /// <summary>
    ///     Proves a page-rendering request against a paginated format the selected backend cannot
    ///     render still degrades with the <c>DD0301</c> pages gap — the per-format distinction must not
    ///     silence a genuinely paginated format. Task 2 regression guard (mirrors the PDF DD0301 case).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_PaginatedFormat_RenderRequestUnavailable_Degrades()
    {
        using var temp = new TempScratch();
        var paginated = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text,
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Succeeded;
            }
        };
        var engine = BuildEngine(paginated);
        var input = temp.CreateFile("document.txt", "hello world");
        var options = FixedOptions();
        options.RenderPages = true;

        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), options, Ct);

        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.Contains(result.Gaps, gap => gap.Kind == GapKind.Pages);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DD0301");
    }

    /// <summary>
    ///     Proves a refused scratch folder returns before any layout is written (PipelineOrder).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_ScratchRefused_ReturnsFailureWithoutWritingLayout()
    {
        // Arrange: a non-empty target folder prepared under the strict RequireEmpty policy
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(scratch);
        await File.WriteAllTextAsync(Path.Combine(scratch, "user-data.txt"), "precious", Ct);
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var options = FixedOptions();
        options.ScratchFolder = ScratchFolderMode.RequireEmpty;

        // Act: run into the non-empty folder, which must be refused before source read or detection
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the refusal is structured and the requested path is reported
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal(ExtractionFailureKind.ScratchFolderRefused, result.Failure?.Kind);
        Assert.Equal(Path.GetFullPath(scratch), result.ScratchFolder);

        // Assert: the *entire* layout is absent, not merely summary.txt — a regression that wrote the
        // manifest, content, or any resource folder into a refused folder must fail this test
        Assert.False(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.False(File.Exists(Path.Combine(scratch, "manifest.json")));
        Assert.False(File.Exists(Path.Combine(scratch, "content.md")));
        Assert.False(Directory.Exists(Path.Combine(scratch, "images")));
        Assert.False(Directory.Exists(Path.Combine(scratch, "pages")));
        Assert.False(Directory.Exists(Path.Combine(scratch, "parts")));

        // Assert: the caller's pre-existing file is untouched, with its original contents intact
        var userData = Path.Combine(scratch, "user-data.txt");
        Assert.True(File.Exists(userData));
        Assert.Equal("precious", await File.ReadAllTextAsync(userData, Ct));

        // Assert: nothing else was created either — the folder holds exactly what it held before
        Assert.Equal([userData], Directory.GetFileSystemEntries(scratch));
    }

    /// <summary>
    ///     Proves the backend is never invoked when detection fails (PipelineOrder).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_UnknownFormat_DoesNotInvokeBackend()
    {
        // Arrange: a backend that records whether it ran, and an input of unrecognized format
        using var temp = new TempScratch();
        var invoked = false;
        var recording = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text,
            ExtractBehavior = (_, _) =>
            {
                invoked = true;
                return ValueTask.FromResult(ExtractionOutcome.Succeeded);
            }
        };
        var engine = BuildEngine(recording);
        var input = temp.CreateFile("mystery.dat", "\u0000\u0001\u0002 not a known format");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run against the unrecognized document
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: detection failed before selection, so the backend was never reached
        Assert.Equal(ExtractionFailureKind.FormatNotRecognized, result.Failure?.Kind);
        Assert.False(invoked);
    }

    /// <summary>
    ///     Proves the options the backend sees are a private clone, not the caller's object (OptionsSnapshot).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_CallerOptions_AreClonedBeforeReachingBackend()
    {
        // Arrange: a backend that captures the options reference it is handed via the context
        using var temp = new TempScratch();
        ExtractionOptions? seen = null;
        var capturing = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages,
            ExtractBehavior = async (_, context) =>
            {
                seen = context.Options;
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Succeeded;
            }
        };
        var engine = BuildEngine(capturing);
        var input = temp.CreateFile("document.txt", "hello world");
        var options = FixedOptions();

        // Act: run with the caller's options object
        await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), options, Ct);

        // Assert: the backend observed a distinct clone, so later caller mutation cannot affect the run
        Assert.NotNull(seen);
        Assert.NotSame(options, seen);
    }

    /// <summary>
    ///     Proves the backend is given a context exposing the sink and its own descriptor, never a path (ExtractorIsolation).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_Backend_ReceivesSinkContextWithoutScratchPath()
    {
        // Arrange: a backend that inspects the context it is handed
        using var temp = new TempScratch();
        IExtractionContext? captured = null;
        var inspecting = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages,
            ExtractBehavior = async (_, context) =>
            {
                captured = context;
                await context.Sink.WriteContentAsync("# isolated\n", context.CancellationToken);
                return ExtractionOutcome.Succeeded;
            }
        };
        var engine = BuildEngine(inspecting);
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the context named the selected backend and exposed the sink write path, and the run succeeded
        Assert.NotNull(captured);
        Assert.Equal("text", captured.SelectedExtractor.Id);
        Assert.IsType<ExtractionSink>(captured.Sink);
        Assert.NotEqual(ExtractionOutcome.Failed, result.Outcome);

        // Assert: the invariant this test is named for — no public member of the context interface
        // or of its concrete type is path-shaped. Checked by reflection so a member added later,
        // however innocently named, fails this test rather than silently leaking the scratch path.
        string[] pathWords = ["Path", "Folder", "Directory", "Scratch"];
        foreach (var type in new[] { typeof(IExtractionContext), captured.GetType() })
        {
            var memberNames = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => member.Name);
            foreach (var name in memberNames)
            {
                Assert.DoesNotContain(pathWords, word => name.Contains(word, StringComparison.Ordinal));
            }
        }

        // Assert: and no public string-valued member holds the scratch path by any other name
        var absoluteScratch = Path.GetFullPath(scratch);
        var stringProperties = captured.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0);
        foreach (var property in stringProperties)
        {
            var value = property.GetValue(captured) as string;
            Assert.False(
                value is not null && value.Contains(absoluteScratch, StringComparison.OrdinalIgnoreCase),
                $"Context member '{property.Name}' exposed the scratch path to the backend.");
        }
    }

    /// <summary>
    ///     Proves a throwing backend is contained and returned as a structured failure (FailureReturned).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_BackendThrows_ReturnsExtractorFailedFailure()
    {
        // Arrange: a backend that throws partway through extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Failing("text", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run so the backend faults
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the exception was contained into an ExtractorFailed failure with the DD0703 code
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal(ExtractionFailureKind.ExtractorFailed, result.Failure?.Kind);
        Assert.Equal("DD0703", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves a failed extraction still writes the full, verifiable artifact layout (FailureArtifacts).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_BackendThrows_StillWritesVerifiableLayout()
    {
        // Arrange: a backend that faults, so the failure branch must still write the layout
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Failing("text", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the faulting extraction
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: summary and manifest exist, content is honestly absent, and the folder verifies clean
        Assert.True(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.True(File.Exists(Path.Combine(scratch, "manifest.json")));
        Assert.False(File.Exists(Path.Combine(scratch, "content.md")));
        Assert.NotEmpty(result.Gaps);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves backend status reports availability, effective capabilities, and the reason (BackendStatus).
    /// </summary>
    [Fact]
    public void DocDownEngine_GetBackendStatus_MixedBackends_ReportsAvailabilityAndCapabilities()
    {
        // Arrange: an available capable backend and an unavailable one with a reason
        const string reason = "the renderer needs a font pack that is not installed";
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available(
                "up", [DocumentFormat.Text], ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages))
            .AddExtractor(StubExtractor.Unavailable("down", [DocumentFormat.Text], reason))
            .Build();

        // Act: query the backend status
        var statuses = engine.GetBackendStatus();

        // Assert: each backend's availability, effective capabilities, and reason are reported
        var up = statuses.Single(status => status.Id == "up");
        var down = statuses.Single(status => status.Id == "down");
        Assert.True(up.IsAvailable);
        Assert.True(up.EffectiveCapabilities.HasFlag(ExtractorCapabilities.RenderedPages));
        Assert.False(down.IsAvailable);
        Assert.Equal(reason, down.UnavailableReason);
    }

    /// <summary>
    ///     Proves the self-test suite always includes Core's three cases (SelfTestCases).
    /// </summary>
    [Fact]
    public void DocDownEngine_GetSelfTestCases_NoBackends_IncludesThreeCoreCases()
    {
        // Arrange: an engine with no registered backends
        var engine = new DocDownBuilder().Build();

        // Act: enumerate the self-test suite
        var cases = engine.GetSelfTestCases();

        // Assert: Core contributes exactly its three own cases in the core category
        Assert.Equal(3, cases.Count(testCase => testCase.Category == "core"));
    }

    /// <summary>
    ///     Proves an unavailable self-validating backend's cases are wrapped to skip without running (SelfTestCases).
    /// </summary>
    [Fact]
    public void DocDownEngine_GetSelfTestCases_UnavailableBackend_WrapsCasesAsSkipped()
    {
        // Arrange: an unavailable self-validating backend whose case would otherwise pass
        using var temp = new TempScratch();
        var backend = StubExtractor.Unavailable("offline", [DocumentFormat.Text], "the backend is offline here");
        var ran = false;
        backend.SelfTestCases.Add(new SelfTestCase("offline.case", "offline", _ =>
        {
            ran = true;
            return SelfTestResult.Passed(TimeSpan.Zero);
        }));
        var engine = new DocDownBuilder().AddExtractor(backend).Build();

        // Act: run the backend's contributed case
        var backendCase = engine.GetSelfTestCases().Single(testCase => testCase.Name == "offline.case");
        var outcome = backendCase.Run(new SelfTestContext(temp.Path, Ct));

        // Assert: the case was skipped without running because the backend is unavailable
        Assert.Equal(SelfTestStatus.Skipped, outcome.Status);
        Assert.False(ran);
    }

    /// <summary>
    ///     Proves cancellation propagates rather than being recorded as a failure (Cancellation).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_CanceledToken_PropagatesOperationCanceled()
    {
        // Arrange: a valid engine and input, but an already-canceled token
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act + Assert: cancellation is a caller signal that propagates, not a structured failure
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await engine.ExtractAsync(input, scratch, FixedOptions(), cts.Token));
    }

    /// <summary>
    ///     Proves a null source is rejected with an argument-null exception (RejectNullArguments).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_NullSource_ThrowsArgumentNullException()
    {
        // Arrange: an engine and a valid scratch path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));

        // Act + Assert: a null source is a programming error
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await engine.ExtractAsync((DocumentSource)null!, Path.Combine(temp.Path, "out"), FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves an empty scratch folder path is rejected with an argument exception (RejectNullArguments).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_EmptyScratchFolder_ThrowsArgumentException()
    {
        // Arrange: an engine and a valid document path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var input = temp.CreateFile("document.txt", "hello world");

        // Act + Assert: an empty scratch folder path is a programming error
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await engine.ExtractAsync(input, string.Empty, FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves an empty document path is rejected with an argument exception (RejectNullArguments).
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_EmptyDocumentPath_ThrowsArgumentException()
    {
        // Arrange: an engine and a valid scratch path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text));

        // Act + Assert: an empty document path is a programming error
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await engine.ExtractAsync(string.Empty, Path.Combine(temp.Path, "out"), FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves a backend that produces both embedded images and rendered pages has BOTH artifact
    ///     folders populated and still succeeds: image extraction never displaces page rendering nor
    ///     the reverse. Pins the owner's "produce both" invariant that nothing else guards, so a
    ///     regression reintroducing the false composition/extraction trade-off fails here.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_ImagesAndPages_ProducesBothArtifacts()
    {
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");
        var bothProducing = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages
                | ExtractorCapabilities.RenderedPages,
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# both\n", context.CancellationToken);

                using (var image = new MemoryStream([1, 2, 3, 4]))
                {
                    await context.Sink.AddImageAsync(
                        image, new ImageHint("figure", "image/png", SourcePages: [1]), context.CancellationToken);
                }

                context.Sink.ReportFound(GapKind.Images, 1);

                using (var page = new MemoryStream([5, 6, 7, 8]))
                {
                    await context.Sink.AddPageAsync(1, page, context.CancellationToken);
                }

                return ExtractionOutcome.Succeeded;
            }
        };
        var engine = BuildEngine(bothProducing);
        var input = temp.CreateFile("document.txt", "hello world");
        var options = FixedOptions();
        options.RenderPages = true;

        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Both folders are populated together, and the run is a clean success
        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(scratch, "images")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(scratch, "pages")));
    }

    /// <summary>
    ///     Builds an engine from the given extractors.
    /// </summary>
    /// <param name="extractors">The extractors to register.</param>
    /// <returns>The built engine.</returns>
    /// <remarks>Keeps each test declarative by hiding the builder wiring.</remarks>
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
    /// <remarks>A fixed timestamp keeps written artifacts reproducible where a test inspects them.</remarks>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };
}
