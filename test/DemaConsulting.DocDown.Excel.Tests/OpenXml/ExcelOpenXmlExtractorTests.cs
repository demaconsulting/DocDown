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
    ///     Proves the descriptor-facing properties: identifier, display name, supported format,
    ///     priority, and the non-paginated page-rendering flag.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlExtractor_Descriptor_MatchesContract()
    {
        var extractor = new ExcelOpenXmlExtractor();

        Assert.Equal("excel-openxml", extractor.Id);
        Assert.Equal("Excel (Open XML SDK)", extractor.DisplayName);
        Assert.Contains(CoreFormat.Xlsx, extractor.SupportedFormats);
        Assert.Equal(10, extractor.Priority);
        Assert.False(extractor.PageRenderingApplicable);
    }

    /// <summary>
    ///     Proves the extractor is unconditionally available, reports no unavailable reason, and
    ///     never claims rendered pages for a non-paginated workbook.
    /// </summary>
    [Fact]
    public void ExcelOpenXmlExtractor_ProbeAvailability_AlwaysAvailable()
    {
        var extractor = new ExcelOpenXmlExtractor();

        var thrown = Record.Exception(extractor.ProbeAvailability);
        Assert.Null(thrown);

        var availability = extractor.ProbeAvailability();
        Assert.True(availability.IsAvailable);
        Assert.Null(availability.UnavailableReason);
        Assert.False(availability.ProvidesRenderedPages);
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
