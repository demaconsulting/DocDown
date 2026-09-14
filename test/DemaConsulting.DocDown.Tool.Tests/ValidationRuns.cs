using DemaConsulting.DocDown.TestSupport;

namespace DemaConsulting.DocDown.Tool.Tests;

/// <summary>
///     The captured outcome of one <c>--validate</c> invocation through the real command line.
/// </summary>
/// <param name="ExitCode">The exit code the tool proposed.</param>
/// <param name="Log">The captured log content.</param>
/// <param name="ResultsPath">The absolute path of the results file the run wrote.</param>
/// <remarks>Immutable, so the tests sharing one run cannot perturb each other. Thread-safe.</remarks>
public sealed record ValidationRun(int ExitCode, string Log, string ResultsPath);

/// <summary>
///     Executes the system-level <c>--validate</c> runs exactly once each, and shares them with the
///     scenarios that assert on their output.
/// </summary>
/// <remarks>
///     <para>
///         These are the only validation runs in the suite that go through the real command line over
///         the shipped engine, which on Windows means genuinely launching Microsoft Visio and
///         Microsoft PowerPoint and rasterizing a page in each. That is the point of them: the COM
///         render cases must actually run and pass here, not be skipped or tolerated. Everything the
///         <c>Validation</c> unit can be asked on its own — the header, the results-file shapes, the
///         exit code — is proven in <c>ValidationTests</c> over a managed engine that starts no
///         application at all.
///     </para>
///     <para>
///         Two runs are kept rather than one because each proves a distinct path from the command
///         line through to a serializer, and neither serializer is reachable from the other's run.
///         Each is created lazily, so a test selection that touches neither starts no application.
///     </para>
///     <para>
///         xUnit runs a class's tests one at a time, so the two runs never overlap within a process;
///         <c>CliHarness.RunValidation</c> covers the three target-framework processes.
///     </para>
/// </remarks>
public sealed class ValidationRuns : IDisposable
{
    /// <summary>The scratch folder holding the results files the shared runs wrote.</summary>
    /// <remarks>Owned for the lifetime of the fixture so a shared results file outlives the test that triggered it.</remarks>
    private readonly TempScratch _work = new();

    /// <summary>The single <c>--validate --results &lt;file&gt;.trx</c> run.</summary>
    private readonly Lazy<ValidationRun> _trx;

    /// <summary>The single <c>--validate --results &lt;file&gt;.xml</c> run.</summary>
    private readonly Lazy<ValidationRun> _jUnit;

    /// <summary>
    ///     Initializes the shared runs without executing either of them.
    /// </summary>
    /// <remarks>Construction is free; each run is launched on first use and never again.</remarks>
    public ValidationRuns()
    {
        _trx = Defer(() => Invoke("results.trx"));
        _jUnit = Defer(() => Invoke("results.xml"));
    }

    /// <summary>Gets the outcome of the <c>--validate</c> run that wrote a TRX results file.</summary>
    public ValidationRun Trx => _trx.Value;

    /// <summary>Gets the outcome of the <c>--validate</c> run that wrote a JUnit results file.</summary>
    public ValidationRun JUnit => _jUnit.Value;

    /// <summary>Removes the scratch folder holding the shared results files.</summary>
    public void Dispose() => _work.Dispose();

    /// <summary>
    ///     Wraps a run factory so it executes at most once, on first use.
    /// </summary>
    /// <param name="factory">The factory that performs the invocation.</param>
    /// <returns>The deferred run.</returns>
    private static Lazy<ValidationRun> Defer(Func<ValidationRun> factory) =>
        new(factory, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Runs one command-line validation through the serializing harness entry point.
    /// </summary>
    /// <param name="resultsFileName">The results file name to request.</param>
    /// <returns>The captured outcome.</returns>
    /// <remarks>
    ///     Routed through <c>CliHarness.RunValidation</c> so the run holds the suite's gate for its
    ///     duration. Side effect: runs the tool, which launches Office where available.
    /// </remarks>
    private ValidationRun Invoke(string resultsFileName)
    {
        var resultsPath = Path.Combine(_work.Path, resultsFileName);
        var (exit, log) = CliHarness.RunValidation("--validate", "--results", resultsPath);
        return new ValidationRun(exit, log, resultsPath);
    }
}
