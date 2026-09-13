using DemaConsulting.DocDown.PowerPoint.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.PowerPoint.Com;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.PowerPoint.Tests.Com;

/// <summary>
///     Unit tests for <see cref="PowerPointComExtractor"/>, exercising the whole extraction path
///     through an injected stub with no Microsoft Office.
/// </summary>
public class PowerPointComExtractorTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the descriptor: identifier, format, page-rendering applicability, and the
    ///     priority-zero tie-break that keeps the managed backend the default.
    /// </summary>
    [Fact]
    public void PowerPointComExtractor_Descriptor_MatchesContract()
    {
        IDocumentExtractor extractor = new PowerPointComExtractor();

        Assert.Equal("powerpoint-com", extractor.Id);
        Assert.Contains(CoreFormat.Pptx, extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);
        Assert.True(extractor.PageRenderingApplicable);
    }

    /// <summary>
    ///     Proves the public parameterless extractor probes without throwing and answers exactly what
    ///     the environment check answers, never instructing an installation.
    /// </summary>
    [Fact]
    public void PowerPointComExtractor_PublicCtor_ProbeDoesNotThrowAndNeverInstructsInstallation()
    {
        var extractor = new PowerPointComExtractor();

        var thrown = Record.Exception(extractor.ProbeAvailability);
        Assert.Null(thrown);

        var result = extractor.ProbeAvailability();
        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result.IsAvailable)
        {
            Assert.True(result.ProvidesRenderedPages);
        }
    }

    /// <summary>
    ///     Proves a null adapter factory makes the backend probe unavailable on every platform,
    ///     without throwing.
    /// </summary>
    [Fact]
    public void PowerPointComExtractor_NullFactory_ProbesUnavailable()
    {
        var extractor = new PowerPointComExtractor(automationFactory: null);

        var result = extractor.ProbeAvailability();

        Assert.False(result.IsAvailable);
        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the COM backend delegates content to the managed backend — writing slide text and
    ///     speaker notes — and adds one rendered page per slide, disposing the automation session.
    /// </summary>
    [Fact]
    public async Task PowerPointComExtractor_Extract_ViaStub_WritesNotesAndRendersEverySlide()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "deck.pptx", PptxFixtures.DeckWithNotes());
        var slides = new[] { StubPowerPointAutomation.Rendered(1), StubPowerPointAutomation.Rendered(2) };
        StubPowerPointAutomation? created = null;
        var extractor = new PowerPointComExtractor(() =>
        {
            created = new StubPowerPointAutomation(slides);
            return created;
        });
        var sink = new RecordingSink();

        var outcome = await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        // Delegation wrote the guaranteed content, including the speaker notes no render can supply
        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Say this out loud on slide one.", content, StringComparison.Ordinal);

        // Rendering added a page per slide and a distinct renderer environment fact
        Assert.Equal(2, sink.Pages.Count);
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "pages.renderer");
        Assert.NotNull(created);
        Assert.True(created.Disposed);
        Assert.Equal(ExtractionOutcome.Produced, outcome);
    }

    /// <summary>
    ///     Proves a slide that fails to render becomes a plain-language note while the remaining
    ///     slides are still rendered — never a silent absence.
    /// </summary>
    [Fact]
    public async Task PowerPointComExtractor_Extract_SlideRenderFails_ReportsNote()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "deck.pptx", PptxFixtures.DeckWithNotes());
        var slides = new[]
        {
            StubPowerPointAutomation.Rendered(1),
            StubPowerPointAutomation.Failed(2, "the slide contained an unsupported effect")
        };
        var extractor = new PowerPointComExtractor(() => new StubPowerPointAutomation(slides));
        var sink = new RecordingSink();

        var outcome = await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        Assert.Equal(ExtractionOutcome.Produced, outcome);
        Assert.Single(sink.Pages);
        var note = Assert.Single(sink.Notes);
        Assert.Equal(
            "Slide 2 could not be rendered (the slide contained an unsupported effect).",
            note.Message);
    }

    /// <summary>
    ///     Proves the extractor passes the caller's render DPI through to the automation seam.
    /// </summary>
    [Fact]
    public async Task PowerPointComExtractor_Extract_PassesRenderDpiToAutomation()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "deck.pptx", PptxFixtures.DeckWithNotes());
        StubPowerPointAutomation? created = null;
        var extractor = new PowerPointComExtractor(() =>
        {
            created = new StubPowerPointAutomation([StubPowerPointAutomation.Rendered(1), StubPowerPointAutomation.Rendered(2)]);
            return created;
        });
        var options = new ExtractionOptions { PageRenderDpi = 200 };

        await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(new RecordingSink(), Ct, options));

        Assert.NotNull(created);
        Assert.Equal(200, created.LastDpi);
    }

    /// <summary>
    ///     Proves the COM run does not leak the delegated managed backend's contradictory
    ///     <c>powerpoint.pageRendering : NOT available</c> environment fact — which would contradict
    ///     the authoritative <c>pages.renderer : available</c> fact — while still surfacing the
    ///     authoritative rendering fact. Defect 2 regression.
    /// </summary>
    [Fact]
    public async Task PowerPointComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "deck.pptx", PptxFixtures.DeckWithNotes());
        var extractor = new PowerPointComExtractor(() =>
            new StubPowerPointAutomation([StubPowerPointAutomation.Rendered(1), StubPowerPointAutomation.Rendered(2)]));
        var sink = new RecordingSink();

        await extractor.ExtractAsync(DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        Assert.DoesNotContain(sink.EnvironmentFacts,
            fact => fact.Key == "powerpoint.pageRendering" && fact.Available == false);
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "pages.renderer" && fact.Available == true);
    }

    /// <summary>Writes a fixture to disk and returns its path.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
