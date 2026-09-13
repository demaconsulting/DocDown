using DocDown.Core;
using DocDown.PowerPoint.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.PowerPoint.Tests.OpenXml;

/// <summary>
///     Unit tests for <see cref="PowerPointOpenXmlExtractor"/>'s descriptor, availability, and
///     self-test surface.
/// </summary>
public class PowerPointOpenXmlExtractorTests
{
    /// <summary>
    ///     Proves the descriptor: identifier, supported format, capabilities, and priority.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_Descriptor_MatchesContract()
    {
        var extractor = new PowerPointOpenXmlExtractor();

        Assert.Equal("powerpoint-openxml", extractor.Id);
        Assert.Contains(CoreFormat.Pptx, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.Text));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.EmbeddedImages));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentStructure));
        Assert.False(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
    }

    /// <summary>
    ///     Proves the extractor is unconditionally available and never throws when probed.
    /// </summary>
    [Fact]
    public void PowerPointOpenXmlExtractor_ProbeAvailability_AlwaysAvailable()
    {
        var extractor = new PowerPointOpenXmlExtractor();

        Assert.Null(Record.Exception(extractor.ProbeAvailability));
        Assert.True(extractor.ProbeAvailability().IsAvailable);
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
}
