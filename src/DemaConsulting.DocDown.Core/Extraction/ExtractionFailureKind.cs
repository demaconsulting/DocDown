namespace DocDown.Core;

/// <summary>
///     Enumerates the distinct reasons an extraction can fail before or during backend execution.
/// </summary>
/// <remarks>
///     A precise failure taxonomy lets callers respond appropriately — a missing backend is a
///     deployment problem, an unreadable source is an input problem, and each maps to a fixed
///     diagnostic code so automated consumers can branch without parsing prose.
/// </remarks>
public enum ExtractionFailureKind
{
    /// <summary>
    ///     The document format could not be recognized.
    /// </summary>
    FormatNotRecognized,

    /// <summary>
    ///     No registered extractor supports the detected format.
    /// </summary>
    NoExtractorForFormat,

    /// <summary>
    ///     Extractors exist for the format but none are available in this environment.
    /// </summary>
    NoAvailableExtractor,

    /// <summary>
    ///     No available extractor can satisfy the caller's required capabilities.
    /// </summary>
    RequiredCapabilitiesUnavailable,

    /// <summary>
    ///     The caller-requested extractor does not apply to the detected format.
    /// </summary>
    RequestedExtractorNotApplicable,

    /// <summary>
    ///     The caller-requested extractor is unavailable in this environment.
    /// </summary>
    RequestedExtractorUnavailable,

    /// <summary>
    ///     The source document could not be opened or read.
    /// </summary>
    SourceUnreadable,

    /// <summary>
    ///     The scratch folder could not be prepared and was refused.
    /// </summary>
    ScratchFolderRefused,

    /// <summary>
    ///     The selected extractor threw while extracting.
    /// </summary>
    ExtractorFailed
}
