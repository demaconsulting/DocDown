using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Subsystem-integration tests for the Extraction subsystem, exercising
///     <see cref="DocDownBuilder"/>, <see cref="ExtractorRegistry"/>,
///     <see cref="ExtractorSelector"/>, and <see cref="DocDownEngine"/> together at the subsystem
///     boundary.
/// </summary>
public class ExtractionTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves explicitly registered backends appear on the built engine.
    /// </summary>
    [Fact]
    public void Extraction_ExplicitRegistration_RegisteredBackends_AppearInEngine()
    {
        // Arrange: a builder with two explicit backends
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("alpha", [DocumentFormat.Text]))
            .AddExtractor(StubExtractor.Available("beta", [DocumentFormat.Pdf]));

        // Act: build the engine and read its descriptors
        var engine = builder.Build();

        // Assert: both registered backends are present, and only those
        Assert.Equal(2, engine.Extractors.Count);
        Assert.Contains(engine.Extractors, descriptor => descriptor.Id == "alpha");
        Assert.Contains(engine.Extractors, descriptor => descriptor.Id == "beta");
    }

    /// <summary>
    ///     Proves a throwing availability probe is contained and treated as unavailable.
    /// </summary>
    [Fact]
    public void Extraction_AvailabilityProbing_ThrowingProbe_TreatedAsUnavailable()
    {
        // Arrange: an engine with a backend whose availability probe throws
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.ThrowingProbe("throwing", [DocumentFormat.Text]))
            .Build();

        // Act: query backend availability
        var candidate = Assert.Single(engine.GetBackends());

        // Assert: the throwing probe is contained and reported unavailable
        Assert.False(candidate.Availability.IsAvailable);
        Assert.False(string.IsNullOrEmpty(candidate.Availability.UnavailableReason));
    }

    /// <summary>
    ///     Proves selector calls remain deterministic for equal inputs.
    /// </summary>
    [Fact]
    public void Extraction_DeterministicSelection_EqualInputs_ProduceEqualSelection()
    {
        // Arrange: a detection, options, and two available candidates
        var detection = TextDetection();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false };
        var candidates = new[]
        {
            Candidate("one", priority: 1),
            Candidate("two", priority: 2)
        };

        // Act: run selection twice with the same inputs
        var first = ExtractorSelector.Select(detection, options, candidates, out var firstFailure);
        var second = ExtractorSelector.Select(detection, options, candidates, out var secondFailure);

        // Assert: the two selections are identical
        Assert.Null(firstFailure);
        Assert.Null(secondFailure);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Proves the selector prefers a renderer when page rendering was requested.
    /// </summary>
    [Fact]
    public void Extraction_RenderPreference_RenderRequested_PrefersRenderer()
    {
        // Arrange: rendered pages are requested and only one candidate can render them
        var options = new ExtractionOptions { IncludeEmbeddedImages = false, RenderPages = true };
        var candidates = new[]
        {
            Candidate("text-only", priority: 100),
            Candidate("renderer", priority: 1, providesRenderedPages: true)
        };

        // Act: select for a text document
        var selected = ExtractorSelector.Select(TextDetection(), options, candidates, out var failure);

        // Assert: render capability is preferred over the higher ordinary priority
        Assert.Null(failure);
        Assert.Equal("renderer", selected?.Id);
    }

    /// <summary>
    ///     Proves the engine orchestrates the full pipeline end to end for a text document.
    /// </summary>
    [Fact]
    public async Task Extraction_Orchestration_TextDocument_RunsPipelineEndToEnd()
    {
        // Arrange: an engine with a text backend and a text input
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("text", [DocumentFormat.Text]))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the full pipeline
        var result = await engine.ExtractAsync(input, scratch, new ExtractionOptions(), Ct);

        // Assert: the layout was produced, the backend was selected, and content was written
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("text", result.SelectedExtractor?.Id);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a structured failure is returned when no backend is available for the detected format.
    /// </summary>
    [Fact]
    public async Task Extraction_StructuredFailure_NoAvailableBackend_ReturnsUnreadableFailure()
    {
        // Arrange: an engine whose only text backend is unavailable
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Unavailable("text", [DocumentFormat.Text], "the text backend is offline"))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");

        // Act: run the extraction with no available backend
        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), new ExtractionOptions(), Ct);

        // Assert: the failure is unreadable and carries prose
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains("No available extractor can process the detected format", result.Failure.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves builder mutation after Build does not affect the already-built engine defaults.
    /// </summary>
    [Fact]
    public async Task Extraction_OptionIsolation_BuilderMutatedAfterBuild_DoesNotAffectEngine()
    {
        // Arrange: build an engine whose defaults do not render pages, then mutate the builder afterwards
        using var temp = new TempScratch();
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("text", [DocumentFormat.Text]))
            .ConfigureDefaults(options => options.RenderPages = false);
        var engine = builder.Build();
        builder.ConfigureDefaults(options => options.RenderPages = true);
        var input = temp.CreateFile("document.txt", "hello world");

        // Act: run the engine using its own captured defaults
        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), null, Ct);

        // Assert: the later builder mutation did not leak into the already-built engine
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.DoesNotContain(result.Notes, note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
        ContractAssert.LayoutPresent(result.ScratchFolder);
    }

    /// <summary>
    ///     Proves a throwing backend becomes a structured unreadable failure rather than propagating.
    /// </summary>
    [Fact]
    public async Task Extraction_ExtractorIsolation_BackendThrows_BecomesUnreadableFailure()
    {
        // Arrange: an engine with a backend that throws during extraction
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Failing("failing", [DocumentFormat.Text]))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");

        // Act: run the extraction so the backend faults
        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), new ExtractionOptions(), Ct);

        // Assert: the exception was contained and converted into an unreadable failure
        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        Assert.Contains("The selected extractor 'failing' failed while extracting.", result.Failure?.Explanation, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(result.ScratchFolder);
    }

    /// <summary>
    ///     Proves the engine exposes Core's own self-test cases together with a self-validating backend's cases.
    /// </summary>
    [Fact]
    public void Extraction_SelfValidationSeam_Engine_ExposesCoreAndBackendCases()
    {
        // Arrange: an available self-validating backend contributing one case
        var backend = StubExtractor.Available("backend", [DocumentFormat.Text]);
        backend.SelfTestCases.Add(new SelfTestCase(
            "backend.case",
            "backend",
            _ => SelfTestResult.Passed(TimeSpan.Zero)));
        var engine = new DocDownBuilder().AddExtractor(backend).Build();

        // Act: enumerate the assembled self-test suite
        var cases = engine.GetSelfTestCases();

        // Assert: the suite includes Core's own cases and the backend's contributed case
        Assert.Equal(2, cases.Count(testCase => testCase.Category == "core"));
        Assert.Contains(cases, testCase => testCase.Category == "backend");
    }

    /// <summary>
    ///     Creates an available candidate for selector tests.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <param name="providesRenderedPages">Whether the candidate can render pages in this environment.</param>
    /// <returns>The configured candidate.</returns>
    private static ExtractorCandidate Candidate(string id, int priority, bool providesRenderedPages = false) =>
        new(
            new ExtractorDescriptor(id, id + " name", [DocumentFormat.Text], priority),
            ExtractorAvailability.Available(providesRenderedPages));

    /// <summary>
    ///     Creates a text-format detection by file extension for selector tests.
    /// </summary>
    /// <returns>A text-format detection.</returns>
    private static FormatDetection TextDetection() => new(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
}
