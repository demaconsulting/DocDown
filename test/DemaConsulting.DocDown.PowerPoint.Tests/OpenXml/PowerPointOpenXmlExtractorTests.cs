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
}
