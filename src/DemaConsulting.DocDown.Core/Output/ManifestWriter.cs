using System.Globalization;
using System.Text.Json;

namespace DocDown.Core;

/// <summary>
///     Reconciles the completeness ledger against the reported gaps and then serializes
///     <c>manifest.json</c>, the machine-readable twin of the summary.
/// </summary>
/// <remarks>
///     <para>
///         Reconciliation is deliberately separable from serialization: the engine needs the final
///         gap list, the synthesized diagnostics, and the ledger to build its
///         <see cref="ExtractionResult"/>, and <see cref="SummaryWriter"/> needs them to render the
///         human summary — all before the JSON is written. <see cref="Reconcile"/> produces those
///         facts; <see cref="WriteAsync"/> consumes them.
///     </para>
///     <para>
///         The load-bearing invariant lives here: every ledger entry that is
///         <see cref="ArtifactStatus.Partial"/> or <see cref="ArtifactStatus.Absent"/> must be
///         explained by at least one gap whose <c>Target</c> matches. Any unexplained absence is
///         given a synthesized gap (reason "the extractor did not report why this artifact is absent")
///         and a <c>DD0701</c> diagnostic, so it is structurally impossible to emit a silent hole.
///     </para>
///     <para>
///         Serialization uses the source-generated <see cref="DocDownJsonContext"/> (no reflection,
///         AOT/trim safe); every domain enum is projected to its camelCase string before it reaches a
///         DTO, and the JSON is written with <c>\n</c> line endings and no BOM for byte-identical
///         determinism. <see cref="WriteAsync"/> performs filesystem I/O; <see cref="Reconcile"/> is
///         a pure function of its inputs. Both are stateless and safe to call from the single
///         extraction flow.
///     </para>
/// </remarks>
public static class ManifestWriter
{
    /// <summary>The manifest schema version this writer emits.</summary>
    /// <remarks>Constant so the version is stated once and the contract verifier can pin it.</remarks>
    private const string SchemaVersion = "1.2";

    /// <summary>The fixed relative name of the manifest file.</summary>
    /// <remarks>Part of the invariant output contract; never varies.</remarks>
    private const string ManifestFileName = "manifest.json";

    /// <summary>
    ///     Reconciles the ledger against the reported gaps, synthesizing gaps for any unexplained
    ///     absence.
    /// </summary>
    /// <param name="sink">The sink holding the recorded images, pages, gaps, and diagnostics. Must not be null.</param>
    /// <param name="report">The engine-side facts describing the extraction. Must not be null.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <returns>
    ///     A <see cref="ReconciliationResult"/> carrying the completeness ledger, the final gap list
    ///     (reported plus synthesized), the final diagnostic list, and the completeness flag.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> or <paramref name="report"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Pure and side-effect free: it derives the ledger from what was actually recorded, then
    ///     appends a synthesized gap and a <c>DD0701</c> diagnostic for every partial or absent entry
    ///     that no reported gap already explains. Synthesized gap identifiers continue the dense
    ///     <c>GAP-n</c> sequence the sink began.
    /// </remarks>
    public static ReconciliationResult Reconcile(ExtractionSink sink, ExtractionReport report, ContentWriteResult? content)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(report);

        // Derive the ledger from what was truly written and found
        var ledger = BuildLedger(sink, report, content);

        // Start from the reported gaps and diagnostics; the sink already assigned dense GAP identifiers
        var gaps = new List<ExtractionGap>(sink.Gaps);
        var diagnostics = new List<ExtractionDiagnostic>(sink.Diagnostics);
        var nextGapNumber = gaps.Count + 1;

        // Enforce the honesty invariant: every partial or absent entry must be explained by a gap
        var unexplained = new[] { ledger.Content, ledger.Images, ledger.Pages }
            .Where(entry => entry.Status != ArtifactStatus.Present && !HasGapFor(gaps, entry.Path));
        foreach (var entry in unexplained)
        {
            // Synthesize the missing explanation so no absence is ever silent
            var scope = entry.Status == ArtifactStatus.Absent ? GapScope.Failed : GapScope.PartiallyExtracted;
            gaps.Add(new ExtractionGap(
                $"GAP-{nextGapNumber.ToString(CultureInfo.InvariantCulture)}",
                KindFor(entry.Path),
                entry.Path,
                scope,
                "the extractor did not report why this artifact is absent"));
            nextGapNumber++;
            diagnostics.Add(new ExtractionDiagnostic(
                DiagnosticCodes.UnexplainedAbsence, DiagnosticSeverity.Warning,
                $"Core synthesized a gap for '{entry.Path}' because the extractor did not explain its absence."));
        }

