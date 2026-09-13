using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Tool.Tests.TestData;
using ToolProgram = DocDown.Tool.Program;

namespace DemaConsulting.DocDown.Tool.Tests;

/// <summary>
///     Unit tests for the <c>Program</c> unit: priority-ordered dispatch, the version and help
///     output, the extraction path, and the expected-error handling in <c>Main</c>.
/// </summary>
public class ProgramTests
{
    /// <summary>Proves the version flag writes an informational version string.</summary>
    [Fact]
    public void Program_Run_VersionFlag_WritesInformationalVersion()
    {
        var (exit, log) = CliHarness.Run("--version");

        Assert.Equal(0, exit);
        Assert.Matches(@"\d+\.\d+\.\d+", log);
    }

    /// <summary>Proves the help flag writes the usage and option list.</summary>
    [Fact]
    public void Program_Run_HelpFlag_WritesUsageAndOptions()
    {
        var (exit, log) = CliHarness.Run("--help");

        Assert.Equal(0, exit);
        Assert.Contains("Usage: docdown", log, StringComparison.Ordinal);
        Assert.Contains("Options:", log, StringComparison.Ordinal);
        Assert.Contains("--input", log, StringComparison.Ordinal);
    }

    /// <summary>Proves dispatch is priority-ordered: version beats help and validate.</summary>
    [Fact]
    public void Program_Run_PriorityOrder_VersionBeatsOtherCommands()
    {
        var (exit, log) = CliHarness.Run("--version", "--help", "--validate");

        // Version wins: only the version prints, and neither help nor the banner appears
        Assert.Equal(0, exit);
        Assert.Matches(@"\d+\.\d+\.\d+", log);
        Assert.DoesNotContain("Usage:", log, StringComparison.Ordinal);
        Assert.DoesNotContain("DocDown Tool version", log, StringComparison.Ordinal);
    }

    /// <summary>Proves the extraction path prints the absolute summary path and exits zero.</summary>
    [Fact]
    public void Program_Run_Extraction_PrintsAbsoluteSummaryPath()
    {
        // Arrange: a generated PDF and a fresh scratch folder unique to this run
        using var temp = new TempScratch();
        var input = Path.Combine(temp.Path, "input.pdf");
        File.WriteAllBytes(input, ToolPdfFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the extraction
        var (exit, log) = CliHarness.Run("--input", input, "--scratch", scratch);

        // Assert: the emitted summary line — read out of the log, never recomputed — is fully
        // qualified, names summary.txt, exists on disk, and lies under the scratch folder targeted
        Assert.Equal(0, exit);
        Assert.Contains("Extraction produced the output layout.", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Extraction succeeded", log, StringComparison.Ordinal);
        var summaryLine = FindSummaryLine(log);
        Assert.True(Path.IsPathFullyQualified(summaryLine));
        Assert.EndsWith("summary.txt", summaryLine, StringComparison.Ordinal);
        Assert.True(File.Exists(summaryLine));
        Assert.StartsWith(Path.GetFullPath(scratch), summaryLine, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Proves an unreadable input renders the structured failure rather than throwing.</summary>
    [Fact]
    public void Program_Run_UnreadableInput_RendersStructuredFailure()
    {
        using var temp = new TempScratch();
        var input = Path.Combine(temp.Path, "missing.pdf");
        var scratch = Path.Combine(temp.Path, "out");

        var (exit, log) = CliHarness.Run("--input", input, "--scratch", scratch);

        Assert.Equal(1, exit);
        Assert.Contains("The source document 'missing.pdf' could not be read.", log, StringComparison.Ordinal);
        Assert.Contains("Detected format:", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Extraction failed.", log, StringComparison.Ordinal);
    }

    /// <summary>Proves a missing required <c>--input</c> is an expected argument error.</summary>
    [Fact]
    public void Program_Run_MissingInput_ThrowsArgumentException()
    {
        using var temp = new TempScratch();
        var scratch = Path.Combine(temp.Path, "out");

        Assert.Throws<ArgumentException>(() => CliHarness.Run("--scratch", scratch));
    }

    /// <summary>Proves an unrecognized argument returns 1 with an error and no stack trace.</summary>
    [Fact]
    public void Program_Main_UnknownArgument_WritesErrorAndReturnsOneWithoutStackTrace()
    {
        var (exit, error) = RunMainCapturingError("--bogus-argument");

        Assert.Equal(1, exit);
        Assert.Contains("Error:", error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
    }

    /// <summary>Proves a log file that cannot be opened returns 1 with an error and no stack trace.</summary>
    [Fact]
    public void Program_Main_LogOpenFailure_ReturnsOneWithoutStackTrace()
    {
        var missingDir = Path.Combine(Path.GetTempPath(), $"docdown-missing-{Guid.NewGuid():N}");
        var logPath = Path.Combine(missingDir, "out.log");

        var (exit, error) = RunMainCapturingError("--log", logPath, "--version");

        Assert.Equal(1, exit);
        Assert.Contains("Error:", error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
    }

    /// <summary>Proves <c>--list-backends</c> reports the PDF backend.</summary>
    [Fact]
    public void Program_Run_ListBackends_ListsPdf()
    {
        var (exit, log) = CliHarness.Run("--list-backends");
        var lines = log.Split('\n').Select(static line => line.TrimEnd('\r')).ToList();
        var pdfIndex = lines.FindIndex(static line => line == "  pdf - PDF (PdfPig)");

        Assert.Equal(0, exit);
        Assert.Contains("Registered backends:", log, StringComparison.Ordinal);
        Assert.DoesNotContain("capabilities:", log, StringComparison.Ordinal);
        Assert.True(pdfIndex >= 0);
        Assert.Equal("    formats: pdf", lines[pdfIndex + 1]);
        Assert.Equal("    status: available", lines[pdfIndex + 2]);
    }

    /// <summary>Runs <c>Program.Main</c> while capturing standard error.</summary>
    private static (int ExitCode, string Error) RunMainCapturingError(params string[] args)
    {
        var originalError = Console.Error;
        var originalOut = Console.Out;
        using var capturedError = new StringWriter();
        using var capturedOut = new StringWriter();
        Console.SetError(capturedError);
        Console.SetOut(capturedOut);
        try
        {
            var exit = ToolProgram.Main(args);
            return (exit, capturedError.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOut);
        }
    }

    /// <summary>Finds the line of tool output that names the produced <c>summary.txt</c>.</summary>
    private static string FindSummaryLine(string log)
    {
        var line = log
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.EndsWith("summary.txt", StringComparison.Ordinal));
        Assert.NotNull(line);
        return line;
    }
}
