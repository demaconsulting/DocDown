namespace DocDown.Core;

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
