namespace DocDown.Core;

/// <summary>
///     The root data-transfer object for <c>manifest.json</c>: the machine-readable twin of the
///     human-readable summary.
/// </summary>
/// <param name="SchemaVersion">The manifest schema version (for example <c>1.0</c>).</param>
/// <param name="Tool">Identifies the tool and package that produced the manifest.</param>
/// <param name="ScratchFolder">The absolute scratch folder the extraction wrote to.</param>
/// <param name="ExtractedAtUtc">The extraction timestamp in ISO-8601 UTC form.</param>
/// <param name="Status">The overall status as a camelCase string (for example <c>degraded</c>).</param>
/// <param name="Complete"><see langword="true"/> when there are no gaps.</param>
/// <param name="Source">Provenance for the source document.</param>
/// <param name="Extractor">The selected extractor, or <see langword="null"/> when none was selected.</param>
/// <param name="Selection">The selection decision and candidate trace.</param>
/// <param name="Environment">The environment the extraction ran in.</param>
/// <param name="Document">Document-level metadata.</param>
/// <param name="ContentFeatures">
///     The counted structural features of the extracted content (headings, tables, comments, and
///     the like) — the machine-readable twin of the summary's content outline. Empty when the
///     backend reported none.
/// </param>
/// <param name="Artifacts">The completeness ledger.</param>
/// <param name="Images">The extracted images.</param>
/// <param name="Pages">The rendered pages.</param>
/// <param name="Parts">The split content parts.</param>
/// <param name="Gaps">The gaps explaining every absent or partial artifact.</param>
/// <param name="Diagnostics">The diagnostics emitted during the extraction.</param>
/// <param name="RequestedOptions">The options the extraction was requested with.</param>
/// <param name="Failure">The structured failure, or <see langword="null"/> when the extraction did not fail.</param>
/// <remarks>
///     <para>
///         Every member of this graph is a <see langword="string"/>, a numeric type, a
///         <see langword="bool"/>, a nullable of those, or an <see cref="IReadOnlyList{T}"/> of
///         those or of another manifest record. No enums appear anywhere: <c>ManifestWriter</c>
///         maps each domain enum to its camelCase string before populating a DTO. This keeps
///         serialization free of custom converters and therefore trimming/AOT/single-file safe,
///         which the library requires.
///     </para>
///     <para>
///         The DTOs are serialized with the source-generated <see cref="DocDownJsonContext"/>.
///         All records are immutable (positional, init-only) and thread-safe.
///     </para>
/// </remarks>
public sealed record ExtractionManifest(
    string SchemaVersion,
    ManifestTool Tool,
    string ScratchFolder,
    string ExtractedAtUtc,
    string Status,
    bool Complete,
    ManifestSource Source,
    ManifestExtractor? Extractor,
    ManifestSelection Selection,
    ManifestEnvironment Environment,
    ManifestDocument Document,
    IReadOnlyList<ManifestContentFeature> ContentFeatures,
    ManifestArtifacts Artifacts,
    IReadOnlyList<ManifestImage> Images,
    IReadOnlyList<ManifestPage> Pages,
    IReadOnlyList<ManifestPart> Parts,
    IReadOnlyList<ManifestGap> Gaps,
    IReadOnlyList<ManifestDiagnostic> Diagnostics,
    ManifestOptions RequestedOptions,
    ManifestFailure? Failure);

/// <summary>
///     Identifies the tool and package that produced a manifest.
/// </summary>
/// <param name="Name">The tool name (for example <c>DocDown</c>).</param>
/// <param name="Package">The producing package (for example <c>DemaConsulting.DocDown.Core</c>).</param>
/// <remarks>
///     Recorded so a consumer or the contract verifier can confirm the manifest was produced by
///     DocDown before trusting its structure. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestTool(string Name, string Package);

