namespace DocDown.Core;

/// <summary>
///     The stable diagnostic and failure code constants emitted by Core, defined in one place.
/// </summary>
/// <remarks>
///     Centralizing the codes as named constants prevents magic strings from drifting across
///     the failure model, the diagnostic stream, and the contract verifier, and lets tests and
///     units reference a code by its meaning. This type is a compile-time constant holder with
///     no state, so it is inherently thread-safe. It is <see langword="internal"/> because the
///     codes are an implementation contract shared within the assembly (and visible to tests via
///     <c>InternalsVisibleTo</c>), not part of the public API.
/// </remarks>
internal static class DiagnosticCodes
{
    /// <summary>The extraction produced no text content.</summary>
    internal const string NoTextContent = "DD0101";

    /// <summary>Embedded-image extraction was disabled by caller options.</summary>
    internal const string EmbeddedImagesDisabled = "DD0201";

    /// <summary>The requested <c>renderedPages</c> capability is unavailable in this environment.</summary>
    internal const string RenderedPagesUnavailable = "DD0301";

    /// <summary>Page rendering was requested but the backend produced no pages.</summary>
    internal const string NoPagesProduced = "DD0302";

    /// <summary>Page rendering was requested against a non-paginated format, so it does not apply.</summary>
    /// <remarks>
    ///     Informational, never a gap: a spreadsheet and similar non-paginated formats have no page
    ///     grid to render, so a page request applies to nothing and the run is not degraded by it.
    /// </remarks>
    internal const string PageRenderingNotApplicable = "DD0303";

    /// <summary>The document format could not be recognized (also the matching failure code).</summary>
    internal const string FormatNotRecognized = "DD0401";

    /// <summary>No registered extractor supports the detected format.</summary>
    internal const string NoExtractorForFormat = "DD0402";

    /// <summary>Extractors exist for the format but none are available in this environment.</summary>
    internal const string NoAvailableExtractor = "DD0403";

    /// <summary>No available extractor can satisfy the required capabilities.</summary>
    internal const string RequiredCapabilitiesUnavailable = "DD0404";

    /// <summary>The caller-requested extractor does not apply to the detected format.</summary>
    internal const string RequestedExtractorNotApplicable = "DD0405";

    /// <summary>The caller-requested extractor is unavailable in this environment.</summary>
    internal const string RequestedExtractorUnavailable = "DD0406";

    /// <summary>The scratch folder was refused (also the matching failure code).</summary>
    internal const string ScratchFolderRefused = "DD0501";

    /// <summary>The source document could not be read.</summary>
    internal const string SourceUnreadable = "DD0502";

    /// <summary>A candidate was excluded because it is unavailable in this environment.</summary>
    internal const string CandidateUnavailable = "DD0601";

    /// <summary>An availability probe threw; the candidate is treated as unavailable.</summary>
    internal const string AvailabilityProbeFailed = "DD0602";

    /// <summary>An absence went unexplained and Core synthesized a gap.</summary>
    internal const string UnexplainedAbsence = "DD0701";

    /// <summary>The selected backend lacks a requested capability; the run is degraded.</summary>
    internal const string DegradedMissingCapability = "DD0702";

    /// <summary>The selected extractor failed (also the matching failure code).</summary>
    internal const string ExtractorFailed = "DD0703";

    /// <summary><c>manifest.json</c> is missing.</summary>
    internal const string ManifestMissing = "DD0710";

    /// <summary><c>manifest.json</c> could not be parsed.</summary>
    internal const string ManifestUnparsable = "DD0711";

    /// <summary>The manifest declares an unsupported <c>schemaVersion</c>.</summary>
    internal const string UnsupportedSchemaVersion = "DD0712";

    /// <summary>A mandatory manifest field is missing.</summary>
    internal const string MandatoryFieldMissing = "DD0713";

    /// <summary><c>summary.txt</c> is missing.</summary>
    internal const string SummaryMissing = "DD0714";

    /// <summary>A resource listed in the manifest is missing on disk.</summary>
    internal const string ListedResourceMissing = "DD0715";

    /// <summary>A listed resource has a size or SHA-256 mismatch against disk.</summary>
    internal const string ResourceHashMismatch = "DD0716";

    /// <summary>A file exists on disk that is not listed in the manifest.</summary>
    internal const string UnlistedFile = "DD0717";

    /// <summary>A ledger status or count contradicts the filesystem.</summary>
    internal const string LedgerContradiction = "DD0718";

    /// <summary>A <c>Partial</c> or <c>Absent</c> artifact has no matching gap.</summary>
    internal const string UnexplainedAbsenceViolation = "DD0719";

    /// <summary>A gap was reported with an empty reason.</summary>
    internal const string GapEmptyReason = "DD0720";

    /// <summary><c>summary.txt</c> does not name the backend the manifest says ran.</summary>
    internal const string BackendMismatch = "DD0721";

    /// <summary>The <c>complete</c> flag does not equal <c>gaps.length == 0</c>.</summary>
    internal const string CompleteFlagMismatch = "DD0722";

    /// <summary>
    ///     Part of the folder could not be inspected, so verification of that part did not happen.
    /// </summary>
    /// <remarks>
    ///     This code means <em>"I could not check"</em>, never <em>"I checked and it was fine"</em>.
    ///     It is raised when a directory read fails, because a blocked read tells the verifier
    ///     nothing about what the directory contains; treating it as an empty directory would
    ///     silently convert an unknown into a clean result. A run carrying this code has an
    ///     incomplete verification, which is distinct from both a passing verification and an
    ///     affirmative finding such as <see cref="UnlistedFile"/>.
    /// </remarks>
    internal const string VerificationIncomplete = "DD0723";
}
