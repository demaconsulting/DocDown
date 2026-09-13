namespace DocDown.Core;

/// <summary>
///     The outcome status of a single self-test case.
/// </summary>
/// <remarks>
///     Distinguishing <see cref="Skipped"/> from <see cref="Passed"/> and <see cref="Failed"/>
///     matters because a self-test that cannot run in the current environment (for example,
///     because its backend is unavailable) must not be reported as a pass, which would hide a
///     genuine coverage gap.
/// </remarks>
public enum SelfTestStatus
{
    /// <summary>
    ///     The self-test ran and met its expectation.
    /// </summary>
    Passed,

    /// <summary>
    ///     The self-test ran and did not meet its expectation.
    /// </summary>
    Failed,

    /// <summary>
    ///     The self-test could not run in the current environment and was skipped.
    /// </summary>
    Skipped
}