/// <summary>
///     Provenance for the source document a manifest describes.
/// </summary>
/// <param name="Path">The source file path, or <see langword="null"/> for a stream source.</param>
/// <param name="FileName">The source file name.</param>
/// <param name="SizeBytes">The source size in bytes, or <see langword="null"/> when unknown.</param>
/// <param name="Sha256">The SHA-256 of the source bytes, or <see langword="null"/> when not computed.</param>
/// <param name="Format">The detected format identifier (for example <c>pdf</c>).</param>
/// <param name="MediaType">The detected media type (for example <c>application/pdf</c>).</param>
/// <param name="DetectionBasis">The detection basis as a camelCase string (for example <c>contentSignature</c>).</param>
/// <remarks>
///     Captures enough to identify and re-locate the input, and to verify integrity via the
///     hash. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestSource(
    string? Path,
    string FileName,
    long? SizeBytes,
    string? Sha256,
    string Format,
    string MediaType,
    string DetectionBasis);

/// <summary>
///     The selected extractor as recorded in a manifest.
/// </summary>
/// <param name="Id">The extractor's stable identifier.</param>
/// <param name="DisplayName">The extractor's human-readable name.</param>
/// <param name="Package">The package that provides the extractor, or <see langword="null"/> when unknown.</param>
/// <param name="Capabilities">The extractor's effective capabilities as camelCase strings.</param>
/// <param name="Priority">The extractor's ranking priority.</param>
/// <param name="Fidelity">A short fidelity descriptor (for example <c>bestEffort</c>).</param>
/// <remarks>
///     Records which backend actually ran and what it could do here, so a reader can attribute
///     the output and understand its fidelity. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestExtractor(
    string Id,
    string DisplayName,
    string? Package,
    IReadOnlyList<string> Capabilities,
    int Priority,
    string Fidelity);

/// <summary>
///     The selection decision and candidate trace as recorded in a manifest.
/// </summary>
/// <param name="Mode">The selection mode as a camelCase string (for example <c>automatic</c>).</param>
/// <param name="RequiredCapabilities">The required capabilities as camelCase strings.</param>
/// <param name="SatisfiedCapabilities">The satisfied capabilities as camelCase strings.</param>
/// <param name="Candidates">The verdict for each candidate considered.</param>
/// <remarks>
///     Preserves the full, deterministic selection reasoning so the manifest is a complete audit
///     of why the chosen backend won. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestSelection(
    string Mode,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<string> SatisfiedCapabilities,
    IReadOnlyList<ManifestCandidate> Candidates);

/// <summary>
///     A single candidate verdict as recorded in a manifest.
/// </summary>
/// <param name="ExtractorId">The candidate extractor's identifier.</param>
/// <param name="DisplayName">The candidate extractor's human-readable name.</param>
/// <param name="Priority">The candidate's ranking priority.</param>
/// <param name="Outcome">The candidate outcome as a camelCase string (for example <c>selected</c>).</param>
/// <param name="Detail">A human-readable explanation of the outcome.</param>
/// <remarks>
///     Shared by both the selection trace and a failure's candidate list so a manifest never
///     loses per-candidate reasoning. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestCandidate(
    string ExtractorId,
    string DisplayName,
    int Priority,
    string Outcome,
    string Detail);

/// <summary>
///     The environment description as recorded in a manifest.
/// </summary>
/// <param name="OperatingSystem">The operating system description.</param>
/// <param name="ProcessArchitecture">The process architecture.</param>
/// <param name="RuntimeVersion">The runtime/framework description.</param>
/// <param name="RuntimeIdentifier">The runtime identifier (RID).</param>
/// <param name="Facts">The contributed environment facts in emission order.</param>
/// <remarks>
///     Makes a degraded result reproducible by recording the platform context that governed
///     capability availability. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestEnvironment(
    string OperatingSystem,
    string ProcessArchitecture,
    string RuntimeVersion,
    string RuntimeIdentifier,
    IReadOnlyList<ManifestEnvironmentFact> Facts);

