using System.Runtime.InteropServices;
using DemaConsulting.TestResults.IO;
using DocDown.Core;
using DocDown.Tool.Cli;
using TestResultsModel = DemaConsulting.TestResults;

namespace DocDown.Tool.SelfTest;

/// <summary>
///     Drives the <c>--validate</c> self-validation: it prints an environment header, runs the
///     tool's own functionality in-process, runs the union of self-test cases every registered
///     backend contributes, prints per-test results, and optionally writes a TRX or JUnit file.
/// </summary>
/// <remarks>
///     <para>
///         The tool's own checks run the CLI in-process with <c>--silent --log &lt;temp&gt;</c> and
///         assert on the captured log, exactly the mechanism the reference DEMA tool uses. The
///         engine self-test union comes from <see cref="DocDownEngine.GetSelfTestCases"/> on the
///         engine the caller supplies, which returns Core's cases followed by each registered
///         backend's cases. This unit knows nothing about which backends those are: <c>Program</c>
///         owns registration, so the self-tests always exercise the same engine extraction uses, and
///         the unit can be driven over a smaller engine without Office or a native stack.
///     </para>
///     <para>
///         A skipped case is emitted as
///         <see cref="DemaConsulting.TestResults.TestOutcome.NotExecuted"/>, distinct from a
///         failure. This is correct and safe — it produces no false failure — but it satisfies no
///         requirement, because ReqStream's trace matrix does not count a not-executed result as
///         executed evidence.
///     </para>
/// </remarks>
internal static class Validation
{
    /// <summary>
    ///     Runs self-validation over the supplied engine and, when requested, writes the results file.
    /// </summary>
    /// <param name="context">The context carrying output routing and the requested results file.</param>
    /// <param name="engine">The engine whose registered backends contribute the self-test union.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="engine"/> is null.</exception>
    /// <remarks>
    ///     The engine is supplied rather than built here so the tool has exactly one place that
    ///     decides which backends are registered — <c>Program.BuildEngine</c> — instead of two that
    ///     must be kept in step. Each failed check and each unsupported results-file extension calls
    ///     <c>context.WriteError</c>, which drives the exit code to 1. A skipped case is not a
    ///     failure and does not affect the exit code.
    /// </remarks>
    public static void Run(Context context, DocDownEngine engine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engine);

        PrintValidationHeader(context);

        var testResults = new TestResultsModel.TestResults
        {
            Name = "DocDown Tool Self-Validation"
        };

        // The tool's own functionality, run in-process against a captured log
        RunCliTest(context, testResults, "DocDownTool_Version", ["--version"], ContainsVersion);
        RunCliTest(context, testResults, "DocDownTool_Help", ["--help"], ContainsUsage);

        // The union of self-test cases every registered backend contributes
        RunEngineSelfTests(context, testResults, engine);

        // Totals
        var total = testResults.Results.Count;
        var passed = testResults.Results.Count(t => t.Outcome == TestResultsModel.TestOutcome.Passed);
        var failed = testResults.Results.Count(t => t.Outcome == TestResultsModel.TestOutcome.Failed);
        var skipped = testResults.Results.Count(t => t.Outcome == TestResultsModel.TestOutcome.NotExecuted);

