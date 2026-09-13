using DocDown.Core;
using TestResultsModel = DemaConsulting.TestResults;

namespace DocDown.Tool.SelfTest;

/// <summary>
///     Adapts Core's dependency-free self-test records into the
///     <see cref="DemaConsulting.TestResults"/> object model so a self-validation run can be
///     serialized to TRX or JUnit.
/// </summary>
/// <remarks>
///     <para>
///         This adapter is the reason <c>DemaConsulting.TestResults</c> is referenced by the tool
///         and nowhere else: Core defines <see cref="SelfTestCase"/>, <see cref="SelfTestResult"/>,
///         and <see cref="SelfTestStatus"/> as plain records with no NuGet dependency, and the
///         mapping into the results model lives here in the tool.
///     </para>
///     <para>
///         The mapping of <see cref="SelfTestStatus.Skipped"/> is deliberate and load-bearing: a
///         skip becomes <see cref="DemaConsulting.TestResults.TestOutcome.NotExecuted"/>, which is
///         <strong>distinct</strong> from <see cref="DemaConsulting.TestResults.TestOutcome.Failed"/>.
///         ReqStream's trace matrix does not count a not-executed result as executed, so a skip
///         satisfies no requirement while also producing no false failure. All members are static
///         and pure.
///     </para>
/// </remarks>
internal static class SelfTestAdapter
{
    /// <summary>The code base recorded on every mapped result.</summary>
    private const string ToolCodeBase = "DemaConsulting.DocDown.Tool";

    /// <summary>
    ///     Maps a Core self-test status to a <see cref="DemaConsulting.TestResults.TestOutcome"/>.
    /// </summary>
    /// <param name="status">The Core self-test status.</param>
    /// <returns>
    ///     <see cref="DemaConsulting.TestResults.TestOutcome.Passed"/> for a pass,
    ///     <see cref="DemaConsulting.TestResults.TestOutcome.Failed"/> for a failure, and
    ///     <see cref="DemaConsulting.TestResults.TestOutcome.NotExecuted"/> for a skip.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="status"/> is not a known value.</exception>
    public static TestResultsModel.TestOutcome ToTestOutcome(SelfTestStatus status) => status switch
    {
        SelfTestStatus.Passed => TestResultsModel.TestOutcome.Passed,
        SelfTestStatus.Failed => TestResultsModel.TestOutcome.Failed,
        // A skip is NOT a pass and NOT a failure: it is not-executed, which ReqStream ignores as
        // evidence rather than counting as a fail.
        SelfTestStatus.Skipped => TestResultsModel.TestOutcome.NotExecuted,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown self-test status")
    };

    /// <summary>
    ///     Builds a <see cref="DemaConsulting.TestResults.TestResult"/> from a self-test case and its
    ///     result.
    /// </summary>
    /// <param name="testCase">The self-test case that ran. Must not be null.</param>
    /// <param name="result">The result the case produced. Must not be null.</param>
    /// <returns>A result carrying the case's name, category, duration, outcome, and any message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="testCase"/> or <paramref name="result"/> is null.</exception>
    /// <remarks>
    ///     The case name and category are preserved verbatim, the category as the class name, so a
    ///     reader of the emitted TRX/JUnit can group results by the backend that contributed them.
    ///     The skip reason or failure detail travels on <c>ErrorMessage</c>.
    /// </remarks>
    public static TestResultsModel.TestResult ToTestResult(SelfTestCase testCase, SelfTestResult result)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(result);

        return new TestResultsModel.TestResult
        {
            Name = testCase.Name,
            ClassName = testCase.Category,
            CodeBase = ToolCodeBase,
            ComputerName = Environment.MachineName,
            Duration = result.Duration,
            Outcome = ToTestOutcome(result.Status),
            ErrorMessage = result.Message ?? string.Empty
        };
    }
}
