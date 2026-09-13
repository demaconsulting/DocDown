namespace DocDown.Core;

/// <summary>
///     The exception raised when the scratch output folder cannot be prepared and is refused.
/// </summary>
/// <remarks>
///     A dedicated exception type lets the engine catch scratch-folder refusals specifically and
///     convert them into a prose extraction failure, rather than surfacing an opaque
///     <see cref="System.IO.IOException"/> or <see cref="System.IO.PathTooLongException"/>. The
///     <see cref="Reason"/> property carries a short, stable cause the engine can present to the
///     caller. This type is immutable after construction and therefore thread-safe.
/// </remarks>
public sealed class ScratchFolderException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class.
    /// </summary>
    /// <remarks>
    ///     Provided to satisfy the standard exception constructor pattern; <see cref="Reason"/> is
    ///     an empty string because no cause was supplied.
    /// </remarks>
    public ScratchFolderException()
        : base()
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    ///     Part of the standard exception constructor pattern; <see cref="Reason"/> is an empty
    ///     string because no distinct short cause was supplied.
    /// </remarks>
    public ScratchFolderException(string message)
        : base(message)
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    /// <remarks>
    ///     Part of the standard exception constructor pattern; used to wrap a lower-level I/O
    ///     error. <see cref="Reason"/> is an empty string because no distinct short cause was
    ///     supplied.
    /// </remarks>
    public ScratchFolderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     short stable reason and a detailed message.
    /// </summary>
    /// <param name="reason">A short, stable cause the engine surfaces to the caller.</param>
    /// <param name="message">The detailed message that describes the error.</param>
    /// <remarks>
    ///     This is the constructor Core uses when refusing a folder, so the refusal always carries
    ///     both a concise <see cref="Reason"/> for programmatic use and a fuller message for
    ///     display. The two <see langword="string"/> parameters are ordered
    ///     (<paramref name="reason"/>, then <paramref name="message"/>) to read naturally at the
    ///     refusal site.
    /// </remarks>
    public ScratchFolderException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    ///     Gets the short, stable reason the scratch folder was refused.
    /// </summary>
    /// <remarks>
    ///     Exposed separately from <see cref="Exception.Message"/> so the engine can map the cause
    ///     into a structured failure without parsing the display message. Never
    ///     <see langword="null"/>; empty when no distinct reason was supplied.
    /// </remarks>
    public string Reason { get; }
}
