using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Subsystem-integration tests for the Extraction subsystem, exercising
///     <see cref="DocDownBuilder"/>, <see cref="ExtractorRegistry"/>, <see cref="ExtractorSelector"/>,
///     and <see cref="DocDownEngine"/> together at the subsystem boundary.
/// </summary>
/// <remarks>
///     These tests use Extraction units and their documented dependencies (the Output subsystem is
///     a documented downstream dependency of orchestration). Each is named for the subsystem
///     requirement it evidences: explicit registration, availability probing, deterministic and
///     explained selection, capability negotiation, orchestration, structured failure, option and
///     extractor isolation, and the self-validation seam.
/// </remarks>
public class ExtractionTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves explicitly registered backends appear on the built engine (ExplicitRegistration).
    /// </summary>
    [Fact]
    public void Extraction_ExplicitRegistration_RegisteredBackends_AppearInEngine()
    {
        // Arrange: a builder with two explicitly registered backends
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("alpha", [DocumentFormat.Text], ExtractorCapabilities.Text))
            .AddExtractor(StubExtractor.Available("beta", [DocumentFormat.Pdf], ExtractorCapabilities.Text));

        // Act: build the engine and read its registered descriptors
        var engine = builder.Build();

        // Assert: both registered backends are present, and only those
        Assert.Equal(2, engine.Extractors.Count);
        Assert.Contains(engine.Extractors, descriptor => descriptor.Id == "alpha");
        Assert.Contains(engine.Extractors, descriptor => descriptor.Id == "beta");
    }

    /// <summary>
    ///     Proves a throwing availability probe is contained and treated as unavailable (AvailabilityProbing).
    /// </summary>
    [Fact]
    public void Extraction_AvailabilityProbing_ThrowingProbe_TreatedAsUnavailable()
    {
        // Arrange: an engine with a backend whose availability probe throws
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.ThrowingProbe("throwing", [DocumentFormat.Text]))
            .Build();

        // Act: query backend status, which must probe availability defensively
        var status = Assert.Single(engine.GetBackendStatus());

        // Assert: the throwing probe is contained and the backend is reported unavailable
        Assert.False(status.IsAvailable);
        Assert.False(string.IsNullOrEmpty(status.UnavailableReason));
    }

    /// <summary>
    ///     Proves the selector is a pure function that yields equal results for equal inputs (DeterministicSelection).
    /// </summary>
    [Fact]
    public void Extraction_DeterministicSelection_EqualInputs_ProduceEqualSelection()
    {
        // Arrange: a selector, a detection, options, and two available candidates
        var selector = new ExtractorSelector();
        var detection = TextDetection();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false };
        var candidates = new[]
        {
            Candidate("one", ExtractorCapabilities.Text, priority: 1),
            Candidate("two", ExtractorCapabilities.Text, priority: 2)
        };

        // Act: run selection twice with the same inputs
        var first = selector.Select(detection, options, candidates);
        var second = selector.Select(detection, options, candidates);

        // Assert: the two selections are identical, including the ordered trace
        Assert.Equal(first.Selected, second.Selected);
        Assert.Equal(first.Trace, second.Trace);
    }

    /// <summary>
    ///     Proves the selection trace explains every candidate considered (SelectionExplained).
    /// </summary>
    [Fact]
    public void Extraction_SelectionExplained_MultipleCandidates_TraceCoversEveryCandidate()
    {
        // Arrange: a selector with two competing candidates for the same format
        var selector = new ExtractorSelector();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false };
        var candidates = new[]
        {
            Candidate("winner", ExtractorCapabilities.Text, priority: 10),
            Candidate("loser", ExtractorCapabilities.Text, priority: 1)
        };

        // Act: run selection
        var selection = selector.Select(TextDetection(), options, candidates);

        // Assert: the trace names both candidates and the higher-priority one wins
        Assert.Equal("winner", selection.Selected?.Id);
        Assert.Contains(selection.Trace, verdict => verdict.ExtractorId == "winner");
        Assert.Contains(selection.Trace, verdict => verdict.ExtractorId == "loser");
    }

    /// <summary>
    ///     Proves the selector accepts a partial satisfier when a requested capability is unavailable (CapabilityNegotiation).
    /// </summary>
    [Fact]
    public void Extraction_CapabilityNegotiation_RenderRequestedButUnavailable_SelectsPartialSatisfier()
    {
        // Arrange: rendered pages are requested but the only candidate offers text alone
        var selector = new ExtractorSelector();
        var options = new ExtractionOptions { IncludeEmbeddedImages = false, RenderPages = true };
        var candidates = new[] { Candidate("text", ExtractorCapabilities.Text, priority: 1) };

        // Act: run selection with the unsatisfiable render request
        var selection = selector.Select(TextDetection(), options, candidates);

        // Assert: the partial satisfier is selected but the render capability is unmet
        Assert.Equal("text", selection.Selected?.Id);
        Assert.True(selection.RequiredCapabilities.HasFlag(ExtractorCapabilities.RenderedPages));
        Assert.False(selection.SatisfiedCapabilities.HasFlag(ExtractorCapabilities.RenderedPages));
    }

    /// <summary>
    ///     Proves the engine orchestrates the full pipeline end to end for a text document (Orchestration).
    /// </summary>
    [Fact]
    public async Task Extraction_Orchestration_TextDocument_RunsPipelineEndToEnd()
    {
        // Arrange: an engine with a fully capable text backend and a text input
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available(
                "text", [DocumentFormat.Text], ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the full pipeline
        var result = await engine.ExtractAsync(input, scratch, new ExtractionOptions(), Ct);

        // Assert: the pipeline produced the layout, selected the backend, and did not fail
        Assert.NotEqual(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal("text", result.SelectedExtractor?.Id);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the engine returns a structured failure with a code when no backend is available (StructuredFailure).
    /// </summary>
    [Fact]
    public async Task Extraction_StructuredFailure_NoAvailableBackend_ReturnsFailureWithCode()
    {
        // Arrange: an engine whose only text backend is unavailable
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Unavailable("text", [DocumentFormat.Text], "the text backend is offline"))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction with no available backend
        var result = await engine.ExtractAsync(input, scratch, new ExtractionOptions(), Ct);

        // Assert: the failure is structured and carries the no-available-extractor code
        Assert.NotNull(result.Failure);
        Assert.Equal("DD0403", result.Failure.Code);
    }

    /// <summary>
    ///     Proves builder mutation after Build does not affect the already-built engine (OptionIsolation).
    /// </summary>
    [Fact]
    public async Task Extraction_OptionIsolation_BuilderMutatedAfterBuild_DoesNotAffectEngine()
    {
        // Arrange: build an engine whose defaults do not render pages, then mutate the builder afterwards
        using var temp = new TempScratch();
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available(
                "text", [DocumentFormat.Text], ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages))
            .ConfigureDefaults(options => options.RenderPages = false);
        var engine = builder.Build();
        builder.ConfigureDefaults(options => options.RenderPages = true);
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the engine using its own captured defaults
        var result = await engine.ExtractAsync(input, scratch, null, Ct);

        // Assert: the later builder mutation did not leak in, so no page-render gap was produced
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Pages);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a throwing backend becomes a structured failure rather than propagating (ExtractorIsolation).
    /// </summary>
    [Fact]
    public async Task Extraction_ExtractorIsolation_BackendThrows_BecomesStructuredFailure()
    {
        // Arrange: an engine with a backend that throws during extraction
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Failing("failing", [DocumentFormat.Text]))
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction so the backend faults
        var result = await engine.ExtractAsync(input, scratch, new ExtractionOptions(), Ct);

        // Assert: the exception was contained and converted into a structured failure
        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.Equal("DD0703", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves the engine exposes Core's own cases and a self-validating backend's cases (SelfValidationSeam).
    /// </summary>
    [Fact]
    public void Extraction_SelfValidationSeam_Engine_ExposesCoreAndBackendCases()
    {
        // Arrange: an engine with an available self-validating backend contributing one case
        var backend = StubExtractor.Available("backend", [DocumentFormat.Text], ExtractorCapabilities.Text);
        backend.SelfTestCases.Add(new SelfTestCase(
            "backend.case", "backend", _ => SelfTestResult.Passed(TimeSpan.Zero)));
        var engine = new DocDownBuilder().AddExtractor(backend).Build();

        // Act: enumerate the assembled self-test suite
        var cases = engine.GetSelfTestCases();

        // Assert: the suite includes Core's own cases and the backend's contributed case
        Assert.Contains(cases, testCase => testCase.Category == "core");
        Assert.Contains(cases, testCase => testCase.Category == "backend");
    }

    /// <summary>
    ///     Creates a candidate for the text format with the given capabilities, priority, and availability.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="capabilities">The declared and effective capabilities.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <param name="available">Whether the candidate is available.</param>
    /// <param name="reason">The unavailability reason when not available.</param>
    /// <returns>The configured candidate.</returns>
    /// <remarks>Keeps the selector tests declarative by building descriptors and availability in one place.</remarks>
    private static ExtractorCandidate Candidate(
        string id, ExtractorCapabilities capabilities, int priority, bool available = true, string reason = "unavailable")
    {
        var descriptor = new ExtractorDescriptor(id, id + " name", [DocumentFormat.Text], capabilities, priority);
        var availability = available
            ? ExtractorAvailability.Available(capabilities)
            : ExtractorAvailability.Unavailable(reason);
        return new ExtractorCandidate(descriptor, availability);
    }

    /// <summary>
    ///     Creates a text-format detection by extension for selector tests.
    /// </summary>
    /// <returns>A text-format detection.</returns>
    /// <remarks>Selection depends only on the detected format, so a simple extension detection suffices.</remarks>
    private static FormatDetection TextDetection() => new(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
}
