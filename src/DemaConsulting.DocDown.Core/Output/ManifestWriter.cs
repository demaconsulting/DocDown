using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocDown.Core;

/// <summary>
///     Serializes <c>manifest.json</c>, the machine-readable twin of the summary: the invariant
///     layout, the source and extractor provenance, the content inventory, and any notes.
/// </summary>
/// <remarks>
///     <para>
///         Serialization uses the source-generated <see cref="DocDownJsonContext"/> (no reflection,
///         AOT/trim safe); every domain enum is projected to its camelCase string before it reaches a
///         DTO, and the JSON is written with <c>\n</c> line endings and no BOM for byte-identical
///         determinism.
///     </para>
///     <para>
///         The manifest records what is present — the counted content inventory and the image, page,
///         and part lists — plus plain notes for any step DocDown attempted but could not complete.
///         It never grades the document. <see cref="WriteAsync"/> performs filesystem I/O and is
///         stateless and safe to call from the single extraction flow.
///     </para>
/// </remarks>
public static class ManifestWriter
{
    /// <summary>The manifest schema version this writer emits.</summary>
    /// <remarks>
    ///     Constant so the version is stated once; consumers pin it. Raised to <c>3.0</c> when the
    ///     integrity digests, the environment block, and the requested-options echo were removed,
    ///     because a consumer pinned to <c>2.0</c> would otherwise meet a manifest missing fields
    ///     that schema promised.
    /// </remarks>
    private const string SchemaVersion = "3.0";

    /// <summary>The fixed relative name of the manifest file.</summary>
    /// <remarks>Part of the invariant output contract; never varies.</remarks>
    private const string ManifestFileName = "manifest.json";

