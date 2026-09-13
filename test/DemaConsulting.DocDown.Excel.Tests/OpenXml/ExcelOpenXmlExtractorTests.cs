using DocDown.Core;
using DocDown.Excel.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Excel.Tests.OpenXml;

/// <summary>
///     Unit tests for <see cref="ExcelOpenXmlExtractor"/>'s descriptor, availability, and self-test
///     surface.
/// </summary>
public class ExcelOpenXmlExtractorTests
{
    /// <summary>
    ///     Proves the descriptor: identifier, supported format, capabilities, and priority.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlExtractor_Descriptor_MatchesContract()
    {
        var extractor = new ExcelOpenXmlExtractor();

        Assert.Equal("excel-openxml", extractor.Id);
        Assert.Contains(CoreFormat.Xlsx, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.Text));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.EmbeddedImages));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentStructure));
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentMetadata));
        Assert.False(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
        Assert.False(extractor.PageRenderingApplicable);
    }

    /// <summary>
    ///     Proves the extractor is unconditionally available with its full declared capabilities and
    ///     never throws when probed.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlExtractor_ProbeAvailability_AlwaysAvailable()
    {
        var extractor = new ExcelOpenXmlExtractor();

        var thrown = Record.Exception(extractor.ProbeAvailability);
        Assert.Null(thrown);

        var availability = extractor.ProbeAvailability();
        Assert.True(availability.IsAvailable);
        Assert.Equal(extractor.Capabilities, availability.EffectiveCapabilities);
    }

    /// <summary>
    ///     Proves the extractor contributes a parse round-trip case and a skipped rendering case, and
    ///     that the round-trip passes in this environment.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped()
    {
        var extractor = new ExcelOpenXmlExtractor();
        using var work = new DemaConsulting.DocDown.TestSupport.TempScratch();
        var context = new SelfTestContext(work.Path, TestContext.Current.CancellationToken);

        var cases = extractor.GetSelfTestCases().ToList();

        Assert.Equal(2, cases.Count);
        var roundTrip = cases.Single(c => c.Name == "excel.openxml.parseRoundTrip");
        Assert.Equal(SelfTestStatus.Passed, roundTrip.Run(context).Status);
        var rendering = cases.Single(c => c.Name == "excel.pageRendering");
        Assert.Equal(SelfTestStatus.Skipped, rendering.Run(context).Status);
    }
}