/// <summary>
///     A single environment fact as recorded in a manifest.
/// </summary>
/// <param name="Source">The contributing component that reported the fact.</param>
/// <param name="Key">The fact key.</param>
/// <param name="Value">The fact value or description.</param>
/// <param name="Available">The tri-state availability flag, or <see langword="null"/> when not applicable.</param>
/// <remarks>
///     Explains why a capability was or was not available in this environment, attributed to the
///     component that reported it. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestEnvironmentFact(string Source, string Key, string Value, bool? Available);

/// <summary>
///     Document-level metadata as recorded in a manifest.
/// </summary>
/// <param name="Title">The document title, or <see langword="null"/> when unknown.</param>
/// <param name="Author">The document author, or <see langword="null"/> when unknown.</param>
/// <param name="PageCount">The page count, or <see langword="null"/> when not applicable.</param>
/// <param name="PartCount">The part count, or <see langword="null"/> when not applicable.</param>
/// <remarks>
///     Nullable throughout because unknown metadata is reported as absent rather than guessed.
///     Immutable and thread-safe.
/// </remarks>
public sealed record ManifestDocument(string? Title, string? Author, int? PageCount, int? PartCount);

/// <summary>
///     One counted structural feature of the extracted content, as recorded in a manifest.
/// </summary>
/// <param name="Label">The plural noun phrase naming the feature (for example <c>comments</c>).</param>
/// <param name="Count">How many of them the extracted content carries; always greater than zero.</param>
/// <remarks>
///     The machine-readable twin of one line of the summary's content outline, so a consumer that
///     reads only the manifest learns the same structure — including, for example, that a draft
///     carries dozens of author-attributed reviewer comments — without parsing the markdown.
///     Immutable and thread-safe.
/// </remarks>
public sealed record ManifestContentFeature(string Label, int Count);

/// <summary>
///     The completeness ledger as recorded in a manifest.
/// </summary>
/// <param name="Summary">The ledger entry for <c>summary.txt</c>.</param>
/// <param name="Manifest">The ledger entry for <c>manifest.json</c>.</param>
/// <param name="Metadata">The ledger entry for <c>metadata.json</c>.</param>
/// <param name="Content">The ledger entry for <c>content.md</c>.</param>
/// <param name="Images">The ledger entry for the <c>images/</c> folder.</param>
/// <param name="Pages">The ledger entry for the <c>pages/</c> folder.</param>
/// <remarks>
///     The serialized form of the completeness ledger the contract verifier reconciles against
///     the filesystem. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestArtifacts(
    ManifestArtifactEntry Summary,
    ManifestArtifactEntry Manifest,
    ManifestArtifactEntry Metadata,
    ManifestArtifactEntry Content,
    ManifestArtifactEntry Images,
    ManifestArtifactEntry Pages);

/// <summary>
///     A single ledger row as recorded in a manifest.
/// </summary>
/// <param name="Path">The relative path or folder the entry describes.</param>
/// <param name="Status">The presence status as a camelCase string (for example <c>partial</c>).</param>
/// <param name="Obtained">The obtained count, or <see langword="null"/> when not meaningful.</param>
/// <param name="Found">The found count, or <see langword="null"/> when not meaningful.</param>
/// <remarks>
///     Mirrors <see cref="ArtifactEntry"/> with the status projected to a string. Immutable and
///     thread-safe.
/// </remarks>
public sealed record ManifestArtifactEntry(string Path, string Status, int? Obtained, int? Found);