        context.WriteLine("");
        context.WriteLine($"Total: {total}");
        context.WriteLine($"Passed: {passed}");
        context.WriteLine($"Skipped: {skipped}");
        if (failed > 0)
        {
            context.WriteError($"Failed: {failed}");
        }
        else
        {
            context.WriteLine($"Failed: {failed}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>Prints the markdown header with environment facts, honoring the requested heading depth.</summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} DEMA Consulting DocDown Tool");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs one in-process CLI check and records its result.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The results collection to append to.</param>
    /// <param name="name">The test name.</param>
    /// <param name="commandArgs">The command-line arguments the check drives the tool with.</param>
    /// <param name="verify">A predicate over the captured log that decides pass or fail.</param>
    private static void RunCliTest(
        Context context,
        TestResultsModel.TestResults testResults,
        string name,
        string[] commandArgs,
        Func<string, bool> verify)
    {
        var startTime = DateTime.UtcNow;
        var test = new TestResultsModel.TestResult
        {
            Name = name,
            ClassName = "Validation",
            CodeBase = "DemaConsulting.DocDown.Tool",
            ComputerName = Environment.MachineName
        };

        try
        {
            using var work = new TemporaryDirectory();
            var logFile = Path.Combine(work.DirectoryPath, "cli-test.log");

            var args = new List<string> { "--silent", "--log", logFile };
            args.AddRange(commandArgs);

            int exitCode;
            using (var testContext = Context.Create([.. args]))
            {
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }

            var logContent = File.ReadAllText(logFile);
            if (exitCode == 0 && verify(logContent))
            {
                test.Outcome = TestResultsModel.TestOutcome.Passed;
                context.WriteLine($"[PASS] {name}");
            }
            else
            {
                test.Outcome = TestResultsModel.TestOutcome.Failed;
                test.ErrorMessage = exitCode == 0 ? "Log content did not satisfy the check" : $"Exit code {exitCode}";
                context.WriteError($"[FAIL] {name}: {test.ErrorMessage}");
            }
        }
        // Generic catch is justified: this is a self-test harness, so any exception must be recorded
        // as a failed result rather than aborting the run.
        catch (Exception ex)
        {
            test.Outcome = TestResultsModel.TestOutcome.Failed;
            test.ErrorMessage = $"Exception: {ex.Message}";
            context.WriteError($"[FAIL] {name}: {ex.Message}");
        }

        test.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(test);
    }

    /// <summary>
    ///     Runs the union of self-test cases contributed by Core and every registered backend.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The results collection to append to.</param>
    /// <param name="engine">The engine whose registered backends contribute the cases.</param>
    /// <remarks>
    ///     The caller supplies the engine, so the tool registers its backends in exactly one place
    ///     and the self-tests necessarily exercise the same registration extraction uses. Each case
    ///     runs in its own work folder.
    /// </remarks>
    private static void RunEngineSelfTests(
        Context context, TestResultsModel.TestResults testResults, DocDownEngine engine)
    {
        using var work = new TemporaryDirectory();
        foreach (var testCase in engine.GetSelfTestCases())
        {
            var result = RunSelfTestCase(testCase, work.DirectoryPath);
            var mapped = SelfTestAdapter.ToTestResult(testCase, result);
            testResults.Results.Add(mapped);

            switch (result.Status)
            {
                case SelfTestStatus.Passed:
                    context.WriteLine($"[PASS] {testCase.Name}");
                    break;
                case SelfTestStatus.Skipped:
                    context.WriteLine($"[SKIP] {testCase.Name}: {result.Message}");
                    break;
                default:
                    context.WriteError($"[FAIL] {testCase.Name}: {result.Message}");
                    break;
            }
        }
    }

    /// <summary>Runs a single self-test case in its own work folder, mapping any exception to a failure.</summary>
    /// <param name="testCase">The case to run.</param>
    /// <param name="workRoot">The root under which the case's private work folder is created.</param>
    /// <returns>The case's result, or a failure describing an exception it raised.</returns>
    private static SelfTestResult RunSelfTestCase(SelfTestCase testCase, string workRoot)
    {
        var workFolder = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);

        try
        {
            return testCase.Run(new SelfTestContext(workFolder, CancellationToken.None));
        }
        // Generic catch is justified: a self-test that throws is recorded as a failed result so the
        // run continues and reports every case.
        catch (Exception ex)
        {
            return SelfTestResult.Failed($"Exception: {ex.Message}", TimeSpan.Zero);
        }
    }

    /// <summary>Writes the results collection to a TRX (.trx) or JUnit (.xml) file.</summary>
    /// <param name="context">The context for output and the requested results file.</param>
    /// <param name="testResults">The results to serialize.</param>
    private static void WriteResultsFile(Context context, TestResultsModel.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            string content;

            if (extension == ".trx")
            {
                content = TrxSerializer.Serialize(testResults);
            }
            else if (extension == ".xml")
            {
                content = JUnitSerializer.Serialize(testResults);
            }
            else
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified as a top-level handler to log a results-file write failure.
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>Returns whether the log contains a version-like token (for example <c>1.2.3</c>).</summary>
    private static bool ContainsVersion(string log)
    {
        var pattern = new System.Text.RegularExpressions.Regex(
            @"\d+\.\d+\.\d+",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));
        return !string.IsNullOrWhiteSpace(log) && pattern.IsMatch(log);
    }

    /// <summary>Returns whether the log contains the usage and options headings.</summary>
    private static bool ContainsUsage(string log) => log.Contains("Usage:", StringComparison.Ordinal) && log.Contains("Options:", StringComparison.Ordinal);

    /// <summary>A disposable temporary directory removed on dispose.</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>Gets the absolute path of the temporary directory.</summary>
        public string DirectoryPath { get; }

        /// <summary>Creates a uniquely named temporary directory.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the directory cannot be created.</exception>
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"docdown-validate-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>Deletes the temporary directory on a best-effort basis.</summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup failures during disposal
            }
        }
    }
}