        return new ReconciliationResult(ledger, gaps, diagnostics, gaps.Count == 0);
    }

    /// <summary>
    ///     Serializes the manifest to <c>manifest.json</c> in the scratch folder.
    /// </summary>
    /// <param name="folder">The scratch folder to write into. Must not be null.</param>
    /// <param name="sink">The sink holding the recorded content. Must not be null.</param>
    /// <param name="report">The engine-side facts describing the extraction. Must not be null.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <param name="reconciliation">The reconciliation output whose ledger and gaps are serialized. Must not be null.</param>
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
        ContentWriteResult? content, ReconciliationResult reconciliation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(reconciliation);

        // Assemble the immutable DTO graph from the recorded state and the reconciled facts
        var manifest = BuildManifest(folder, sink, report, reconciliation);

        // Serialize via the source-generated context, then normalize endings and append a trailing newline
        var json = JsonSerializer.Serialize(manifest, DocDownJsonContext.Default.ExtractionManifest);
        var normalized = json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        await folder.WriteTextAsync(ManifestFileName, normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the completeness ledger from the recorded content, images, and pages.
    /// </summary>
    /// <param name="sink">The sink holding the recorded state.</param>
    /// <param name="report">The engine-side facts (used for the render-pages request and options).</param>
    /// <param name="content">The content-write result, or <see langword="null"/>.</param>
    /// <returns>The completeness ledger.</returns>
    /// <remarks>
    ///     Summary and manifest are always present. Content, images, and pages statuses follow the
    ///     obtained-versus-found counts, with suppression and an unfulfilled render request treated as
    ///     absences so the invariant forces an explaining gap. Pure and side-effect free.
    /// </remarks>
    private static ArtifactLedger BuildLedger(ExtractionSink sink, ExtractionReport report, ContentWriteResult? content)
    {
        // Summary, manifest, and metadata are written unconditionally, so they are always present
        var summary = new ArtifactEntry("summary.txt", ArtifactStatus.Present);
        var manifest = new ArtifactEntry("manifest.json", ArtifactStatus.Present);
        var metadata = new ArtifactEntry("metadata.json", ArtifactStatus.Present);

        // Content is partial when a gap targets it, present when text exists, otherwise absent
        var contentPresent = content?.ContentPresent ?? false;
        var contentStatus = ContentStatus(contentPresent, HasGapFor(sink.Gaps, "content.md"));
        var contentEntry = new ArtifactEntry("content.md", contentStatus);

        // Images: suppression is an absence; otherwise compare obtained against found
        var imagesObtained = sink.Images.Count;
        var imagesFound = ImagesFound(sink, imagesObtained);
        var imagesStatus = ImageStatus(sink.ImagesSuppressed, imagesObtained, imagesFound);
        var imagesEntry = new ArtifactEntry("images/", imagesStatus, imagesObtained, imagesFound);

        // Pages: only a rendering request can turn an absence into a shortfall requiring a gap, and
        // only for a paginated format — a page request against a non-paginated format applies to
        // nothing, so its absence is legitimately present rather than a shortfall
        var pagesObtained = sink.Pages.Count;
        var pageRenderingApplicable = report.Selection.Selected?.PageRenderingApplicable ?? true;
        var (pagesStatus, pagesFound) = PageStatus(
            report.Options.RenderPages && pageRenderingApplicable, pagesObtained,
            sink.FoundCounts, sink.DocumentInfo?.PageCount);
        var pagesEntry = new ArtifactEntry("pages/", pagesStatus, pagesObtained, pagesFound);

        return new ArtifactLedger(summary, manifest, metadata, contentEntry, imagesEntry, pagesEntry);
    }

    /// <summary>
    ///     Computes the content ledger status from presence and gap coverage.
    /// </summary>
    /// <param name="present">Whether textual content was produced.</param>
    /// <param name="hasGap">Whether a gap targets <c>content.md</c>.</param>
    /// <returns>The content artifact status.</returns>
    /// <remarks>Absent without content, partial when a gap explains a shortfall, present otherwise. Pure.</remarks>
    private static ArtifactStatus ContentStatus(bool present, bool hasGap)
    {
        // No content at all is an absence; a gap over present content marks it partial
        if (!present)
        {
            return ArtifactStatus.Absent;
        }

        return hasGap ? ArtifactStatus.Partial : ArtifactStatus.Present;
    }

    /// <summary>
    ///     Computes the expected image count from suppression and the reported found count.
    /// </summary>
    /// <param name="sink">The sink holding the found counts and suppression flag.</param>
    /// <param name="obtained">The number of images written.</param>
    /// <returns>The expected image count.</returns>
    /// <remarks>Suppression means zero were sought; otherwise use the reported count or the obtained count. Pure.</remarks>
    private static int ImagesFound(ExtractionSink sink, int obtained)
    {
        // Suppressed extraction sought nothing, so nothing was expected
        if (sink.ImagesSuppressed)
        {
            return 0;
        }

        return sink.FoundCounts.TryGetValue(GapKind.Images, out var reported) ? reported : obtained;
    }

    /// <summary>
    ///     Computes the images ledger status from suppression and the obtained/found counts.
    /// </summary>
    /// <param name="suppressed">Whether image writing was suppressed.</param>
    /// <param name="obtained">The number of images written.</param>
    /// <param name="found">The number of images the extractor reported finding.</param>
    /// <returns>The images artifact status.</returns>
    /// <remarks>
    ///     Present when everything found was obtained (including zero of zero); partial when some but
    ///     not all found images were written; absent when suppressed or when images were found but
    ///     none written. Pure and side-effect free.
    /// </remarks>
    private static ArtifactStatus ImageStatus(bool suppressed, int obtained, int found)
    {
        // Suppression is an explicit, gap-worthy absence
        if (suppressed)
        {
            return ArtifactStatus.Absent;
        }

        // Nothing found and nothing written means there was legitimately nothing to extract
        if (obtained == 0)
        {
            return found == 0 ? ArtifactStatus.Present : ArtifactStatus.Absent;
        }

        // Some written: present when all found were obtained, partial otherwise
        return obtained < found ? ArtifactStatus.Partial : ArtifactStatus.Present;
    }

    /// <summary>
    ///     Computes the pages ledger status and found count from the render request and page counts.
    /// </summary>
    /// <param name="renderRequested">Whether page rendering was requested.</param>
    /// <param name="obtained">The number of pages rendered.</param>
    /// <param name="foundCounts">The reported found counts by kind.</param>
    /// <param name="documentPageCount">The document page count, or <see langword="null"/> when unknown.</param>
    /// <returns>A tuple of the pages status and the found (expected) count.</returns>
    /// <remarks>
    ///     When rendering was not requested, absent pages are legitimately present (nothing was
    ///     expected). When rendering was requested, zero rendered pages is a shortfall (absent) that
    ///     the invariant forces a gap to explain. Pure and side-effect free.
    /// </remarks>
    private static (ArtifactStatus Status, int Found) PageStatus(
        bool renderRequested, int obtained, IReadOnlyDictionary<GapKind, int> foundCounts, int? documentPageCount)
    {
        // No render request: pages were not expected, so their absence is not a shortfall
        if (!renderRequested)
        {
            return (ArtifactStatus.Present, obtained);
        }

        // Rendering was requested: the expected count is what the extractor found, the page count, or at least what was obtained
        var found = foundCounts.TryGetValue(GapKind.Pages, out var reported)
            ? reported
            : Math.Max(obtained, documentPageCount ?? 0);
        if (found < obtained)
        {
            found = obtained;
        }

        if (obtained == 0)
        {
            return (ArtifactStatus.Absent, found);
        }

        return (obtained < found ? ArtifactStatus.Partial : ArtifactStatus.Present, found);
    }

    /// <summary>
    ///     Determines whether any gap targets the given artifact path.
    /// </summary>
    /// <param name="gaps">The gaps to search.</param>
    /// <param name="target">The artifact path to match (for example <c>images/</c>).</param>
    /// <returns><see langword="true"/> when a gap's target matches; otherwise <see langword="false"/>.</returns>
    /// <remarks>Match is an exact ordinal comparison so a gap must name the precise artifact it explains.</remarks>
    private static bool HasGapFor(IReadOnlyList<ExtractionGap> gaps, string target) =>
        gaps.Any(gap => string.Equals(gap.Target, target, StringComparison.Ordinal));

    /// <summary>
    ///     Maps an artifact path to the gap kind used when synthesizing an explanation for it.
    /// </summary>
    /// <param name="artifactPath">The artifact path (<c>content.md</c>, <c>images/</c>, or <c>pages/</c>).</param>
    /// <returns>The matching <see cref="GapKind"/>.</returns>
    /// <remarks>Content is classified as text because a missing content document is missing text. Pure.</remarks>
    private static GapKind KindFor(string artifactPath) => artifactPath switch
    {
        "images/" => GapKind.Images,
        "pages/" => GapKind.Pages,
        _ => GapKind.Text
    };

    /// <summary>
    ///     Builds the immutable manifest DTO graph from the recorded state and reconciled facts.
    /// </summary>
    /// <param name="folder">The scratch folder, whose absolute path is recorded.</param>
    /// <param name="sink">The sink holding the recorded content.</param>
    /// <param name="report">The engine-side facts describing the extraction.</param>
    /// <param name="reconciliation">The reconciliation output.</param>
    /// <returns>The populated <see cref="ExtractionManifest"/>.</returns>
    /// <remarks>
    ///     Kept separate from serialization so the mapping (including every enum-to-string projection)
    ///     is expressed once and can be reasoned about independently of JSON formatting. Pure and
    ///     side-effect free.
    /// </remarks>
    private static ExtractionManifest BuildManifest(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report, ReconciliationResult reconciliation)
    {
        var selected = report.Selection.Selected;
        return new ExtractionManifest(
            SchemaVersion,
            new ManifestTool("DocDown", "DemaConsulting.DocDown.Core"),
            folder.AbsolutePath,
            FormatTimestamp(report.TimestampUtc),
            OutcomeString(report.Outcome),
            reconciliation.IsComplete,
            BuildSource(report),
            BuildExtractor(selected, report),
            BuildSelection(report.Selection),
            BuildEnvironment(report.Environment),
            BuildDocument(sink.DocumentInfo),
            BuildContentFeatures(sink.ContentFeatures),
            BuildArtifacts(reconciliation.Ledger),
            BuildImages(sink.Images),
            BuildPages(sink.Pages),
            BuildParts(sink.Parts),
            BuildGaps(reconciliation.Gaps),
            BuildDiagnostics(reconciliation.Diagnostics),
            BuildOptions(report.Options),
            BuildFailure(report.Failure));
    }

    /// <summary>Builds the manifest source block from the report.</summary>
    /// <param name="report">The engine-side facts.</param>
    /// <returns>The manifest source DTO.</returns>
    /// <remarks>Records enough to identify and integrity-check the input. Pure.</remarks>
    private static ManifestSource BuildSource(ExtractionReport report) => new(
        report.Source.Path,
        report.Source.FileName,
        report.Source.SizeBytes,
        report.SourceSha256,
        report.DetectedFormat.Format.Id,
        report.DetectedFormat.Format.MediaType,
        BasisString(report.DetectedFormat.Basis));

    /// <summary>Builds the manifest extractor block, or <see langword="null"/> when none was selected.</summary>
    /// <param name="selected">The selected descriptor, or <see langword="null"/>.</param>
    /// <param name="report">The engine-side facts (for package and fidelity).</param>
    /// <returns>The manifest extractor DTO, or <see langword="null"/>.</returns>
    /// <remarks>Uses the extractor's declared capabilities; package is unknown to Core and may be null. Pure.</remarks>
    private static ManifestExtractor? BuildExtractor(ExtractorDescriptor? selected, ExtractionReport report)
    {
        // No selected extractor means the failure block, not this block, tells the story
        if (selected is null)
        {
            return null;
        }

        return new ManifestExtractor(
            selected.Id,
            selected.DisplayName,
            report.ExtractorPackage,
            selected.Capabilities.ToCamelCaseNames(),
            selected.Priority,
            report.ExtractorFidelity);
    }

    /// <summary>Builds the manifest selection block from the selection result.</summary>
    /// <param name="selection">The selection result.</param>
    /// <returns>The manifest selection DTO.</returns>
    /// <remarks>Preserves the full candidate trace so the decision is fully auditable. Pure.</remarks>
    private static ManifestSelection BuildSelection(SelectionResult selection) => new(
        ModeString(selection.Mode),
        selection.RequiredCapabilities.ToCamelCaseNames(),
        selection.SatisfiedCapabilities.ToCamelCaseNames(),
        BuildCandidates(selection.Trace));

    /// <summary>Builds the manifest candidate list from a verdict list.</summary>
    /// <param name="verdicts">The candidate verdicts.</param>
    /// <returns>The manifest candidate DTOs in the same order.</returns>
    /// <remarks>Shared by the selection trace and the failure candidate list. Pure.</remarks>
    private static IReadOnlyList<ManifestCandidate> BuildCandidates(IReadOnlyList<CandidateVerdict> verdicts)
    {
        // Preserve verdict order so the serialized trace is deterministic
        var candidates = new List<ManifestCandidate>(verdicts.Count);
        foreach (var verdict in verdicts)
        {
            candidates.Add(new ManifestCandidate(
                verdict.ExtractorId, verdict.DisplayName, verdict.Priority,
                CandidateOutcomeString(verdict.Outcome), verdict.Detail));
        }

        return candidates;
    }

    /// <summary>Builds the manifest environment block from the environment description.</summary>
    /// <param name="environment">The environment description.</param>
    /// <returns>The manifest environment DTO.</returns>
    /// <remarks>Facts are copied in order and never re-sorted, preserving provenance. Pure.</remarks>
    private static ManifestEnvironment BuildEnvironment(ExtractionEnvironment environment)
    {
        // Copy facts in emission order so environment provenance reads chronologically
        var facts = new List<ManifestEnvironmentFact>(environment.Facts.Count);
        foreach (var fact in environment.Facts)
        {
            facts.Add(new ManifestEnvironmentFact(fact.Source, fact.Key, fact.Value, fact.Available));
        }

        return new ManifestEnvironment(
            environment.OperatingSystem, environment.ProcessArchitecture,
            environment.RuntimeVersion, environment.RuntimeIdentifier, facts);
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

    /// <summary>Builds the manifest artifacts block from the ledger.</summary>
    /// <param name="ledger">The completeness ledger.</param>
    /// <returns>The manifest artifacts DTO.</returns>
    /// <remarks>Projects each ledger status to its camelCase string. Pure.</remarks>
    private static ManifestArtifacts BuildArtifacts(ArtifactLedger ledger) => new(
        BuildArtifactEntry(ledger.Summary),
        BuildArtifactEntry(ledger.Manifest),
        BuildArtifactEntry(ledger.Metadata),
        BuildArtifactEntry(ledger.Content),
        BuildArtifactEntry(ledger.Images),
        BuildArtifactEntry(ledger.Pages));

    /// <summary>Builds a single manifest ledger row.</summary>
    /// <param name="entry">The ledger entry.</param>
    /// <returns>The manifest ledger-entry DTO.</returns>
    /// <remarks>Mirrors the entry with the status projected to a string. Pure.</remarks>
    private static ManifestArtifactEntry BuildArtifactEntry(ArtifactEntry entry) =>
        new(entry.Path, StatusString(entry.Status), entry.Obtained, entry.Found);

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
                image.SizeBytes, image.Sha256, image.SourcePage, image.SourcePages,
                image.ReferencedByTemplate, image.SourceRef,
                TransformString(image.Transform), image.References,
                image.Description, image.DescriptionSource));
        }

        return list;
    }

    /// <summary>Builds the manifest page list from the recorded pages.</summary>
    /// <param name="pages">The recorded pages.</param>
    /// <returns>The manifest page DTOs in insertion order.</returns>
    /// <remarks>Maps each page back to its document page number and integrity hash. Pure.</remarks>
    private static IReadOnlyList<ManifestPage> BuildPages(IReadOnlyList<RecordedPage> pages)
    {
        // Preserve insertion order; page numbers are the stable identity
        var list = new List<ManifestPage>(pages.Count);
        foreach (var page in pages)
        {
            list.Add(new ManifestPage(page.Path, page.PageNumber, page.SizeBytes, page.Sha256));
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

    /// <summary>Builds the manifest gap list from the reconciled gaps.</summary>
    /// <param name="gaps">The reconciled gaps.</param>
    /// <returns>The manifest gap DTOs in emission order.</returns>
    /// <remarks>Includes both reported and synthesized gaps so every absence is explained. Pure.</remarks>
    private static IReadOnlyList<ManifestGap> BuildGaps(IReadOnlyList<ExtractionGap> gaps)
    {
        // Emit in emission order so gap identifiers remain dense and ordered
        var list = new List<ManifestGap>(gaps.Count);
        foreach (var gap in gaps)
        {
            list.Add(new ManifestGap(
                gap.Id, GapKindString(gap.Kind), gap.Target, GapScopeString(gap.Scope),
                gap.Reason, gap.Impact, gap.Remedy, gap.AffectedCount, gap.AffectedItems));
        }

        return list;
    }

    /// <summary>Builds the manifest diagnostic list from the reconciled diagnostics.</summary>
    /// <param name="diagnostics">The reconciled diagnostics.</param>
    /// <returns>The manifest diagnostic DTOs in emission order.</returns>
    /// <remarks>Projects each severity to its camelCase string. Pure.</remarks>
    private static IReadOnlyList<ManifestDiagnostic> BuildDiagnostics(IReadOnlyList<ExtractionDiagnostic> diagnostics)
    {
        // Preserve emission order so the diagnostic stream reads chronologically
        var list = new List<ManifestDiagnostic>(diagnostics.Count);
        foreach (var diagnostic in diagnostics)
        {
            list.Add(new ManifestDiagnostic(
                diagnostic.Code, SeverityString(diagnostic.Severity), diagnostic.Message, diagnostic.Location));
        }

        return list;
    }

    /// <summary>Builds the manifest requested-options block from the effective options.</summary>
    /// <param name="options">The effective options.</param>
    /// <returns>The manifest options DTO.</returns>
    /// <remarks>Records exactly what was asked for so the run is reproducible. Pure.</remarks>
    private static ManifestOptions BuildOptions(ExtractionOptions options) => new(
        options.RenderPages,
        FormatPageRange(options.Pages),
        options.IncludeEmbeddedImages,
        ImageOutputString(options.ImageOutput),
        options.MaxImageDimensionPx,
        options.MaxImageBytes,
        options.PageRenderDpi,
        SplitString(options.ContentSplit),
        ScratchModeString(options.ScratchFolder),
        options.PreferredExtractorId,
        options.RequireCapabilities?.ToCamelCaseNames());

    /// <summary>Builds the manifest failure block, or <see langword="null"/> when the extraction did not fail.</summary>
    /// <param name="failure">The structured failure, or <see langword="null"/>.</param>
    /// <returns>The manifest failure DTO, or <see langword="null"/>.</returns>
    /// <remarks>Projects the failure kind to a string and carries the verbatim explanation. Pure.</remarks>
    private static ManifestFailure? BuildFailure(ExtractionFailure? failure)
    {
        // Absent failure serializes as an explicit null so the shape stays stable
        if (failure is null)
        {
            return null;
        }

        return new ManifestFailure(
            FailureKindString(failure.Kind), failure.Code, failure.Summary,
            failure.Explanation, failure.Remedy, BuildCandidates(failure.Candidates));
    }

    /// <summary>Formats a timestamp as ISO-8601 UTC with second precision.</summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>The formatted UTC timestamp (for example <c>2026-09-10T16:33:11Z</c>).</returns>
    /// <remarks>Uses the invariant culture so the value is stable across locales. Pure.</remarks>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>Formats an optional page range as <c>first-last</c>.</summary>
    /// <param name="range">The page range, or <see langword="null"/> for all pages.</param>
    /// <returns>The formatted range, or <see langword="null"/> when no range was set.</returns>
    /// <remarks>A null range means the whole document, recorded as an explicit null. Pure.</remarks>
    private static string? FormatPageRange(PageRange? range) => range is { } value
        ? $"{value.First.ToString(CultureInfo.InvariantCulture)}-{value.Last.ToString(CultureInfo.InvariantCulture)}"
        : null;

    /// <summary>Projects an extraction outcome to its camelCase string.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Kept as a switch so an unmapped value fails fast rather than serializing wrongly. Pure.</remarks>
    private static string OutcomeString(ExtractionOutcome outcome) => outcome switch
    {
        ExtractionOutcome.Succeeded => "succeeded",
        ExtractionOutcome.Degraded => "degraded",
        ExtractionOutcome.Failed => "failed",
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
        DetectionBasis.CallerSpecified => "callerSpecified",
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unmapped detection basis.")
    };

    /// <summary>Projects a selection mode to its camelCase string.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string ModeString(SelectionMode mode) => mode switch
    {
        SelectionMode.Automatic => "automatic",
        SelectionMode.CallerOverride => "callerOverride",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped selection mode.")
    };

    /// <summary>Projects a candidate outcome to its camelCase string.</summary>
    /// <param name="outcome">The candidate outcome.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string CandidateOutcomeString(CandidateOutcome outcome) => outcome switch
    {
        CandidateOutcome.Selected => "selected",
        CandidateOutcome.FormatNotSupported => "formatNotSupported",
        CandidateOutcome.Unavailable => "unavailable",
        CandidateOutcome.CapabilitiesInsufficient => "capabilitiesInsufficient",
        CandidateOutcome.OutrankedByHigherFidelity => "outrankedByHigherFidelity",
        CandidateOutcome.ExcludedByOverride => "excludedByOverride",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unmapped candidate outcome.")
    };

    /// <summary>Projects an artifact status to its camelCase string.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string StatusString(ArtifactStatus status) => status switch
    {
        ArtifactStatus.Present => "present",
        ArtifactStatus.Partial => "partial",
        ArtifactStatus.Absent => "absent",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped artifact status.")
    };

    /// <summary>Projects a gap kind to its camelCase string.</summary>
    /// <param name="kind">The gap kind.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string GapKindString(GapKind kind) => kind switch
    {
        GapKind.Text => "text",
        GapKind.Images => "images",
        GapKind.Pages => "pages",
        GapKind.Structure => "structure",
        GapKind.Metadata => "metadata",
        GapKind.Parts => "parts",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped gap kind.")
    };

    /// <summary>Projects a gap scope to its camelCase string.</summary>
    /// <param name="scope">The gap scope.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string GapScopeString(GapScope scope) => scope switch
    {
        GapScope.NotAttempted => "notAttempted",
        GapScope.Unavailable => "unavailable",
        GapScope.PartiallyExtracted => "partiallyExtracted",
        GapScope.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unmapped gap scope.")
    };

    /// <summary>Projects a diagnostic severity to its camelCase string.</summary>
    /// <param name="severity">The severity.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string SeverityString(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => "info",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unmapped diagnostic severity.")
    };

    /// <summary>Projects a failure kind to its camelCase string.</summary>
    /// <param name="kind">The failure kind.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string FailureKindString(ExtractionFailureKind kind) => kind switch
    {
        ExtractionFailureKind.FormatNotRecognized => "formatNotRecognized",
        ExtractionFailureKind.NoExtractorForFormat => "noExtractorForFormat",
        ExtractionFailureKind.NoAvailableExtractor => "noAvailableExtractor",
        ExtractionFailureKind.RequiredCapabilitiesUnavailable => "requiredCapabilitiesUnavailable",
        ExtractionFailureKind.RequestedExtractorNotApplicable => "requestedExtractorNotApplicable",
        ExtractionFailureKind.RequestedExtractorUnavailable => "requestedExtractorUnavailable",
        ExtractionFailureKind.SourceUnreadable => "sourceUnreadable",
        ExtractionFailureKind.ScratchFolderRefused => "scratchFolderRefused",
        ExtractionFailureKind.ExtractorFailed => "extractorFailed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped failure kind.")
    };

    /// <summary>Projects an image output mode to its camelCase string.</summary>
    /// <param name="mode">The image output mode.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string ImageOutputString(ImageOutputMode mode) => mode switch
    {
        ImageOutputMode.Preserve => "preserve",
        ImageOutputMode.ForcePng => "forcePng",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped image output mode.")
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

    /// <summary>Projects a content split mode to its camelCase string.</summary>
    /// <param name="mode">The content split mode.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string SplitString(ContentSplitMode mode) => mode switch
    {
        ContentSplitMode.Auto => "auto",
        ContentSplitMode.Single => "single",
        ContentSplitMode.PerPart => "perPart",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped content split mode.")
    };

    /// <summary>Projects a scratch-folder mode to its camelCase string.</summary>
    /// <param name="mode">The scratch-folder mode.</param>
    /// <returns>The camelCase name.</returns>
    /// <remarks>Switch-based so an unmapped value is caught. Pure.</remarks>
    private static string ScratchModeString(ScratchFolderMode mode) => mode switch
    {
        ScratchFolderMode.RequireEmpty => "requireEmpty",
        ScratchFolderMode.CleanIfDocDownFolder => "cleanIfDocDownFolder",
        ScratchFolderMode.Overwrite => "overwrite",
        ScratchFolderMode.CreateUnique => "createUnique",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped scratch-folder mode.")
    };
}

