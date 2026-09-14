namespace DocDown.Excel.OpenXml;

/// <summary>
///     The exception the Excel reader raises for a workbook it cannot open or interpret, so Core can
///     convert it into a structured failure rather than letting a raw Open XML fault reach the caller.
/// </summary>
/// <remarks>
///     Raised for a missing workbook part or a package the Open XML SDK cannot open (an encrypted or
///     malformed <c>.xlsx</c>). Core catches it and writes a structured failure with the full output
///     layout still present, so an adverse workbook never surfaces as an unhandled exception.
/// </remarks>
public sealed class ExcelExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class.
    /// </summary>
    public ExcelExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    public ExcelExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    /// <param name="innerException">The underlying fault.</param>
    public ExcelExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
