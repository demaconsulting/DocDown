using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Office.Tests.Visio.TestData;
using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Visio.OpenXml;
using CoreFormat = DemaConsulting.DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Office.Tests.Visio.OpenXml;

/// <summary>
///     Unit tests for <see cref="VisioOpenXmlExtractor"/>'s descriptor, availability, and self-test
///     surface.
/// </summary>
public class VisioOpenXmlExtractorTests
{
    /// <summary>
    ///     Proves the descriptor: identifier, display name, supported formats (both modern
    ///     drawings), priority, and the fact that page rendering is meaningful for Visio drawings.
    /// </summary>
    [Fact]
    public void VisioOpenXmlExtractor_Descriptor_MatchesContract()
    {
        var extractor = new VisioOpenXmlExtractor();

        Assert.Equal("visio-openxml", extractor.Id);
        Assert.Equal("Visio (Open Packaging)", extractor.DisplayName);
        Assert.Contains(CoreFormat.Vsdx, extractor.SupportedFormats);
        Assert.Contains(CoreFormat.Vsdm, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.True(((IDocumentExtractor)extractor).PageRenderingApplicable);
    }

    /// <summary>
    ///     Proves the extractor is unconditionally available and never throws when probed.
    /// </summary>
    [Fact]
    public void VisioOpenXmlExtractor_ProbeAvailability_AlwaysAvailable()
    {
        var extractor = new VisioOpenXmlExtractor();

        Assert.Null(Record.Exception(extractor.ProbeAvailability));
        var availability = extractor.ProbeAvailability();
        Assert.True(availability.IsAvailable);
        Assert.False(availability.ProvidesRenderedPages);
        Assert.Null(availability.UnavailableReason);
    }

    /// <summary>
    ///     Proves the parse round-trip self-test — which reads back the embedded probe drawing and
    ///     confirms the connection resolves — passes, and the rendering case is skipped.
    /// </summary>
    [Fact]
    public void VisioOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped()
    {
        var extractor = new VisioOpenXmlExtractor();
        using var work = new DemaConsulting.DocDown.TestSupport.TempScratch();
        var context = new SelfTestContext(work.Path, TestContext.Current.CancellationToken);

        var cases = extractor.GetSelfTestCases().ToList();

        Assert.Equal(2, cases.Count);
        var roundTrip = cases.Single(c => c.Name == "visio.openxml.parseRoundTrip");
        Assert.Equal(SelfTestStatus.Passed, roundTrip.Run(context).Status);
        var rendering = cases.Single(c => c.Name == "visio.pageRendering");
        Assert.Equal(SelfTestStatus.Skipped, rendering.Run(context).Status);
    }

    /// <summary>
    ///     Proves a corrupt package makes the extractor throw, leaving Core to convert the fault into
    ///     an unreadable result rather than the extractor swallowing it or reporting <c>Produced</c>.
    /// </summary>
    [Fact]
    public async Task VisioOpenXmlExtractor_ExtractAsync_CorruptPackage_ThrowsExtractionException()
    {
        // Arrange: bytes that are not an Open Packaging container
        var extractor = new VisioOpenXmlExtractor();
        var sink = new RecordingSink();
        var context = new StubExtractionContext(new ExtractionOptions(), sink, TestContext.Current.CancellationToken);
        using var stream = new MemoryStream("this is not a zip package"u8.ToArray());
        var source = DocumentSource.FromStream(stream, "corrupt.vsdx");

        // Act & Assert: the fault propagates unmasked and nothing was written as content
        await Assert.ThrowsAsync<VisioExtractionException>(async () => await extractor.ExtractAsync(source, context));
        Assert.Empty(sink.ContentWrites);
    }

    /// <summary>
    ///     Proves an already-canceled token surfaces as <see cref="OperationCanceledException"/> rather
    ///     than an unreadable result or a produced one.
    /// </summary>
    [Fact]
    public async Task VisioOpenXmlExtractor_ExtractAsync_CancelledToken_Throws()
    {
        // Arrange: a valid drawing and a token that is already canceled
        var extractor = new VisioOpenXmlExtractor();
        var sink = new RecordingSink();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new StubExtractionContext(new ExtractionOptions(), sink, cancellation.Token);
        using var stream = new MemoryStream(VsdxFixtures.WashSystem());
        var source = DocumentSource.FromStream(stream, "wash.vsdx");

        // Act & Assert: cancellation propagates and no content was written
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await extractor.ExtractAsync(source, context));
        Assert.Empty(sink.ContentWrites);
    }
}
