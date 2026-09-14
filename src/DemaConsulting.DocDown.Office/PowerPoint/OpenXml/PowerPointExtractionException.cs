namespace DocDown.PowerPoint.OpenXml;

/// <summary>
///     The exception the PowerPoint reader or COM adapter raises for a deck it cannot open or
///     interpret, so Core can convert it into a structured failure rather than letting a raw fault
///     reach the caller.
/// </summary>
/// <remarks>
///     Raised for a missing presentation part, a package the Open XML SDK cannot open (an encrypted
///     or malformed <c>.pptx</c>), or a COM automation failure. Core catches it and writes a
///     structured failure with the full output layout still present.
/// </remarks>
public sealed class PowerPointExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class.
    /// </summary>
    public PowerPointExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    public PowerPointExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    /// <param name="innerException">The underlying fault.</param>
    public PowerPointExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
