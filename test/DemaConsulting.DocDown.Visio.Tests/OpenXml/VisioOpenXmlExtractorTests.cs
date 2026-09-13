using DocDown.Core;
using DocDown.Visio.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Visio.Tests.OpenXml;

/// <summary>
///     Unit tests for <see cref="VisioOpenXmlExtractor"/>'s descriptor, availability, and self-test
///     surface.
/// </summary>
public class VisioOpenXmlExtractorTests
{
    /// <summary>
    ///     Proves the descriptor: identifier, supported formats (both modern drawings), capabilities,
    ///     and priority.
    /// </summary>
    [Fact]
    public void VisioOpenXmlExtractor_Descriptor_MatchesContract()
    {
        var extractor = new VisioOpenXmlExtractor();

        Assert.Equal("visio-openxml", extractor.Id);
        Assert.Contains(CoreFormat.Vsdx, extractor.SupportedFormats);
        Assert.Contains(CoreFormat.Vsdm, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.EmbeddedImages));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentStructure));
        Assert.False(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
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
        Assert.True(extractor.ProbeAvailability().IsAvailable);
    }

    /// <summary>
    ///     Proves the parse round-trip self-test — which builds a drawing with a directed edge and
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
}
