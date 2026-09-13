using System.Xml.Linq;
using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.TestResults;
using DemaConsulting.TestResults.IO;

namespace DemaConsulting.DocDown.Tool.Tests.SelfTest;

/// <summary>
///     Unit tests for the <c>Validation</c> unit: the header, the self-test union, the results-file
///     writing, and the pass/fail exit outcome.
/// </summary>
public class ValidationTests
{
    /// <summary>Proves the header honors the heading depth and reports environment facts.</summary>
    [Fact]
    public void Validation_Run_Header_HonorsDepthAndReportsEnvironment()
    {
        var (exit, log) = CliHarness.Run("--validate", "--depth", "2");

        Assert.Equal(0, exit);
        Assert.Contains("## DEMA Consulting DocDown Tool", log, StringComparison.Ordinal);
        Assert.Contains(Environment.MachineName, log, StringComparison.Ordinal);
        Assert.Contains("UTC", log, StringComparison.Ordinal);
    }

    /// <summary>Proves the run executes Core's cases and the PDF backend's cases as one union.</summary>
    [Fact]
    public void Validation_Run_DefaultEngine_RunsCoreAndPdfSelfTestUnion()
    {
        var (exit, log) = CliHarness.Run("--validate");

        Assert.Equal(0, exit);
        Assert.Contains("core.layout-invariance", log, StringComparison.Ordinal);
        Assert.Contains("core.manifest-schema", log, StringComparison.Ordinal);
        Assert.DoesNotContain("core.gap-accuracy", log, StringComparison.Ordinal);
        Assert.Contains("pdf.parseRoundTrip", log, StringComparison.Ordinal);
        // The always-skipped page-rendering case appears as a skip, not a failure
        Assert.Contains("[SKIP] pdf.pageRendering", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a requested TRX results file is written, re-parses as well-formed XML, and carries
    ///     the real self-test union with correct per-result outcomes and consistent run-level counts.
    /// </summary>
    [Fact]
    public void Validation_Run_ResultsTrx_WritesFile()
    {
        // Arrange: a per-test scratch folder holding the requested TRX file
        using var temp = new TempScratch();
        var trx = Path.Combine(temp.Path, "results.trx");

        // Act: run a self-validation that writes the TRX results file
        var (exit, log) = CliHarness.Run("--validate", "--results", trx);

        // Assert: reported, exists, parses as well-formed XML, and carries the named self-test cases
        Assert.Equal(0, exit);
        Assert.Contains("Results written to", log, StringComparison.Ordinal);
        Assert.True(File.Exists(trx));

        var text = File.ReadAllText(trx);
        var parsed = TrxSerializer.Deserialize(text);
        Assert.NotEmpty(parsed.Results);
        Assert.Contains(parsed.Results, r => r.Name == "core.layout-invariance");
        Assert.Contains(parsed.Results, r => r.Name == "core.manifest-schema");
        Assert.DoesNotContain(parsed.Results, r => r.Name == "core.gap-accuracy");
        Assert.Contains(parsed.Results, r => r.Name == "pdf.parseRoundTrip");

        // A passing case is recorded as passed
        var roundTrip = Assert.Single(parsed.Results, r => r.Name == "pdf.parseRoundTrip");
        Assert.Equal(TestOutcome.Passed, roundTrip.Outcome);

        // The always-skipped page-rendering case is not-executed, and explicitly not a failure
        var render = Assert.Single(parsed.Results, r => r.Name == "pdf.pageRendering");
        Assert.Equal(TestOutcome.NotExecuted, render.Outcome);
        Assert.NotEqual(TestOutcome.Failed, render.Outcome);

        // Run-level counters are consistent with the individual results
        var counters = XDocument.Parse(text)
            .Descendants().First(e => e.Name.LocalName == "Counters");
        Assert.Equal(parsed.Results.Count, (int)counters.Attribute("total")!);
        Assert.Equal(parsed.Results.Count(r => r.Outcome == TestOutcome.Passed), (int)counters.Attribute("passed")!);
        Assert.Equal(parsed.Results.Count(r => r.Outcome == TestOutcome.Failed), (int)counters.Attribute("failed")!);
        Assert.Equal(0, (int)counters.Attribute("failed")!);
    }

    /// <summary>
    ///     Proves a requested JUnit (.xml) results file is written, re-parses as well-formed XML, and
    ///     records the always-skipped page-rendering case as not-executed with an explicit
    ///     <c>&lt;skipped/&gt;</c> element, with suite-level counts consistent with the results.
    /// </summary>
    [Fact]
    public void Validation_Run_ResultsXml_WritesWellFormedJUnit()
    {
        // Arrange: a per-test scratch folder holding the requested JUnit file
        using var temp = new TempScratch();
        var xml = Path.Combine(temp.Path, "results.xml");

        // Act: run a self-validation that writes the JUnit results file
        var (exit, log) = CliHarness.Run("--validate", "--results", xml);

        // Assert: reported, exists, parses as well-formed XML, and carries the named self-test cases
        Assert.Equal(0, exit);
        Assert.Contains("Results written to", log, StringComparison.Ordinal);
        Assert.True(File.Exists(xml));

        var text = File.ReadAllText(xml);
        var parsed = JUnitSerializer.Deserialize(text);
        Assert.NotEmpty(parsed.Results);
        Assert.Contains(parsed.Results, r => r.Name == "core.manifest-schema");
        Assert.DoesNotContain(parsed.Results, r => r.Name == "core.gap-accuracy");
        Assert.Contains(parsed.Results, r => r.Name == "pdf.parseRoundTrip");

        // The always-skipped page-rendering case is not-executed, and explicitly not a failure
        var render = Assert.Single(parsed.Results, r => r.Name == "pdf.pageRendering");
        Assert.Equal(TestOutcome.NotExecuted, render.Outcome);
        Assert.NotEqual(TestOutcome.Failed, render.Outcome);

        // Suite-level counts are consistent with the individual results
        var doc = XDocument.Parse(text);
        var suites = doc.Descendants().Where(e => e.Name.LocalName == "testsuite").ToList();
        Assert.Equal(parsed.Results.Count, suites.Sum(s => (int)s.Attribute("tests")!));
        Assert.Equal(
            parsed.Results.Count(r => r.Outcome == TestOutcome.NotExecuted),
            suites.Sum(s => (int)s.Attribute("skipped")!));

        // The skipped case carries an explicit <skipped/> element
        var renderCase = doc.Descendants().First(e =>
            e.Name.LocalName == "testcase" && (string?)e.Attribute("name") == "pdf.pageRendering");
        Assert.Contains(renderCase.Elements(), e => e.Name.LocalName == "skipped");
    }

    /// <summary>Proves an unsupported results-file extension is reported as an error.</summary>
    [Fact]
    public void Validation_Run_UnsupportedResultsExtension_WritesError()
    {
        using var temp = new TempScratch();
        var json = Path.Combine(temp.Path, "results.json");

        var (exit, log) = CliHarness.Run("--validate", "--results", json);

        Assert.Equal(1, exit);
        Assert.Contains("Unsupported results file format", log, StringComparison.Ordinal);
    }

    /// <summary>Proves an all-pass run (with only benign skips) exits zero.</summary>
    [Fact]
    public void Validation_Run_AllPass_ExitCodeZero()
    {
        var (exit, log) = CliHarness.Run("--validate");

        Assert.Equal(0, exit);
        Assert.DoesNotContain("[FAIL]", log, StringComparison.Ordinal);
    }
}
