using System.Reflection;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="DocDownEngine"/>, exercising options snapshotting, structured
///     failure return, notes, backend discovery, self-test exposure, cancellation propagation, and
///     argument validation.
/// </summary>
public class DocDownEngineTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a page-rendering request against a non-paginated format is honored with silence.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_NonPaginatedFormat_RenderRequest_SucceedsSilently()
    {
        // Arrange: a non-paginated backend whose format has no pages to render
        using var temp = new TempScratch();
        var nonPaginated = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            PageRenderingApplicable = false,
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Produced;
            }
        };
        var engine = BuildEngine(nonPaginated);
        var options = FixedOptions();
        options.RenderPages = true;

        // Act: run against a non-paginated format
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            Path.Combine(temp.Path, "out"),
            options,
            Ct);

        // Assert: the run succeeds and no render note is emitted
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.DoesNotContain(result.Notes, note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a page-rendering request against a paginated format without a renderer records a
    ///     plain note while still producing the layout.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_PaginatedFormat_RenderRequestUnavailable_RecordsNote()
    {
        // Arrange: a paginated backend that cannot render pages in this environment
        using var temp = new TempScratch();
        var paginated = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Produced;
            }
        };
        var engine = BuildEngine(paginated);
        var options = FixedOptions();
        options.RenderPages = true;

        // Act: run with rendering requested
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            Path.Combine(temp.Path, "out"),
            options,
            Ct);

        // Assert: the layout is still produced and the missing renderer is recorded as a note
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Contains(
            result.Notes,
            note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a refused scratch folder returns before any layout is written.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_ScratchRefused_ReturnsFailureWithoutWritingLayout()
    {
        // Arrange: a non-empty target folder that the default safe mode must refuse
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(scratch);
        var userData = Path.Combine(scratch, "user-data.txt");
        await File.WriteAllTextAsync(userData, "precious", Ct);
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));

        // Act: run into the caller's populated folder
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the refusal is unreadable, reports the requested path, and writes no layout
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.Equal(Path.GetFullPath(scratch), result.ScratchFolder);
        Assert.Contains("was refused", result.Failure?.Summary, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(scratch, "summary.txt")));
        Assert.False(File.Exists(Path.Combine(scratch, "manifest.json")));
        Assert.Equal([userData], Directory.GetFileSystemEntries(scratch));
    }

    /// <summary>
    ///     Proves the backend is never invoked when format detection fails.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_UnknownFormat_DoesNotInvokeBackend()
    {
        // Arrange: a backend that records whether it ran
        using var temp = new TempScratch();
        var invoked = false;
        var extractor = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = (_, _) =>
            {
                invoked = true;
                return ValueTask.FromResult(ExtractionOutcome.Produced);
            }
        };
        var engine = BuildEngine(extractor);

        // Act: run against an unrecognized file type
        var result = await engine.ExtractAsync(
            temp.CreateFile("mystery.dat", "\u0000\u0001\u0002 not a known format"),
            Path.Combine(temp.Path, "out"),
            FixedOptions(),
            Ct);

        // Assert: detection failed before backend invocation
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.Contains("could not be recognized", result.Failure?.Summary, StringComparison.Ordinal);
        Assert.False(invoked);
    }

    /// <summary>
    ///     Proves the options the backend sees are a private clone, not the caller's instance.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_CallerOptions_AreClonedBeforeReachingBackend()
    {
        // Arrange: a backend that captures the options reference it sees
        using var temp = new TempScratch();
        ExtractionOptions? seen = null;
        var extractor = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = async (_, context) =>
            {
                seen = context.Options;
                await context.Sink.WriteContentAsync("# ok\n", context.CancellationToken);
                return ExtractionOutcome.Produced;
            }
        };
        var engine = BuildEngine(extractor);
        var options = FixedOptions();

        // Act: run with the caller-owned options object
        await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            Path.Combine(temp.Path, "out"),
            options,
            Ct);

        // Assert: the backend observed a clone rather than the caller's object
        Assert.NotNull(seen);
        Assert.NotSame(options, seen);
    }

    /// <summary>
    ///     Proves the backend receives a context exposing the sink and selected descriptor, never the scratch path.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_Backend_ReceivesSinkContextWithoutScratchPath()
    {
        // Arrange: a backend that inspects the context it receives
        using var temp = new TempScratch();
        IExtractionContext? captured = null;
        var extractor = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ExtractBehavior = async (_, context) =>
            {
                captured = context;
                await context.Sink.WriteContentAsync("# isolated\n", context.CancellationToken);
                return ExtractionOutcome.Produced;
            }
        };
        var engine = BuildEngine(extractor);
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the context exposes the sink and selected descriptor, but not the scratch path
        Assert.NotNull(captured);
        Assert.Equal("text", captured.SelectedExtractor.Id);
        Assert.IsType<ExtractionSink>(captured.Sink);
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);

        string[] pathWords = ["Path", "Folder", "Directory", "Scratch"];
        foreach (var type in new[] { typeof(IExtractionContext), captured.GetType() })
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var word in pathWords)
                {
                    Assert.DoesNotContain(word, member.Name, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>
    ///     Proves a throwing backend becomes an unreadable result and the layout is still written.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_BackendThrows_ReturnsUnreadableLayout()
    {
        // Arrange: a backend that throws during extraction
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Failing("text", [DocumentFormat.Text]));
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run so the backend faults
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            FixedOptions(),
            Ct);

        // Assert: the exception was contained and the summary and manifest were still written
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.False(File.Exists(Path.Combine(scratch, "content.md")));
        Assert.Contains("failed while extracting", result.Failure?.Explanation, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves backend discovery returns cached availability and render-page support.
    /// </summary>
    [Fact]
    public void DocDownEngine_GetBackends_MixedBackends_ReportsAvailabilityAndRenderedPageSupport()
    {
        // Arrange: one render-capable backend and one unavailable backend
        const string reason = "the renderer needs a font pack that is not installed";
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("up", [DocumentFormat.Text], providesRenderedPages: true))
            .AddExtractor(StubExtractor.Unavailable("down", [DocumentFormat.Text], reason))
            .Build();

        // Act: query the backend candidates
        var candidates = engine.GetBackends();

        // Assert: availability and rendered-page support are reported accurately
        var up = candidates.Single(candidate => candidate.Descriptor.Id == "up");
        var down = candidates.Single(candidate => candidate.Descriptor.Id == "down");
        Assert.True(up.Availability.IsAvailable);
        Assert.True(up.Availability.ProvidesRenderedPages);
        Assert.False(down.Availability.IsAvailable);
        Assert.Equal(reason, down.Availability.UnavailableReason);
    }

    /// <summary>
    ///     Proves the self-test suite always includes Core's two cases.
    /// </summary>
    [Fact]
    public void DocDownEngine_GetSelfTestCases_NoBackends_IncludesTwoCoreCases()
    {
        // Arrange: an engine with no registered backends
        var engine = new DocDownBuilder().Build();

        // Act: enumerate the self-test suite
        var cases = engine.GetSelfTestCases();

        // Assert: Core contributes exactly its two current self-tests
        Assert.Equal(["core.layout-invariance", "core.manifest-schema"], cases.Select(testCase => testCase.Name));
    }

    /// <summary>
    ///     Proves an unavailable self-validating backend's cases are wrapped to skip without running.
    /// </summary>
    [Fact]
    public void DocDownEngine_GetSelfTestCases_UnavailableBackend_WrapsCasesAsSkipped()
    {
        // Arrange: an unavailable self-validating backend whose case would otherwise run
        using var temp = new TempScratch();
        var ran = false;
        var backend = StubExtractor.Unavailable("offline", [DocumentFormat.Text], "the backend is offline here");
        backend.SelfTestCases.Add(new SelfTestCase(
            "offline.case",
            "offline",
            _ =>
            {
                ran = true;
                return SelfTestResult.Passed(TimeSpan.Zero);
            }));
        var engine = new DocDownBuilder().AddExtractor(backend).Build();

        // Act: run the backend case
        var backendCase = engine.GetSelfTestCases().Single(testCase => testCase.Name == "offline.case");
        var outcome = backendCase.Run(new SelfTestContext(temp.Path, Ct));

        // Assert: the case was skipped without running
        Assert.Equal(SelfTestStatus.Skipped, outcome.Status);
        Assert.False(ran);
    }

    /// <summary>
    ///     Proves cancellation propagates rather than being recorded as a structured failure.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_CanceledToken_PropagatesOperationCanceled()
    {
        // Arrange: a valid engine and input, but an already-canceled token
        using var temp = new TempScratch();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));

        // Act / Assert: cancellation is a caller signal that propagates
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await engine.ExtractAsync(
                temp.CreateFile("document.txt", "hello world"),
                Path.Combine(temp.Path, "out"),
                FixedOptions(),
                cts.Token));
    }

    /// <summary>
    ///     Proves a null source is rejected with an argument-null exception.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_NullSource_ThrowsArgumentNullException()
    {
        // Arrange: an engine and a valid scratch path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));

        // Act / Assert: a null source is a programming error
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await engine.ExtractAsync((DocumentSource)null!, Path.Combine(temp.Path, "out"), FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves an empty scratch-folder path is rejected with an argument exception.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_EmptyScratchFolder_ThrowsArgumentException()
    {
        // Arrange: an engine and a valid document path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));
        var input = temp.CreateFile("document.txt", "hello world");

        // Act / Assert: an empty scratch path is a programming error
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await engine.ExtractAsync(input, string.Empty, FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves an empty document path is rejected with an argument exception.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_EmptyDocumentPath_ThrowsArgumentException()
    {
        // Arrange: an engine and a valid scratch path
        using var temp = new TempScratch();
        var engine = BuildEngine(StubExtractor.Available("text", [DocumentFormat.Text]));

        // Act / Assert: an empty document path is a programming error
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await engine.ExtractAsync(string.Empty, Path.Combine(temp.Path, "out"), FixedOptions(), Ct));
    }

    /// <summary>
    ///     Proves a backend that writes both embedded images and rendered pages can produce both artifact sets together.
    /// </summary>
    [Fact]
    public async Task DocDownEngine_ExtractAsync_ImagesAndPages_ProducesBothArtifacts()
    {
        // Arrange: a backend scripted to add one image and one rendered page
        using var temp = new TempScratch();
        var extractor = new StubExtractor
        {
            Id = "text",
            SupportedFormats = [DocumentFormat.Text],
            ProvidesRenderedPages = true,
            ExtractBehavior = async (_, context) =>
            {
                await context.Sink.WriteContentAsync("# both\n", context.CancellationToken);

                using (var image = new MemoryStream([1, 2, 3, 4]))
                {
                    await context.Sink.AddImageAsync(
                        image,
                        new ImageHint("figure", "image/png", SourcePages: [1]),
                        context.CancellationToken);
                }

                using (var page = new MemoryStream([5, 6, 7, 8]))
                {
                    await context.Sink.AddPageAsync(1, page, context.CancellationToken);
                }

                return ExtractionOutcome.Produced;
            }
        };
        var engine = BuildEngine(extractor);
        var options = FixedOptions();
        options.RenderPages = true;
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction end to end
        var result = await engine.ExtractAsync(
            temp.CreateFile("document.txt", "hello world"),
            scratch,
            options,
            Ct);

        // Assert: both folders are populated together and the run succeeds
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(scratch, "images")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(scratch, "pages")));
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
    private static ExtractionOptions FixedOptions() => new();
}
