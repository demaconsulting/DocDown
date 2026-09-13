using DemaConsulting.TestResults;
using DocDown.Core;
using DocDown.Tool.SelfTest;

namespace DemaConsulting.DocDown.Tool.Tests.SelfTest;

/// <summary>
///     Unit tests for the <c>SelfTestAdapter</c> unit: the mapping from Core's dependency-free
///     self-test records into the <see cref="DemaConsulting.TestResults"/> object model.
/// </summary>
public class SelfTestAdapterTests
{
    /// <summary>Proves a passed status maps to a passed outcome.</summary>
    [Fact]
    public void SelfTestAdapter_ToTestOutcome_Passed_ReturnsPassed()
    {
        Assert.Equal(TestOutcome.Passed, SelfTestAdapter.ToTestOutcome(SelfTestStatus.Passed));
    }

    /// <summary>Proves a failed status maps to a failed outcome.</summary>
    [Fact]
    public void SelfTestAdapter_ToTestOutcome_Failed_ReturnsFailed()
    {
        Assert.Equal(TestOutcome.Failed, SelfTestAdapter.ToTestOutcome(SelfTestStatus.Failed));
    }

    /// <summary>
    ///     Proves a skipped status maps to <see cref="TestOutcome.NotExecuted"/>, distinct from a
    ///     failure — the load-bearing mapping the trace matrix depends on.
    /// </summary>
    [Fact]
    public void SelfTestAdapter_ToTestOutcome_Skipped_ReturnsNotExecuted()
    {
        var outcome = SelfTestAdapter.ToTestOutcome(SelfTestStatus.Skipped);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
        Assert.NotEqual(TestOutcome.Failed, outcome);
    }

    /// <summary>Proves the mapping preserves the case name, category, duration, and message.</summary>
    [Fact]
    public void SelfTestAdapter_ToTestResult_PreservesNameCategoryDurationAndMessage()
    {
        var testCase = new SelfTestCase("pdf.pageRendering", "pdf", _ => SelfTestResult.Skipped("not attempted"));
        var result = SelfTestResult.Skipped("not attempted");

        var mapped = SelfTestAdapter.ToTestResult(testCase, result);

        Assert.Equal("pdf.pageRendering", mapped.Name);
        Assert.Equal("pdf", mapped.ClassName);
        Assert.Equal(result.Duration, mapped.Duration);
        Assert.Equal(TestOutcome.NotExecuted, mapped.Outcome);
        Assert.Equal("not attempted", mapped.ErrorMessage);
    }

    /// <summary>Proves a failed case carries its failure message on the mapped result.</summary>
    [Fact]
    public void SelfTestAdapter_ToTestResult_FailedCase_CarriesMessage()
    {
        var testCase = new SelfTestCase("core.example", "core", _ => SelfTestResult.Failed("boom", TimeSpan.FromMilliseconds(5)));
        var result = SelfTestResult.Failed("boom", TimeSpan.FromMilliseconds(5));

        var mapped = SelfTestAdapter.ToTestResult(testCase, result);

        Assert.Equal(TestOutcome.Failed, mapped.Outcome);
        Assert.Equal("boom", mapped.ErrorMessage);
    }
}
