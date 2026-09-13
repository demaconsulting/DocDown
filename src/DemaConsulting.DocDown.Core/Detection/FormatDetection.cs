namespace DocDown.Core;

/// <summary>
///     The result of inspecting a document, pairing the identified <see cref="DocumentFormat"/>
///     with the evidence and confidence behind that identification.
/// </summary>
/// <param name="Format">The identified document format.</param>
/// <param name="Basis">The kind of evidence used to identify the format.</param>
/// <param name="Confidence">
///     A confidence score in the range <c>0.0</c> (no confidence) to <c>1.0</c> (certain).
///     Content signatures score highest; extension-only guesses score lowest.
/// </param>
/// <remarks>
///     Detection outcomes are surfaced to callers and recorded in the manifest so that a
///     low-confidence extension guess is never silently treated as an authoritative match.
///     Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed record FormatDetection(DocumentFormat Format, DetectionBasis Basis, double Confidence)
{
    /// <summary>
    ///     Produces a one-line human-readable explanation of what was detected and how.
    /// </summary>
    /// <returns>
    ///     A string such as <c>pdf (application/pdf) - detected by content signature</c>,
    ///     combining the format display form with a phrase describing the detection basis.
    /// </returns>
    /// <remarks>
    ///     Provided so summaries and logs can present the detection reasoning verbatim without
    ///     each caller re-deriving the basis phrasing. Pure and side-effect free.
    /// </remarks>
    public string Describe() => $"{Format} - detected by {DescribeBasis(Basis)}";

    /// <summary>
    ///     Maps a <see cref="DetectionBasis"/> value to the human-readable phrase used in
    ///     <see cref="Describe"/>.
    /// </summary>
    /// <param name="basis">The detection basis to describe.</param>
    /// <returns>A short lowercase phrase describing the evidence behind the detection.</returns>
    /// <remarks>
    ///     Kept as a private helper so the phrase mapping lives in exactly one place; a
    ///     <see langword="switch"/> expression is used so an unhandled enum value fails fast at
    ///     the default arm rather than producing a silently wrong description.
    /// </remarks>
    private static string DescribeBasis(DetectionBasis basis) => basis switch
    {
        DetectionBasis.ContentSignature => "content signature",
        DetectionBasis.Extension => "file extension",
        DetectionBasis.CallerSpecified => "caller",
        _ => basis.ToString()
    };
}