/// <summary>
///     The engine-side facts about an extraction that the sink does not itself hold, supplied to the
///     manifest and summary writers.
/// </summary>
/// <param name="Outcome">The overall extraction outcome.</param>
/// <param name="Source">The document source (path, file name, size).</param>
/// <param name="SourceSha256">The SHA-256 of the source bytes, or <see langword="null"/> when not computed.</param>
/// <param name="DetectedFormat">The detected format and its evidence.</param>
/// <param name="Selection">The selection decision, capability negotiation, and candidate trace.</param>
/// <param name="Environment">The environment description, whose facts are already fully assembled by the engine.</param>
/// <param name="Options">The effective (cloned) options the extraction ran with.</param>
/// <param name="TimestampUtc">The resolved extraction timestamp to stamp into output.</param>
/// <param name="Failure">The structured failure, or <see langword="null"/> when the extraction did not fail.</param>
/// <param name="ExtractorPackage">The selected extractor's providing package, or <see langword="null"/> when unknown to Core.</param>
/// <param name="ExtractorFidelity">A short fidelity descriptor for the selected extractor.</param>
/// <remarks>
///     Gathering these into one record gives the engine a single object to populate and hand to both
///     writers, keeping their signatures small and their inputs identical. The engine is responsible
///     for assembling <see cref="Environment"/> to include any facts the extractor contributed to the
///     sink. Immutable and thread-safe.
/// </remarks>
public sealed record ExtractionReport(
    ExtractionOutcome Outcome,
    DocumentSource Source,
    string? SourceSha256,
    FormatDetection DetectedFormat,
    SelectionResult Selection,
    ExtractionEnvironment Environment,
    ExtractionOptions Options,
    DateTimeOffset TimestampUtc,
    ExtractionFailure? Failure,
    string? ExtractorPackage = null,
    string ExtractorFidelity = "bestEffort");

/// <summary>
///     The output of ledger-and-gap reconciliation: the completeness ledger, the final gaps and
///     diagnostics, and whether the extraction is complete.
/// </summary>
/// <param name="Ledger">The completeness ledger derived from what was actually produced.</param>
/// <param name="Gaps">The final gaps: those reported plus any synthesized for an unexplained absence.</param>
/// <param name="Diagnostics">The final diagnostics: those reported plus any synthesized <c>DD0701</c> warnings.</param>
/// <param name="IsComplete"><see langword="true"/> when there are no gaps.</param>
/// <remarks>
///     Produced by <see cref="ManifestWriter.Reconcile"/> and shared by the engine (for its result),
///     <see cref="ManifestWriter.WriteAsync"/>, and <see cref="SummaryWriter"/>, so all three agree
///     on the same reconciled facts. Immutable and thread-safe.
/// </remarks>
public sealed record ReconciliationResult(
    ArtifactLedger Ledger,
    IReadOnlyList<ExtractionGap> Gaps,
    IReadOnlyList<ExtractionDiagnostic> Diagnostics,
    bool IsComplete);
