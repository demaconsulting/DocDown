using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Visio.Tests.TestData;
using DocDown.Core;
using DocDown.Visio.Com;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Visio.Tests.Com;

/// <summary>
///     Unit tests for <see cref="VisioComExtractor"/>, exercising the whole extraction path through an
///     injected stub with no Microsoft Office.
/// </summary>
public class VisioComExtractorTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the descriptor: identifier, formats, capabilities (including rendered pages), and the
    ///     priority-zero tie-break that keeps the managed backend the default.
    /// </summary>
    [Fact]
    public void VisioComExtractor_Descriptor_MatchesContract()
    {
        var extractor = new VisioComExtractor();

        Assert.Equal("visio-com", extractor.Id);
        Assert.Contains(CoreFormat.Vsdx, extractor.SupportedFormats);
        Assert.Contains(CoreFormat.Vsdm, extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentStructure));
    }

    /// <summary>
    ///     Proves the public parameterless extractor probes without throwing and never instructs an
    ///     installation.
    /// </summary>
    [Fact]
    public void VisioComExtractor_PublicCtor_ProbeDoesNotThrowAndNeverInstructsInstallation()
    {
        var extractor = new VisioComExtractor();

        Assert.Null(Record.Exception(extractor.ProbeAvailability));
        var result = extractor.ProbeAvailability();
        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves a null adapter factory makes the backend probe unavailable on every platform.
    /// </summary>
    [Fact]
    public void VisioComExtractor_NullFactory_ProbesUnavailable()
    {
        var extractor = new VisioComExtractor(automationFactory: null);

        var result = extractor.ProbeAvailability();

        Assert.False(result.IsAvailable);
        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the COM backend delegates content to the managed backend — writing the directed
    ///     topology — and adds one rendered page per page, disposing the automation session. This is
    ///     the guarantee that content is delivered on any host and rendering is the richer addition.
    /// </summary>
    [Fact]
    public async Task VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "wash.vsdx", VsdxFixtures.WashSystem());
        var pages = new[] { StubVisioAutomation.Rendered(1) };
        StubVisioAutomation? created = null;
        var extractor = new VisioComExtractor(() =>
        {
            created = new StubVisioAutomation(pages);
            return created;
        });
        var sink = new RecordingSink();

        var outcome = await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("Inlet Tank \u2192 Transfer Pump", content, StringComparison.Ordinal);
        Assert.Single(sink.Pages);
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "pages.renderer");
        Assert.NotNull(created);
        Assert.True(created.Disposed);
        Assert.Equal(ExtractionOutcome.Succeeded, outcome);
    }

    /// <summary>
    ///     Proves a page that fails to render becomes a counted gap while the remaining pages are still
    ///     rendered and the topology is still delivered — never a silent absence.
    /// </summary>
    [Fact]
    public async Task VisioComExtractor_Extract_PageRenderFails_ReportsCountedGap()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "two.vsdx", VsdxFixtures.TwoPages());
        var pages = new[]
        {
            StubVisioAutomation.Rendered(1),
            StubVisioAutomation.Failed(2, "the page contained an unsupported shape")
        };
        var extractor = new VisioComExtractor(() => new StubVisioAutomation(pages));
        var sink = new RecordingSink();

        var outcome = await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        Assert.Equal(ExtractionOutcome.Degraded, outcome);
        Assert.Single(sink.Pages);
        var gap = Assert.Single(sink.Gaps, candidate => candidate.Kind == GapKind.Pages && candidate.Scope == GapScope.PartiallyExtracted);
        Assert.Equal(1, gap.AffectedCount);
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "VISIO0004");
    }

    /// <summary>
    ///     Proves the extractor passes the caller's render DPI through to the automation seam.
    /// </summary>
    [Fact]
    public async Task VisioComExtractor_Extract_PassesRenderDpiToAutomation()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "wash.vsdx", VsdxFixtures.WashSystem());
        StubVisioAutomation? created = null;
        var extractor = new VisioComExtractor(() =>
        {
            created = new StubVisioAutomation([StubVisioAutomation.Rendered(1)]);
            return created;
        });
        var options = new ExtractionOptions { PageRenderDpi = 300 };

        await extractor.ExtractAsync(
            DocumentSource.FromFile(input), new CapturingContext(new RecordingSink(), Ct, options));

        Assert.NotNull(created);
        Assert.Equal(300, created.LastDpi);
    }

    /// <summary>
    ///     Proves the COM run does not leak the delegated managed backend's contradictory
    ///     <c>visio.pageRendering : NOT available</c> environment fact — which would contradict the
    ///     authoritative <c>pages.renderer : available</c> fact three lines away — while still
    ///     surfacing the authoritative rendering fact. Defect 2 regression.
    /// </summary>
    [Fact]
    public async Task VisioComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact()
    {
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "wash.vsdx", VsdxFixtures.WashSystem());
        var extractor = new VisioComExtractor(() => new StubVisioAutomation([StubVisioAutomation.Rendered(1)]));
        var sink = new RecordingSink();

        await extractor.ExtractAsync(DocumentSource.FromFile(input), new CapturingContext(sink, Ct));

        Assert.DoesNotContain(sink.EnvironmentFacts,
            fact => fact.Key == "visio.pageRendering" && fact.Available == false);
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
