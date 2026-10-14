using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Office.Tests.PowerPoint.TestData;
using DemaConsulting.DocDown.Office.Tests.Visio.TestData;
using DemaConsulting.DocDown.PowerPoint.OpenXml;
using DemaConsulting.DocDown.TestSupport;
using CoreFormat = DemaConsulting.DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint.OpenXml;

/// <summary>
///     Unit tests for <see cref="PowerPointOpenXmlExtractor"/>'s descriptor, availability, and
///     self-test surface.
/// </summary>
public class PowerPointOpenXmlExtractorTests
{
    /// <summary>
    ///     Proves the descriptor: identifier, supported format, page-rendering applicability, and
    ///     priority.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_Descriptor_MatchesContract()
    {
        IDocumentExtractor extractor = new PowerPointOpenXmlExtractor();

        Assert.Equal("powerpoint-openxml", extractor.Id);
        Assert.Contains(CoreFormat.Pptx, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.True(extractor.PageRenderingApplicable);
    }

    /// <summary>
    ///     Proves the extractor is unconditionally available and never throws when probed.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_ProbeAvailability_AlwaysAvailable()
    {
        var extractor = new PowerPointOpenXmlExtractor();
        var availability = extractor.ProbeAvailability();

        Assert.Null(Record.Exception(extractor.ProbeAvailability));
        Assert.True(availability.IsAvailable);
        Assert.False(availability.ProvidesRenderedPages);
    }

    /// <summary>
    ///     Proves the parse round-trip self-test passes and the rendering case is skipped.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped()
    {
        var extractor = new PowerPointOpenXmlExtractor();
        using var work = new DemaConsulting.DocDown.TestSupport.TempScratch();
        var context = new SelfTestContext(work.Path, TestContext.Current.CancellationToken);

        var cases = extractor.GetSelfTestCases().ToList();

        Assert.Equal(2, cases.Count);
        var roundTrip = cases.Single(c => c.Name == "powerpoint.openxml.parseRoundTrip");
        Assert.Equal(SelfTestStatus.Passed, roundTrip.Run(context).Status);
        var rendering = cases.Single(c => c.Name == "powerpoint.pageRendering");
        Assert.Equal(SelfTestStatus.Skipped, rendering.Run(context).Status);
    }

    /// <summary>
    ///     Proves the skipped rendering case tells the operator rendering comes from the
    ///     PowerPoint.Rendering package, and never points at a COM backend that does not exist.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_SelfTestCases_RenderingSkipNamesRenderingPackage()
    {
        var extractor = new PowerPointOpenXmlExtractor();
        using var work = new DemaConsulting.DocDown.TestSupport.TempScratch();
        var context = new SelfTestContext(work.Path, TestContext.Current.CancellationToken);

        var rendering = extractor.GetSelfTestCases().Single(c => c.Name == "powerpoint.pageRendering");
        var result = rendering.Run(context);

        Assert.Equal(SelfTestStatus.Skipped, result.Status);
        Assert.Contains("PowerPoint.Rendering package", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("COM", result.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a corrupt package makes the extractor throw a <see cref="PowerPointExtractionException"/>,
    ///     leaving Core to convert the fault into an unreadable result rather than the extractor
    ///     swallowing it or reporting <c>Produced</c>.
    /// </summary>
    [Fact]
    public async Task PowerPointOpenXmlExtractor_ExtractAsync_CorruptDeck_ThrowsExtractionException()
    {
        var extractor = new PowerPointOpenXmlExtractor();
        var sink = new RecordingSink();
        var context = new StubExtractionContext(new ExtractionOptions(), sink, TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(PptxFixtures.CorruptPptxBytes());
        var source = DocumentSource.FromStream(stream, "corrupt.pptx");

        await Assert.ThrowsAsync<PowerPointExtractionException>(
            async () => await extractor.ExtractAsync(source, context));
        Assert.Empty(sink.ContentWrites);
    }

    /// <summary>
    ///     Proves an already-canceled token surfaces as <see cref="OperationCanceledException"/> rather
    ///     than an unreadable result or a produced one.
    /// </summary>
    [Fact]
    public async Task PowerPointOpenXmlExtractor_ExtractAsync_CancelledToken_Throws()
    {
        var extractor = new PowerPointOpenXmlExtractor();
        var sink = new RecordingSink();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new StubExtractionContext(new ExtractionOptions(), sink, cancellation.Token);
        using var stream = new MemoryStream(PptxFixtures.DeckWithNotes());
        var source = DocumentSource.FromStream(stream, "deck.pptx");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await extractor.ExtractAsync(source, context));
        Assert.Empty(sink.ContentWrites);
    }
}
