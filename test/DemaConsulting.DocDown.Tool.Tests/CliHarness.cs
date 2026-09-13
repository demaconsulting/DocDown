using DocDown.Tool;
using DocDown.Tool.Cli;

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
