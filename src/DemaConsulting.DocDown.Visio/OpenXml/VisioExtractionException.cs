namespace DocDown.Visio.OpenXml;

/// <summary>
///     The exception the Visio reader or COM adapter raises for a drawing it cannot open or
///     interpret, so Core can convert it into a structured failure rather than letting a raw fault
///     reach the caller.
/// </summary>
/// <remarks>
///     Raised for a package that is not a valid Open Packaging container, a drawing with no pages
///     part, or a COM automation failure. Core catches it and writes a structured failure with the
///     full output layout still present.
/// </remarks>
public sealed class VisioExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VisioExtractionException"/> class.
    /// </summary>
    public VisioExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VisioExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    public VisioExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VisioExtractionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    /// <param name="innerException">The underlying fault.</param>
    public VisioExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
