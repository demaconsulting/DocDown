using DocDown.Core;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;
using DocDown.Tool;
using DocDown.Tool.Cli;
using DocDown.Tool.SelfTest;

namespace DemaConsulting.DocDown.Tool.Tests;

/// <summary>
///     Shared helpers for driving the <c>docdown</c> tool in-process and capturing its output.
/// </summary>
/// <remarks>
///     The tool routes all output through a <see cref="Context"/>, so a test runs it with
///     <c>--silent --log &lt;temp&gt;</c> and asserts on the captured log — the same mechanism the
///     tool's own self-validation uses. All members are static.
/// </remarks>
internal static class CliHarness
{
    /// <summary>
    ///     Runs the tool's <see cref="Program.Run"/> in-process against a captured log.
    /// </summary>
    /// <param name="args">The command-line arguments (excluding the <c>--silent</c>/<c>--log</c> capture flags).</param>
    /// <returns>The proposed exit code and the captured log content.</returns>
    public static (int ExitCode, string Log) Run(params string[] args)
    {
        var logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"docdown-test-{Guid.NewGuid():N}.log");
        try
        {
            var full = new List<string> { "--silent", "--log", logFile };
            full.AddRange(args);

            int exit;
            using (var context = Context.Create([.. full]))
            {
                Program.Run(context);
                exit = context.ExitCode;
            }

            return (exit, File.ReadAllText(logFile));
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Runs a <c>--validate</c> invocation through the real command line.
    /// </summary>
    /// <param name="args">The command-line arguments, which must include <c>--validate</c>.</param>
    /// <returns>The proposed exit code and the captured log content.</returns>
    /// <remarks>
    ///     <c>--validate</c> drives every registered backend's self-test cases, including the Visio
    ///     and PowerPoint COM renders, and Office automation is single-instance. Nothing here guards
    ///     against a concurrent run, because nothing in the suite runs concurrently: parallelism is
    ///     off in <c>test/xunit.runner.json</c>, and <c>build.ps1</c> runs the target frameworks and
    ///     the test projects one at a time. Each render therefore simply runs and passes.
    /// </remarks>
    public static (int ExitCode, string Log) RunValidation(params string[] args) => Run(args);
    /// <summary>
    ///     Runs self-validation over the supplied engine, without going through the CLI dispatch.
    /// </summary>
    /// <param name="engine">The engine whose registered backends contribute the self-test union.</param>
    /// <param name="args">Any further arguments, such as <c>--depth</c> or <c>--results</c>.</param>
    /// <returns>The proposed exit code and the captured log content.</returns>
    /// <remarks>
    ///     Lets a unit test of the <c>Validation</c> unit choose its own engine, which is the point:
    ///     the header, the results-file writing, the pass/skip/fail accounting and the exit code are
    ///     properties of the unit, not of which backends happen to be registered. Driving them over a
    ///     small managed engine proves them without launching Microsoft Office, so these tests create
    ///     no contention at all and need no gate.
    /// </remarks>
    public static (int ExitCode, string Log) RunSelfValidation(DocDownEngine engine, params string[] args)
    {
        var logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"docdown-test-{Guid.NewGuid():N}.log");
        try
        {
            var full = new List<string> { "--silent", "--log", logFile, "--validate" };
            full.AddRange(args);

            int exit;
            using (var context = Context.Create([.. full]))
            {
                Validation.Run(context, engine);
                exit = context.ExitCode;
            }

            return (exit, File.ReadAllText(logFile));
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Builds the engine the <c>Validation</c> unit tests run against.
    /// </summary>
    /// <returns>An engine registering only the fully managed PDF backends.</returns>
    /// <remarks>
    ///     Enough to produce every case shape the unit's accounting has to handle — a passing round
    ///     trip, an always-skipped page-rendering case, and a native-stack render — and deliberately
    ///     no Office backend, because none of those assertions is about Visio or PowerPoint.
    /// </remarks>
    public static DocDownEngine BuildManagedEngine() =>
        new DocDownBuilder().AddPdf().AddPdfRendering().Build();

    /// <summary>
    ///     Finds the repository root by walking up from the test assembly until the solution file is found.
    /// </summary>
    /// <returns>The absolute path of the repository root.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the root cannot be located.</exception>
    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "DocDown.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (DocDown.slnx).");
    }
}
