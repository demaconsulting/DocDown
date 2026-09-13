namespace DocDown.Word.OpenXml;

/// <summary>
///     The exception the Word backend raises for a document it can recognize as unextractable — most
///     importantly a password-protected document — so Core can convert it into a clear structured
///     failure rather than surfacing a raw parser error.
/// </summary>
/// <remarks>
///     Core catches any exception a backend throws and converts it into a structured
///     <c>ExtractorFailed</c> failure with the full layout still written. This type exists so the
///     message that reaches the caller is the backend's own plain explanation (for example that the
///     document is encrypted) rather than the SDK's low-level XML error. Public because Core and
///     callers may reference the failure it produces.
/// </remarks>
public sealed class WordExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WordExtractionException"/> class.
    /// </summary>
    /// <remarks>Provided for completeness; prefer the message-carrying constructors.</remarks>
    public WordExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The plain explanation of why the document cannot be extracted.</param>
    /// <remarks>The message is surfaced to the caller through the structured failure Core produces.</remarks>
    public WordExtractionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordExtractionException"/> class with a message
    ///     and an inner exception.
    /// </summary>
    /// <param name="message">The plain explanation of why the document cannot be extracted.</param>
    /// <param name="innerException">The underlying cause.</param>
    /// <remarks>Preserves the underlying cause while presenting a clear top-level message.</remarks>
    public WordExtractionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
