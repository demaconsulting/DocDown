namespace DocDown.Core;

/// <summary>
///     The complete, immutable result of an extraction: its outcome, the paths it produced, the
///     detection and selection decisions, and any notes about steps that could not be completed.
/// </summary>
/// <remarks>
///     Returned by the engine instead of throwing so a caller always receives the full picture,
///     including on failure. The constructor is <see langword="internal"/> because only the
///     engine assembles a result once the pipeline has finished; consumers treat it as read-only.
///     Instances are immutable and thread-safe.
/// </remarks>
public sealed class ExtractionResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtractionResult"/> class.
    /// </summary>
    /// <param name="outcome">The overall outcome of the extraction.</param>
    /// <param name="scratchFolder">The absolute scratch folder the output was (or would have been) written to.</param>
    /// <param name="summaryPath">The path to <c>summary.txt</c>.</param>
    /// <param name="manifestPath">The path to <c>manifest.json</c>.</param>
    /// <param name="contentPath">The path to <c>content.md</c>, or <see langword="null"/> when none was written.</param>
    /// <param name="imagePaths">The relative paths of extracted images.</param>
    /// <param name="pagePaths">The relative paths of rendered pages.</param>
    /// <param name="partPaths">The relative paths of split content parts.</param>
    /// <param name="detectedFormat">The detected format for the source document.</param>
    /// <param name="selectedExtractor">The selected extractor descriptor, or <see langword="null"/> on selection failure.</param>
    /// <param name="failure">The structured failure when the extraction was unreadable, or <see langword="null"/> otherwise.</param>
    /// <param name="environment">The environment the extraction ran in.</param>
    /// <param name="notes">The notes recording any step DocDown attempted but could not complete.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when any non-nullable reference argument is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Validates the required reference arguments so a result can never be observed in a
    ///     partially populated state.
    /// </remarks>
    internal ExtractionResult(
        ExtractionOutcome outcome,
        string scratchFolder,
        string summaryPath,
        string manifestPath,
        string? contentPath,
        IReadOnlyList<string> imagePaths,
        IReadOnlyList<string> pagePaths,
        IReadOnlyList<string> partPaths,
        FormatDetection detectedFormat,
        ExtractorDescriptor? selectedExtractor,
        ExtractionFailure? failure,
        ExtractionEnvironment environment,
        IReadOnlyList<ExtractionNote> notes)
    {
        // Guard the required references so downstream consumers never see nulls in these members
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);
        ArgumentException.ThrowIfNullOrEmpty(summaryPath);
        ArgumentException.ThrowIfNullOrEmpty(manifestPath);
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentNullException.ThrowIfNull(pagePaths);
        ArgumentNullException.ThrowIfNull(partPaths);
        ArgumentNullException.ThrowIfNull(detectedFormat);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(notes);

        Outcome = outcome;
        ScratchFolder = scratchFolder;
        SummaryPath = summaryPath;
        ManifestPath = manifestPath;
        ContentPath = contentPath;
        ImagePaths = imagePaths;
        PagePaths = pagePaths;
        PartPaths = partPaths;
        DetectedFormat = detectedFormat;
        SelectedExtractor = selectedExtractor;
        Failure = failure;
        Environment = environment;
        Notes = notes;
    }

    /// <summary>Gets the overall outcome of the extraction.</summary>
    /// <remarks>The single value a caller can branch on before inspecting detail.</remarks>
    public ExtractionOutcome Outcome { get; }

    /// <summary>Gets the absolute scratch folder the output was written to.</summary>
    /// <remarks>Set even on a scratch-folder refusal, in which case it is the requested absolute path.</remarks>
    public string ScratchFolder { get; }

    /// <summary>Gets the path to <c>summary.txt</c>.</summary>
    /// <remarks>Always populated; on a scratch refusal it is the path the summary would have used.</remarks>
    public string SummaryPath { get; }

    /// <summary>Gets the path to <c>manifest.json</c>.</summary>
    /// <remarks>Always populated; on a scratch refusal it is the path the manifest would have used.</remarks>
    public string ManifestPath { get; }

    /// <summary>Gets the path to <c>content.md</c>, or <see langword="null"/> when none was written.</summary>
    /// <remarks><see langword="null"/> when extraction could not produce any content.</remarks>
    public string? ContentPath { get; }

    /// <summary>Gets the relative paths of extracted images.</summary>
    /// <remarks>Forward-slash separated and relative to the scratch folder; empty when no images were written.</remarks>
    public IReadOnlyList<string> ImagePaths { get; }

    /// <summary>Gets the relative paths of rendered pages.</summary>
    /// <remarks>Forward-slash separated and relative to the scratch folder; empty when no pages were rendered.</remarks>
    public IReadOnlyList<string> PagePaths { get; }

    /// <summary>Gets the relative paths of split content parts.</summary>
    /// <remarks>Forward-slash separated and relative to the scratch folder; empty when content was not split.</remarks>
    public IReadOnlyList<string> PartPaths { get; }

    /// <summary>Gets the detected format of the source document.</summary>
    /// <remarks>Present even on failure so a caller can see what was (or was not) recognized.</remarks>
    public FormatDetection DetectedFormat { get; }

    /// <summary>Gets the selected extractor descriptor, or <see langword="null"/> on selection failure.</summary>
    /// <remarks><see langword="null"/> when no extractor could be selected.</remarks>
    public ExtractorDescriptor? SelectedExtractor { get; }

    /// <summary>Gets the structured failure, or <see langword="null"/> when the extraction produced output.</summary>
    /// <remarks>Carries the displayable prose for an unreadable extraction.</remarks>
    public ExtractionFailure? Failure { get; }

    /// <summary>Gets the environment the extraction ran in.</summary>
    /// <remarks>Captured from the runtime plus any facts contributed by backends and candidates.</remarks>
    public ExtractionEnvironment Environment { get; }

    /// <summary>Gets the notes recording any step DocDown attempted but could not complete.</summary>
    /// <remarks>
    ///     Each note is a single plain-language fact about the extraction (for example that page
    ///     rendering was requested but no renderer was available). Empty when nothing was left
    ///     incomplete. Absences of content the document simply does not contain are described by the
    ///     inventory, not here.
    /// </remarks>
    public IReadOnlyList<ExtractionNote> Notes { get; }
}