    /// <summary>
    ///     Serializes the manifest to <c>manifest.json</c> in the scratch folder.
    /// </summary>
    /// <param name="folder">The scratch folder to write into. Must not be null.</param>
    /// <param name="sink">The sink holding the recorded content. Must not be null.</param>
    /// <param name="report">The engine-side facts describing the extraction. Must not be null.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the manifest has been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Builds the DTO graph, mapping every enum to its camelCase string, then serializes through
    ///     the source-generated context and writes with <c>\n</c> endings and no BOM. Performs
    ///     filesystem I/O.
    /// </remarks>
    public static async ValueTask WriteAsync(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
        ContentWriteResult? content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(report);

        // The content-write result is not read directly here, but is accepted so the signature
        // matches the summary writer and future manifest fields can consume it without a change
        _ = content;

        // Assemble the immutable DTO graph from the recorded state and the engine-side facts
        var manifest = BuildManifest(folder, sink, report);

        // Serialize via the source-generated context, then normalize endings and append a trailing newline
        var json = JsonSerializer.Serialize(manifest, DocDownJsonContext.Default.ExtractionManifest);
        var normalized = json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        await folder.WriteTextAsync(ManifestFileName, normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the immutable manifest DTO graph from the recorded state and engine-side facts.
    /// </summary>
    /// <param name="folder">The scratch folder, whose absolute path is recorded.</param>
    /// <param name="sink">The sink holding the recorded content.</param>
    /// <param name="report">The engine-side facts describing the extraction.</param>
    /// <returns>The populated <see cref="ExtractionManifest"/>.</returns>
    /// <remarks>
    ///     Kept separate from serialization so the mapping (including every enum-to-string projection)
    ///     is expressed once and can be reasoned about independently of JSON formatting. Pure and
    ///     side-effect free.
    /// </remarks>
    private static ExtractionManifest BuildManifest(ScratchFolder folder, ExtractionSink sink, ExtractionReport report) => new(
        SchemaVersion,
        new ManifestTool("DocDown", "DemaConsulting.DocDown.Core"),
        folder.AbsolutePath,
        FormatTimestamp(report.TimestampUtc),
        OutcomeString(report.Outcome),
        BuildSource(report),
        BuildExtractor(report.SelectedExtractor, report),
        BuildDocument(sink.DocumentInfo),
        BuildContentFeatures(sink.ContentFeatures),
        BuildImages(sink.Images),
        BuildPages(sink.Pages),
        BuildParts(sink.Parts),
        BuildNotes(sink.Notes),
        BuildFailure(report.Failure));

    /// <summary>Builds the manifest source block from the report.</summary>
    /// <param name="report">The engine-side facts.</param>
    /// <returns>The manifest source DTO.</returns>
    /// <remarks>Records enough to identify and re-locate the input. Pure.</remarks>
    private static ManifestSource BuildSource(ExtractionReport report) => new(
        report.Source.Path,
        report.Source.FileName,
        report.Source.SizeBytes,
        report.DetectedFormat.Format.Id,
        report.DetectedFormat.Format.MediaType,
        BasisString(report.DetectedFormat.Basis));

    /// <summary>Builds the manifest extractor block, or <see langword="null"/> when none was selected.</summary>
    /// <param name="selected">The selected descriptor, or <see langword="null"/>.</param>
    /// <param name="report">The engine-side facts (for the providing package).</param>
    /// <returns>The manifest extractor DTO, or <see langword="null"/>.</returns>
    /// <remarks>The package is unknown to Core and may be null. Pure.</remarks>
    private static ManifestExtractor? BuildExtractor(ExtractorDescriptor? selected, ExtractionReport report)
    {
        // No selected extractor means the failure block, not this block, tells the story
        if (selected is null)
        {
            return null;
        }

        return new ManifestExtractor(selected.Id, selected.DisplayName, report.ExtractorPackage, selected.Priority);
    }

    /// <summary>Builds the manifest document block from reported document info.</summary>
    /// <param name="info">The reported document info, or <see langword="null"/>.</param>
    /// <returns>The manifest document DTO with nulls for unknown metadata.</returns>
    /// <remarks>Unknown metadata is reported as absent rather than guessed. Pure.</remarks>
    private static ManifestDocument BuildDocument(DocumentInfo? info) =>
        new(info?.Title, info?.Author, info?.PageCount, info?.PartCount);

    /// <summary>Builds the manifest content-feature list from the sink's accumulated counts.</summary>
    /// <param name="features">The accumulated features in first-reported order.</param>
    /// <returns>The manifest content-feature DTOs in the same order.</returns>
    /// <remarks>
    ///     Order is preserved rather than sorted because the backend reported the features in the
    ///     order it judged most informative, and the summary renders the same order. Pure.
    /// </remarks>
    private static IReadOnlyList<ManifestContentFeature> BuildContentFeatures(IReadOnlyList<ContentFeature> features)
    {
        var mapped = new List<ManifestContentFeature>(features.Count);
        foreach (var feature in features)
        {
            mapped.Add(new ManifestContentFeature(feature.Label, feature.Count));
        }

        return mapped;
    }

    /// <summary>Builds the manifest image list from the recorded images.</summary>
    /// <param name="images">The recorded images.</param>
    /// <returns>The manifest image DTOs in allocation order.</returns>
    /// <remarks>Each image appears once with its reference count, reflecting deduplication. Pure.</remarks>
    private static IReadOnlyList<ManifestImage> BuildImages(IReadOnlyList<RecordedImage> images)
    {
        // Emit in allocation order so the manifest matches the file names
        var list = new List<ManifestImage>(images.Count);
        foreach (var image in images)
        {
            list.Add(new ManifestImage(
                image.Path, image.MediaType, image.WidthPx, image.HeightPx,
                image.SizeBytes, image.SourcePage, image.SourcePages,
                image.ReferencedByTemplate, image.SourceRef,
                TransformString(image.Transform), image.References,
                image.Description, image.DescriptionSource));
        }

        return list;
    }

    /// <summary>Builds the manifest page list from the recorded pages.</summary>
    /// <param name="pages">The recorded pages.</param>
    /// <returns>The manifest page DTOs in insertion order.</returns>
    /// <remarks>Maps each page back to its document page number. Pure.</remarks>
    private static IReadOnlyList<ManifestPage> BuildPages(IReadOnlyList<RecordedPage> pages)
    {
        // Preserve insertion order; page numbers are the stable identity
        var list = new List<ManifestPage>(pages.Count);
        foreach (var page in pages)
        {
            list.Add(new ManifestPage(page.Path, page.PageNumber, page.SizeBytes));
        }

        return list;
    }

    /// <summary>Builds the manifest part list from the recorded parts.</summary>
    /// <param name="parts">The recorded parts.</param>
    /// <returns>The manifest part DTOs in ordinal order.</returns>
    /// <remarks>Lists each part with its stable ordinal so split content can be reassembled. Pure.</remarks>
    private static IReadOnlyList<ManifestPart> BuildParts(IReadOnlyList<RecordedPart> parts)
    {
        // Emit in call order, which is the dense ordinal order the sink assigned
        var list = new List<ManifestPart>(parts.Count);
        foreach (var part in parts)
        {
            list.Add(new ManifestPart(part.Path, part.Kind, part.Ordinal, part.Title, part.CharacterCount));
        }

        return list;
    }

    /// <summary>Builds the manifest notes list from the recorded notes.</summary>
    /// <param name="notes">The recorded notes in emission order.</param>
    /// <returns>The note messages in emission order.</returns>
    /// <remarks>Each note is a single plain-language message; the manifest carries the message only. Pure.</remarks>
    private static IReadOnlyList<string> BuildNotes(IReadOnlyList<ExtractionNote> notes)
    {
        var list = new List<string>(notes.Count);
        foreach (var note in notes)
        {
            list.Add(note.Message);
        }

        return list;
    }

    /// <summary>Builds the manifest failure block, or <see langword="null"/> when the extraction produced output.</summary>
    /// <param name="failure">The structured failure, or <see langword="null"/>.</param>
    /// <returns>The manifest failure DTO, or <see langword="null"/>.</returns>
    /// <remarks>Carries the verbatim prose; a produced run serializes an explicit null. Pure.</remarks>
    private static ManifestFailure? BuildFailure(ExtractionFailure? failure) =>
        failure is null ? null : new ManifestFailure(failure.Summary, failure.Explanation);

    /// <summary>Formats a timestamp as ISO-8601 UTC with second precision.</summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>The formatted UTC timestamp (for example <c>2026-09-10T16:33:11Z</c>).</returns>
    /// <remarks>Uses the invariant culture so the value is stable across locales. Pure.</remarks>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>Projects an extraction outcome to its camelCase string.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Kept as a switch so an unmapped value fails fast rather than serializing wrongly. Pure.</remarks>
    private static string OutcomeString(ExtractionOutcome outcome) => outcome switch
    {
        ExtractionOutcome.Produced => "produced",
        ExtractionOutcome.Unreadable => "unreadable",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unmapped extraction outcome.")
    };

    /// <summary>Projects a detection basis to its camelCase string.</summary>
    /// <param name="basis">The basis.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string BasisString(DetectionBasis basis) => basis switch
    {
        DetectionBasis.ContentSignature => "contentSignature",
        DetectionBasis.Extension => "extension",
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unmapped detection basis.")
    };

    /// <summary>Projects an image transform to its camelCase string.</summary>
    /// <param name="transform">The image transform reported for a written image.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string TransformString(ImageTransform transform) => transform switch
    {
        ImageTransform.Passthrough => "passthrough",
        ImageTransform.DecodedToPng => "decodedToPng",
        _ => throw new ArgumentOutOfRangeException(nameof(transform), transform, "Unmapped image transform.")
    };
}

/// <summary>
///     The engine-side facts about an extraction that the sink does not itself hold, supplied to the
///     manifest and summary writers.
/// </summary>
/// <param name="Outcome">The overall extraction outcome.</param>
/// <param name="Source">The document source (path, file name, size).</param>
/// <param name="DetectedFormat">The detected format and its evidence.</param>
/// <param name="SelectedExtractor">The selected extractor descriptor, or <see langword="null"/> when none was selected.</param>
/// <param name="Environment">The environment description, whose facts are already fully assembled by the engine.</param>
/// <param name="Options">The effective (cloned) options the extraction ran with.</param>
/// <param name="TimestampUtc">The resolved extraction timestamp to stamp into output.</param>
/// <param name="Failure">The structured failure, or <see langword="null"/> when the extraction produced output.</param>
/// <param name="ExtractorPackage">The selected extractor's providing package, or <see langword="null"/> when unknown to Core.</param>
/// <remarks>
///     Gathering these into one record gives the engine a single object to populate and hand to both
///     writers, keeping their signatures small and their inputs identical. The engine is responsible
///     for assembling <see cref="Environment"/> to include any facts the extractor contributed to the
///     sink. Immutable and thread-safe.
/// </remarks>
public sealed record ExtractionReport(
    ExtractionOutcome Outcome,
    DocumentSource Source,
    FormatDetection DetectedFormat,
    ExtractorDescriptor? SelectedExtractor,
    ExtractionEnvironment Environment,
    ExtractionOptions Options,
    DateTimeOffset TimestampUtc,
    ExtractionFailure? Failure,
    string? ExtractorPackage = null);

/// <summary>
///     The source-generated <see cref="JsonSerializerContext"/> for the manifest DTO graph.
/// </summary>
/// <remarks>
///     <para>
///         Source-generated serialization metadata is used instead of runtime reflection so
///         <c>manifest.json</c> can be produced in a trimmed, AOT-compiled, or single-file
///         published application without losing type metadata. Registering only
///         <see cref="ExtractionManifest"/> is sufficient because the generator walks the entire
///         reachable graph of nested records from that root.
///     </para>
///     <para>
///         The options mirror the manifest contract: camelCase property names, indented output
///         for human readability, and no ignore condition so absent optional values serialize as
///         explicit <c>null</c> rather than being dropped — which keeps the shape stable for
///         consumers.
///     </para>
///     <para>
///         Declared <see langword="internal"/> because only Core's <c>ManifestWriter</c> and
///         <c>ScratchFolder</c> (same assembly) serialize and deserialize the manifest; the DTOs
///         themselves are public for inspection, but the serialization context is an internal
///         implementation detail. Generated contexts are thread-safe for concurrent use.
///     </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ExtractionManifest))]
internal sealed partial class DocDownJsonContext : JsonSerializerContext;

/// <summary>
///     A description of the environment an extraction ran in, from runtime information plus
///     contributed facts.
/// </summary>
/// <param name="OperatingSystem">The operating system description (from <c>RuntimeInformation.OSDescription</c>).</param>
/// <param name="ProcessArchitecture">The process architecture (for example <c>x64</c> or <c>arm64</c>).</param>
/// <param name="RuntimeVersion">The runtime/framework description (from <c>RuntimeInformation.FrameworkDescription</c>).</param>
/// <param name="RuntimeIdentifier">The runtime identifier (RID) the process is running as.</param>
/// <param name="Facts">
///     The environment facts contributed by backends and unavailable candidates, in insertion
///     order followed by availability-derived facts; never re-sorted.
/// </param>
/// <remarks>
///     Recording the environment makes an incomplete result reproducible and explicable: whether a
///     capability was available often depends on the OS, architecture, and RID, so capturing them
///     lets a reader understand why the same document might extract differently elsewhere. The
///     fact order is preserved deliberately so provenance reads chronologically. Instances are
///     immutable and thread-safe.
/// </remarks>
public sealed record ExtractionEnvironment(string OperatingSystem, string ProcessArchitecture,
    string RuntimeVersion, string RuntimeIdentifier, IReadOnlyList<EnvironmentFact> Facts);

/// <summary>
///     Where an environment fact came from, so the summary can tell what the run actually used
///     from context about what it did not.
/// </summary>
/// <remarks>
///     A summary that lists the availability of every registered backend when exactly one ran
///     spends a reader's token budget on context. The distinction is carried in the data rather
///     than inferred from a key prefix, so trimming the summary can never accidentally hide a fact
///     a backend deliberately reported.
/// </remarks>
public enum EnvironmentFactOrigin
{
    /// <summary>
    ///     The fact was contributed by the backend that ran, describing the environment it actually
    ///     used. Always shown in the summary.
    /// </summary>
    Backend,

    /// <summary>
    ///     The fact was derived by Core from a registered candidate that did not run, recording
    ///     whether it would have been available. Shown in the summary only when it reports an
    ///     unavailability; otherwise summarized as a count with a pointer to <c>manifest.json</c>,
    ///     which always carries every fact.
    /// </summary>
    CandidateAvailability
}

/// <summary>
///     A single environment observation contributed to the extraction record.
/// </summary>
/// <param name="Source">
///     The contributing component that reported the fact (for example <c>DocDown.Pdf</c> or
///     <c>DocDown.Pdf.Rendering</c>). Required so the summary can group facts by their origin,
///     which keeps a component's honest statement (such as a capability it does not offer) from
///     reading as a whole-run failure in a multi-backend environment.
/// </param>
/// <param name="Key">A short, stable key identifying the fact (for example <c>pdf.pageRenderer</c>).</param>
/// <param name="Value">A human-readable value or description for the fact.</param>
/// <param name="Available">
///     An optional tri-state availability flag: <see langword="true"/> present,
///     <see langword="false"/> absent, or <see langword="null"/> when availability is not a
///     meaningful dimension for this fact.
/// </param>
/// <param name="Origin">
///     Whether the fact came from the backend that ran or from a registered candidate that did not.
///     Defaults to <see cref="EnvironmentFactOrigin.Backend"/> so a backend reporting a fact never
///     has to think about it and is never trimmed from the summary.
/// </param>
/// <remarks>
///     Facts let backends and unavailable candidates record why a capability could or could not
///     be provided in this environment (for example a missing native binary), which turns an
///     opaque degradation into an explained one. Instances are immutable and thread-safe.
/// </remarks>
public sealed record EnvironmentFact(
    string Source, string Key, string Value, bool? Available = null,
    EnvironmentFactOrigin Origin = EnvironmentFactOrigin.Backend);
