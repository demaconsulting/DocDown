namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     The exception thrown by <see cref="ContractAssert"/> when an extraction folder fails a
///     contract, layout, or byte-equality assertion.
/// </summary>
/// <remarks>
///     A dedicated exception type keeps <c>DemaConsulting.DocDown.TestSupport</c> free of any test-framework
///     dependency: assertions raise this exception rather than calling into xUnit, so the same
///     helpers can be shared by every future test project regardless of its runner. The message
///     is richly formatted at the throw site to name every violation code and detail. This type
///     is immutable after construction and therefore thread-safe.
/// </remarks>
public sealed class ContractAssertionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractAssertionException"/> class.
    /// </summary>
    /// <remarks>Provided to satisfy the standard exception constructor pattern.</remarks>
    public ContractAssertionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractAssertionException"/> class with a
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    /// <remarks>The message is the fully formatted failure report built by <see cref="ContractAssert"/>.</remarks>
    public ContractAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractAssertionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    /// <param name="innerException">The exception that caused this assertion failure.</param>
    /// <remarks>Used when an assertion failure wraps a lower-level fault such as an I/O error.</remarks>
    public ContractAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