/// <summary>
///     An extracted image as recorded in a manifest.
/// </summary>
/// <param name="Path">The relative path to the image.</param>
/// <param name="MediaType">The image media type.</param>
/// <param name="WidthPx">The image width in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="HeightPx">The image height in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="SizeBytes">The image size in bytes.</param>
/// <param name="Sha256">The SHA-256 of the image bytes.</param>
/// <param name="SourcePage">
///     The first (lowest) 1-based referrer, or <see langword="null"/> when none; a convenience alias
///     for the first entry of <paramref name="SourcePages"/>, kept for consumers reading only the scalar.
/// </param>
/// <param name="SourcePages">
///     Every 1-based page, slide, or worksheet that references the image, sorted and distinct. Empty
///     when no page-level referrer is derivable. The authoritative multi-referrer record.
/// </param>
/// <param name="ReferencedByTemplate">
///     <see langword="true"/> when a template container (a PowerPoint layout or master, or a Visio
///     master) references the image; lets a reader tell a template-borne asset from a true orphan
///     (both have an empty <paramref name="SourcePages"/>, but only an orphan has this
///     <see langword="false"/>).
/// </param>
/// <param name="SourceRef">A backend-specific source reference, or <see langword="null"/> when none.</param>
/// <param name="Transform">
///     The image transform as a camelCase string (for example <c>passthrough</c>), projected from
///     <see cref="ImageTransform"/>.
/// </param>
/// <param name="References">
///     How many times the image is referenced; deduplication stores identical bytes once and
///     increments this count.
/// </param>
/// <param name="Description">
///     A human-meaningful description of the image (for example authored alt text or a caption), or
///     <see langword="null"/> when the document offered none.
/// </param>
/// <param name="DescriptionSource">
///     The provenance of <paramref name="Description"/> as a camelCase string (for example
///     <c>description</c>, <c>caption</c>, or <c>heading</c>), or <see langword="null"/> when there
///     is no description.
/// </param>
/// <remarks>
///     Records each stored image once with provenance and a reference count, so a reader can see
///     reuse without duplicate files. When present, the description is recorded together with its
///     source so a reader can tell an authored description apart from a mere contextual hint.
///     Immutable and thread-safe.
/// </remarks>
public sealed record ManifestImage(
    string Path,
    string MediaType,
    int? WidthPx,
    int? HeightPx,
    long SizeBytes,
    string Sha256,
    int? SourcePage,
    IReadOnlyList<int> SourcePages,
    bool ReferencedByTemplate,
    string? SourceRef,
    string Transform,
    int References,
    string? Description,
    string? DescriptionSource);

/// <summary>
///     A rendered page as recorded in a manifest.
/// </summary>
/// <param name="Path">The relative path to the rendered page image.</param>
/// <param name="PageNumber">The 1-based document page number.</param>
/// <param name="SizeBytes">The page image size in bytes.</param>
/// <param name="Sha256">The SHA-256 of the page image bytes.</param>
/// <remarks>
///     Maps each rendered page file back to its source page number and records its integrity
///     hash. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestPage(string Path, int PageNumber, long SizeBytes, string Sha256);

/// <summary>
///     A split content part as recorded in a manifest.
/// </summary>
/// <param name="Path">The relative path to the part file.</param>
/// <param name="Kind">The part kind as a camelCase string (for example <c>sheet</c>).</param>
/// <param name="Ordinal">The Core-assigned, dense 1-based ordinal of the part.</param>
/// <param name="Title">The part title, or <see langword="null"/> when none.</param>
/// <param name="CharacterCount">The character count of the part, or <see langword="null"/> when not counted.</param>
/// <remarks>
///     Lists each part file with its stable ordinal so the split content can be reassembled or
///     navigated in order. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestPart(string Path, string Kind, int Ordinal, string? Title, int? CharacterCount);

