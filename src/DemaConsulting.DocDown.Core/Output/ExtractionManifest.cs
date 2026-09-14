namespace DocDown.Core;

/// <summary>
///     The root data-transfer object for <c>manifest.json</c>: the machine-readable twin of the
///     human-readable summary.
/// </summary>
/// <param name="SchemaVersion">The manifest schema version (for example <c>3.0</c>).</param>
/// <param name="Tool">Identifies the tool and package that produced the manifest.</param>
/// <param name="ScratchFolder">The absolute scratch folder the extraction wrote to.</param>
/// <param name="ExtractedAtUtc">The extraction timestamp in ISO-8601 UTC form.</param>
/// <param name="Status">The overall status as a camelCase string (<c>produced</c> or <c>unreadable</c>).</param>
/// <param name="Source">Provenance for the source document.</param>
/// <param name="Extractor">The selected extractor, or <see langword="null"/> when none was selected.</param>
/// <param name="Document">Document-level metadata.</param>
/// <param name="ContentFeatures">
///     The counted structural features of the extracted content (headings, tables, comments, and
///     the like) — the machine-readable twin of the summary's content outline. Empty when the
///     backend reported none.
/// </param>
/// <param name="Images">The extracted images.</param>
/// <param name="Pages">The rendered pages.</param>
/// <param name="Parts">The split content parts.</param>
/// <param name="Notes">
///     Plain-language notes for any step DocDown attempted but could not complete. Empty when
///     nothing was left incomplete.
/// </param>
/// <param name="Failure">The structured failure, or <see langword="null"/> when the extraction produced output.</param>
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
    ManifestSource Source,
    ManifestExtractor? Extractor,
    ManifestDocument Document,
    IReadOnlyList<ManifestContentFeature> ContentFeatures,
    IReadOnlyList<ManifestImage> Images,
    IReadOnlyList<ManifestPage> Pages,
    IReadOnlyList<ManifestPart> Parts,
    IReadOnlyList<string> Notes,
    ManifestFailure? Failure);

/// <summary>
///     Identifies the tool and package that produced a manifest.
/// </summary>
/// <param name="Name">The tool name (for example <c>DocDown</c>).</param>
/// <param name="Package">The producing package (for example <c>DemaConsulting.DocDown.Core</c>).</param>
/// <remarks>
///     Recorded so a consumer can confirm the manifest was produced by DocDown before trusting its
///     structure. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestTool(string Name, string Package);

/// <summary>
///     Provenance for the source document a manifest describes.
/// </summary>
/// <param name="Path">The source file path, or <see langword="null"/> for a stream source.</param>
/// <param name="FileName">The source file name.</param>
/// <param name="SizeBytes">The source size in bytes, or <see langword="null"/> when unknown.</param>
/// <param name="Format">The detected format identifier (for example <c>pdf</c>).</param>
/// <param name="MediaType">The detected media type (for example <c>application/pdf</c>).</param>
/// <param name="DetectionBasis">The detection basis as a camelCase string (for example <c>contentSignature</c>).</param>
/// <remarks>
///     Captures enough to identify and re-locate the input. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestSource(
    string? Path,
    string FileName,
    long? SizeBytes,
    string Format,
    string MediaType,
    string DetectionBasis);

/// <summary>
///     The selected extractor as recorded in a manifest.
/// </summary>
/// <param name="Id">The extractor's stable identifier.</param>
/// <param name="DisplayName">The extractor's human-readable name.</param>
/// <param name="Package">The package that provides the extractor, or <see langword="null"/> when unknown.</param>
/// <param name="Priority">The extractor's ranking priority.</param>
/// <remarks>
///     Records which backend actually ran, so a reader can attribute the output. Immutable and
///     thread-safe.
/// </remarks>
public sealed record ManifestExtractor(
    string Id,
    string DisplayName,
    string? Package,
    int Priority);

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
///     An extracted image as recorded in a manifest.
/// </summary>
/// <param name="Path">The relative path to the image.</param>
/// <param name="MediaType">The image media type.</param>
/// <param name="WidthPx">The image width in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="HeightPx">The image height in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="SizeBytes">The image size in bytes.</param>
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
/// <remarks>
///     Maps each rendered page file back to its source page number. Immutable and thread-safe.
/// </remarks>
public sealed record ManifestPage(string Path, int PageNumber, long SizeBytes);

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
///     A plain-language failure as recorded in a manifest.
/// </summary>
/// <param name="Summary">The one-line failure headline.</param>
/// <param name="Explanation">The multi-line, displayable explanation.</param>
/// <remarks>
///     Present only when the extraction was unreadable; mirrors <see cref="ExtractionFailure"/>.
///     Immutable and thread-safe.
/// </remarks>
public sealed record ManifestFailure(string Summary, string Explanation);
