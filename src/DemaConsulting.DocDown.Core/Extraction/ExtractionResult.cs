namespace DocDown.Core;

/// <summary>
///     The complete, immutable result of an extraction: its outcome, the paths it produced, the
///     detection and selection decisions, and the honesty record (gaps, diagnostics, ledger).
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
    /// <param name="selectionMode">Whether selection was automatic or a caller override.</param>
    /// <param name="selectionTrace">The full candidate trace explaining the selection.</param>
    /// <param name="failure">The structured failure when the extraction failed, or <see langword="null"/> otherwise.</param>
    /// <param name="diagnostics">The diagnostics emitted during the extraction.</param>
    /// <param name="isComplete"><see langword="true"/> when there are no gaps.</param>
    /// <param name="environment">The environment the extraction ran in.</param>
    /// <param name="gaps">The gaps explaining every absent or partial artifact.</param>
    /// <param name="artifacts">The completeness ledger describing what exists on disk.</param>
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
        SelectionMode selectionMode,
        IReadOnlyList<CandidateVerdict> selectionTrace,
        ExtractionFailure? failure,
        IReadOnlyList<ExtractionDiagnostic> diagnostics,
        bool isComplete,
        ExtractionEnvironment environment,
        IReadOnlyList<ExtractionGap> gaps,
        ArtifactLedger artifacts)
    {
        // Guard the required references so downstream consumers never see nulls in these members
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);
        ArgumentException.ThrowIfNullOrEmpty(summaryPath);
        ArgumentException.ThrowIfNullOrEmpty(manifestPath);
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentNullException.ThrowIfNull(pagePaths);
        ArgumentNullException.ThrowIfNull(partPaths);
        ArgumentNullException.ThrowIfNull(detectedFormat);
        ArgumentNullException.ThrowIfNull(selectionTrace);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(gaps);
        ArgumentNullException.ThrowIfNull(artifacts);

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
        SelectionMode = selectionMode;
        SelectionTrace = selectionTrace;
        Failure = failure;
        Diagnostics = diagnostics;
        IsComplete = isComplete;
        Environment = environment;
        Gaps = gaps;
        Artifacts = artifacts;
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
    /// <remarks><see langword="null"/> when extraction failed before any content could be produced.</remarks>
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

    /// <summary>Gets whether selection was automatic or forced by a caller override.</summary>
    /// <remarks>Recorded so a caller can tell an override apart from an automatic decision.</remarks>
    public SelectionMode SelectionMode { get; }

    /// <summary>Gets the full candidate trace explaining the selection.</summary>
    /// <remarks>Deterministically ordered so the same inputs always yield the same trace.</remarks>
    public IReadOnlyList<CandidateVerdict> SelectionTrace { get; }

    /// <summary>Gets the structured failure, or <see langword="null"/> when the extraction did not fail.</summary>
    /// <remarks>Carries the fixed code and displayable explanation for a failed extraction.</remarks>
    public ExtractionFailure? Failure { get; }

    /// <summary>Gets the diagnostics emitted during the extraction.</summary>
    /// <remarks>An ordered, auditable record of the decisions and degradations of the run.</remarks>
    public IReadOnlyList<ExtractionDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether the extraction produced everything requested.</summary>
    /// <remarks>Equivalent to <c>Gaps.Count == 0</c>; the same value drives the manifest's <c>complete</c> flag.</remarks>
    public bool IsComplete { get; }

    /// <summary>Gets the environment the extraction ran in.</summary>
    /// <remarks>Captured from the runtime plus any facts contributed by backends and candidates.</remarks>
    public ExtractionEnvironment Environment { get; }

    /// <summary>Gets the gaps explaining every absent or partial artifact.</summary>
    /// <remarks>Every partial or absent ledger entry is explained by at least one gap here.</remarks>
    public IReadOnlyList<ExtractionGap> Gaps { get; }

    /// <summary>Gets the completeness ledger describing what exists on disk.</summary>
    /// <remarks>The machine-checkable statement of which artifacts are present, partial, or absent.</remarks>
    public ArtifactLedger Artifacts { get; }
}
