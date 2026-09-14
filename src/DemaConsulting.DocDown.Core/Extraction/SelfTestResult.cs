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

/// <summary>
///     A single named self-test case: a runnable check contributed by Core or a backend.
/// </summary>
/// <param name="Name">A short, unique, human-readable name for the case.</param>
/// <param name="Category">
///     A grouping category (for example <c>core</c> or a backend identifier) used to organize
///     and filter results.
/// </param>
/// <param name="Run">
///     The delegate that executes the case against a <see cref="SelfTestContext"/> and returns
///     its <see cref="SelfTestResult"/>. The work happens only when this delegate is invoked.
/// </param>
/// <remarks>
///     Deferring the work behind a delegate lets Core enumerate cases cheaply — for example to
///     count or wrap them — without running anything, and lets an unavailable backend's cases be
///     replaced with skip-returning wrappers. Instances are immutable and thread-safe; the
///     delegate's own thread-safety is the responsibility of its author.
/// </remarks>
public sealed record SelfTestCase(string Name, string Category, Func<SelfTestContext, SelfTestResult> Run);

/// <summary>
///     The execution context handed to a self-test case when it runs.
/// </summary>
/// <remarks>
///     Provides a case with a private working folder and a cancellation token so it can create
///     temporary artifacts without colliding with other cases and can honor cooperative
///     cancellation. Instances are immutable after construction and thread-safe to read.
/// </remarks>
public sealed class SelfTestContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SelfTestContext"/> class.
    /// </summary>
    /// <param name="workFolder">
    ///     An existing, writable folder the case may use for temporary files. Must not be null or
    ///     empty.
    /// </param>
    /// <param name="cancellationToken">A token the case should observe for cooperative cancellation.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="workFolder"/> is null or empty.</exception>
    /// <remarks>
    ///     Validates the work folder at construction so a case never receives an unusable context.
    /// </remarks>
    public SelfTestContext(string workFolder, CancellationToken cancellationToken)
    {
        // Refuse an empty work folder so every case has a usable scratch location
        ArgumentException.ThrowIfNullOrEmpty(workFolder);
        WorkFolder = workFolder;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Gets the writable folder the self-test case may use for temporary files.
    /// </summary>
    /// <remarks>
    ///     Supplied by Core so cases do not invent their own temp locations and can be cleaned up
    ///     centrally.
    /// </remarks>
    public string WorkFolder { get; }

    /// <summary>
    ///     Gets the cancellation token the self-test case should observe.
    /// </summary>
    /// <remarks>
    ///     Lets a long-running case abort promptly when the caller cancels the self-test run.
    /// </remarks>
    public CancellationToken CancellationToken { get; }
}
