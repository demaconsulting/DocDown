namespace DocDown.Core;

/// <summary>
///     Whether an extraction produced the invariant output layout or could not read the document
///     at all.
/// </summary>
/// <remarks>
///     This is the single value a caller branches on, and it is a fact about whether output exists,
///     not a grade of quality. A run that wrote the standard layout is <see cref="Produced"/> even
///     when the inventory reports zero of something or a note records a step DocDown could not
///     complete — those are ordinary, expected outcomes, not failures. Only a document that could
///     not be read (or a scratch folder that was refused, so no layout could be written) is
///     <see cref="Unreadable"/>; its prose failure explains why.
/// </remarks>
public enum ExtractionOutcome
{
    /// <summary>
    ///     The invariant output layout was written. The content is best-effort; the inventory and any
    ///     notes describe what was and was not extracted.
    /// </summary>
    Produced,

    /// <summary>
    ///     No output could be produced: the document could not be read, or the scratch folder was
    ///     refused. See the structured failure for the reason.
    /// </summary>
    Unreadable
}