/// <summary>
///     A gap as recorded in a manifest.
/// </summary>
/// <param name="Id">The gap identifier (for example <c>GAP-1</c>).</param>
/// <param name="Kind">The gap kind as a camelCase string (for example <c>pages</c>).</param>
/// <param name="Target">The artifact or path the gap applies to.</param>
/// <param name="Scope">The gap scope as a camelCase string (for example <c>unavailable</c>).</param>
/// <param name="Reason">The non-empty explanation of the gap.</param>
/// <param name="Impact">The consequence for the consumer, or <see langword="null"/> when none.</param>
/// <param name="Remedy">A suggested remedy, or <see langword="null"/> when none.</param>
/// <param name="AffectedCount">The number of affected items, or <see langword="null"/> when not counted.</param>
/// <param name="AffectedItems">The specific affected items, or <see langword="null"/> when not enumerated.</param>
/// <remarks>
///     The serialized form of <see cref="ExtractionGap"/>; every partial or absent ledger entry
///     is explained by at least one of these. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestGap(
    string Id,
    string Kind,
    string Target,
    string Scope,
    string Reason,
    string? Impact,
    string? Remedy,
    int? AffectedCount,
    IReadOnlyList<string>? AffectedItems);

/// <summary>
///     A diagnostic as recorded in a manifest.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">The severity as a camelCase string (for example <c>warning</c>).</param>
/// <param name="Message">The human-readable diagnostic message.</param>
/// <param name="Location">The location the diagnostic refers to, or <see langword="null"/> for the whole document.</param>
/// <remarks>
///     Mirrors <see cref="ExtractionDiagnostic"/> with the severity projected to a string.
///     Immutable and thread-safe.
/// </remarks>
public sealed record ManifestDiagnostic(string Code, string Severity, string Message, string? Location);

/// <summary>
///     The requested options as recorded in a manifest.
/// </summary>
/// <param name="RenderPages">Whether page rendering was requested.</param>
/// <param name="Pages">The requested page range as a string (for example <c>1-12</c>), or <see langword="null"/> for all pages.</param>
/// <param name="IncludeEmbeddedImages">Whether embedded-image extraction was requested.</param>
/// <param name="ImageOutput">The image output mode as a camelCase string (for example <c>preserve</c>).</param>
/// <param name="MaxImageDimensionPx">The maximum image dimension in pixels, or <see langword="null"/> for no limit.</param>
/// <param name="MaxImageBytes">The maximum image size in bytes, or <see langword="null"/> for no limit.</param>
/// <param name="PageRenderDpi">The page render DPI.</param>
/// <param name="ContentSplit">The content split mode as a camelCase string (for example <c>auto</c>).</param>
/// <param name="ScratchFolder">The scratch-folder mode as a camelCase string (for example <c>cleanIfDocDownFolder</c>).</param>
/// <param name="PreferredExtractorId">The forced extractor id, or <see langword="null"/> for automatic selection.</param>
/// <param name="RequireCapabilities">The required capabilities as camelCase strings, or <see langword="null"/> when none.</param>
/// <remarks>
///     Records exactly what was asked for so the manifest is self-describing and a run can be
///     reproduced. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestOptions(
    bool RenderPages,
    string? Pages,
    bool IncludeEmbeddedImages,
    string ImageOutput,
    int? MaxImageDimensionPx,
    long? MaxImageBytes,
    int PageRenderDpi,
    string ContentSplit,
    string ScratchFolder,
    string? PreferredExtractorId,
    IReadOnlyList<string>? RequireCapabilities);

/// <summary>
///     A structured failure as recorded in a manifest.
/// </summary>
/// <param name="Kind">The failure kind as a camelCase string (for example <c>formatNotRecognized</c>).</param>
/// <param name="Code">The fixed failure code (for example <c>DD0401</c>).</param>
/// <param name="Summary">The one-line failure headline.</param>
/// <param name="Explanation">The multi-line, displayable explanation.</param>
/// <param name="Remedy">A suggested remedy, or <see langword="null"/> when none.</param>
/// <param name="Candidates">The candidate verdicts that led to the failure.</param>
/// <remarks>
///     Present only when the extraction failed; mirrors <see cref="ExtractionFailure"/> with the
///     kind projected to a string. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestFailure(
    string Kind,
    string Code,
    string Summary,
    string Explanation,
    string? Remedy,
    IReadOnlyList<ManifestCandidate> Candidates);
