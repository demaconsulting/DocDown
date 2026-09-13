namespace DocDown.Core;

/// <summary>
///     Describes how a document format was determined, so consumers can weigh how much to
///     trust the detected format.
/// </summary>
/// <remarks>
///     The basis is reported alongside every detection because different evidence carries
///     different weight: the file-name extension is the primary signal, and a content signature
///     is the fallback used when the name yields nothing. Recording the basis lets downstream
///     code and human reviewers reason about how the format was determined without re-running
///     the sniffer.
/// </remarks>
public enum DetectionBasis
{
    /// <summary>
    ///     The format was identified from a magic-number or signature in the document bytes.
    /// </summary>
    ContentSignature,

    /// <summary>
    ///     The format was identified from the file name extension, the primary detection signal.
    /// </summary>
    Extension,

    /// <summary>
    ///     The format was supplied by the caller rather than inspected from the content.
    /// </summary>
    CallerSpecified
}
