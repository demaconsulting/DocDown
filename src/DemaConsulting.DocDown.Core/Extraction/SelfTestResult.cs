namespace DocDown.Core;

/// <summary>
///     The result of running a single self-test case.
/// </summary>
/// <param name="Status">Whether the case passed, failed, or was skipped.</param>
/// <param name="Message">
///     An explanatory message: the failure detail for a failed case, the skip reason for a
///     skipped case, or <see langword="null"/> for a pass.
/// </param>
/// <param name="Duration">How long the case took to run; <see cref="TimeSpan.Zero"/> when skipped.</param>
/// <remarks>
///     Reported as data so a caller can aggregate a self-test run without exceptions. The named
///     factories below make each outcome read clearly at call sites and keep the message/status
///     pairing consistent. Instances are immutable and thread-safe.
/// </remarks>
public sealed record SelfTestResult(SelfTestStatus Status, string? Message, TimeSpan Duration)
{
    /// <summary>
    ///     Creates a passing result.
    /// </summary>
    /// <param name="duration">How long the case took to run.</param>
    /// <returns>A <see cref="SelfTestResult"/> with <see cref="SelfTestStatus.Passed"/> and no message.</returns>
    /// <remarks>Pure and thread-safe.</remarks>
    public static SelfTestResult Passed(TimeSpan duration) => new(SelfTestStatus.Passed, null, duration);

    /// <summary>
    ///     Creates a failing result.
    /// </summary>
    /// <param name="message">The failure detail. Must not be null or empty.</param>
    /// <param name="duration">How long the case took to run.</param>
    /// <returns>A <see cref="SelfTestResult"/> with <see cref="SelfTestStatus.Failed"/> and the given message.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null or empty.</exception>
    /// <remarks>Requires a non-empty message so a failure always carries a displayable cause. Pure and thread-safe.</remarks>
    public static SelfTestResult Failed(string message, TimeSpan duration)
    {
        // A failure with no message is not actionable, so refuse an empty one
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new SelfTestResult(SelfTestStatus.Failed, message, duration);
    }

    /// <summary>
    ///     Creates a skipped result.
    /// </summary>
    /// <param name="reason">The reason the case was skipped. Must not be null or empty.</param>
    /// <returns>
    ///     A <see cref="SelfTestResult"/> with <see cref="SelfTestStatus.Skipped"/>, the given
    ///     reason, and a zero duration.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
    /// <remarks>
    ///     Duration is zero because a skipped case does no work. Requires a non-empty reason so a
    ///     skip is never silent. Pure and thread-safe.
    /// </remarks>
    public static SelfTestResult Skipped(string reason)
    {
        // A skip must explain itself so a coverage gap is never hidden
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new SelfTestResult(SelfTestStatus.Skipped, reason, TimeSpan.Zero);
    }
}
