namespace DocDown.Core;

/// <summary>
///     Marks an extractor as contributing self-test cases that Core can enumerate and run to
///     validate the backend in its deployed environment.
/// </summary>
/// <remarks>
///     The seam exists so a backend can prove it actually works where it is installed — native
///     dependencies and platform quirks make declared capabilities insufficient evidence on
///     their own. Implementations should return cheap, self-contained cases; Core wraps the
///     cases of an unavailable backend so they report as skipped without running.
/// </remarks>
public interface ISelfValidating
{
    /// <summary>
    ///     Returns the self-test cases this extractor contributes.
    /// </summary>
    /// <returns>
    ///     The self-test cases to run; an empty sequence when the extractor contributes none.
    ///     Must never be <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     Enumerated by the engine when assembling the full self-test suite. Implementations
    ///     should make this cheap and side-effect free; the actual work happens when a case's
    ///     delegate is invoked, not when the cases are enumerated.
    /// </remarks>
    IEnumerable<SelfTestCase> GetSelfTestCases();
}
