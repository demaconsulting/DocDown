using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DocDown.Core;

/// <summary>
///     The public facade that orchestrates one extraction end to end: scratch preparation, source
///     reading, format detection, extractor selection, backend invocation, and the honest,
///     self-describing output layout.
/// </summary>
/// <remarks>
///     <para>
///         The engine runs the fixed pipeline in order and <strong>returns failures rather than
///         throwing them</strong>. Exceptions are reserved for programming errors — a null or empty
///         argument raises <see cref="ArgumentNullException"/>/<see cref="ArgumentException"/> — and for
///         <see cref="OperationCanceledException"/>, which propagates so a caller can observe
///         cancellation. Every other adverse condition (an unreadable source, an unrecognized format,
///         no viable backend, or a backend that threw) becomes a structured
///         <see cref="ExtractionFailure"/> on the returned <see cref="ExtractionResult"/>.
///     </para>
///     <para>
///         A failed extraction still writes the full artifact layout — including a
///         <c>summary.txt</c> whose failure section carries the explanation verbatim and a
///         <c>manifest.json</c> — so the LLM-facing contract never has a hole where failures live. The
///         one exception is a refused scratch folder: Core cannot write into a folder it refused, so
///         that failure returns with the requested absolute path as <see cref="ExtractionResult.ScratchFolder"/>
///         and the paths the summary and manifest <em>would</em> have used, but no files are written.
///     </para>
///     <para>
///         <strong>Thread safety.</strong> The engine is safe for concurrent
///         <see cref="ExtractAsync(DocumentSource,string,ExtractionOptions?,CancellationToken)"/> calls
///         that target <em>different</em> scratch folders: it holds no per-call mutable state and each
///         call clones its options on entry so caller mutation cannot affect an in-flight or later
///         run. Two concurrent calls into the <em>same</em> scratch folder are a caller error — they
///         would race on the same files — and are not supported.
///     </para>
/// </remarks>
public sealed class DocDownEngine
{
    /// <summary>The immutable registry of extractors backing this engine.</summary>
    /// <remarks>Shared read-only for the engine's lifetime; its availability cache is the only mutable part.</remarks>
    private readonly ExtractorRegistry _registry;

    /// <summary>The engine's private default options, cloned from the builder at construction.</summary>
    /// <remarks>Never mutated; each extraction clones it (or the caller's options) so defaults stay pristine.</remarks>
    private readonly ExtractionOptions _defaults;

    /// <summary>The pure selector used to rank candidates.</summary>
    /// <remarks>Stateless and reusable across concurrent extractions.</remarks>
    private readonly ExtractorSelector _selector = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DocDownEngine"/> class.
    /// </summary>
    /// <param name="registry">The materialized extractor registry. Must not be null.</param>
    /// <param name="defaults">The engine's private default options (already cloned by the builder). Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> or <paramref name="defaults"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     <see langword="internal"/> because only <see cref="DocDownBuilder.Build"/> constructs an
    ///     engine, guaranteeing the registry was built with duplicate-identifier detection and the
    ///     defaults are a private copy the caller cannot mutate.
    /// </remarks>
    internal DocDownEngine(ExtractorRegistry registry, ExtractionOptions defaults)
    {
        // Guard the collaborators so no extraction has to defend against a missing registry or defaults
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(defaults);
        _registry = registry;
        _defaults = defaults;
    }

    /// <summary>
    ///     Gets the descriptors of the registered extractors in registration order.
    /// </summary>
    /// <remarks>Probe-free, so reading it never inspects the environment; use <see cref="GetBackendStatus"/> for availability.</remarks>
    public IReadOnlyList<ExtractorDescriptor> Extractors => _registry.Descriptors;

    /// <summary>
    ///     Extracts a document identified by file path into the given scratch folder.
    /// </summary>
    /// <param name="documentPath">The path to the source document. Must not be null or empty.</param>
    /// <param name="scratchFolder">The folder to write the output layout into. Must not be null or empty.</param>
    /// <param name="options">The options for this extraction, or <see langword="null"/> to use the engine defaults.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result, carrying the outcome, paths, and honesty record.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentPath"/> or <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    ///     A convenience overload that builds a <see cref="DocumentSource"/> from the file path and
    ///     delegates to the stream-agnostic overload; all pipeline behavior is identical.
    /// </remarks>
    public ValueTask<ExtractionResult> ExtractAsync(
        string documentPath, string scratchFolder, ExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        // A path is required to build a source; validate before constructing anything
        ArgumentException.ThrowIfNullOrEmpty(documentPath);
        return ExtractAsync(DocumentSource.FromFile(documentPath), scratchFolder, options, cancellationToken);
    }

    /// <summary>
    ///     Extracts a document from a <see cref="DocumentSource"/> into the given scratch folder.
    /// </summary>
    /// <param name="source">The source document. Must not be null.</param>
    /// <param name="scratchFolder">The folder to write the output layout into. Must not be null or empty.</param>
    /// <param name="options">The options for this extraction, or <see langword="null"/> to use the engine defaults.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result, carrying the outcome, paths, and honesty record.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    ///     Clones the effective options <strong>before any other work</strong> (Correction C5), so a
    ///     caller that mutates its options object after the call cannot affect this in-flight
    ///     extraction or a later one. Adverse conditions are returned as a failure on the result rather
    ///     than thrown; only argument faults and cancellation surface as exceptions.
    /// </remarks>
    public async ValueTask<ExtractionResult> ExtractAsync(
        DocumentSource source, string scratchFolder, ExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Step 1: reject the only inputs whose absence is a programming error
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);

        // Step 2 (C5/D5): take a private snapshot of the options before touching anything else
        var effective = options?.Clone() ?? _defaults.Clone();
        var timestamp = effective.TimestampUtc ?? DateTimeOffset.UtcNow;
        var mode = string.IsNullOrEmpty(effective.PreferredExtractorId)
            ? SelectionMode.Automatic
            : SelectionMode.CallerOverride;

        // Step 3: resolve the scratch folder; a refusal is the one failure with no writable layout
        ScratchFolder folder;
        try
        {
            folder = ScratchFolder.Prepare(scratchFolder, effective.ScratchFolder);
        }
        catch (ScratchFolderException exception)
        {
            return BuildScratchRefusedResult(scratchFolder, mode, exception);
        }

        return await RunPipelineAsync(folder, source, effective, timestamp, mode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the current availability status of every registered backend.
    /// </summary>
    /// <returns>One <see cref="BackendStatus"/> per registered extractor, in registration order.</returns>
    /// <remarks>
    ///     Uses the registry's cached availability, so it reflects the last probe pass without opening
    ///     any document. Useful for diagnosing a deployment — for example seeing a backend present but
    ///     unavailable and the reason — without triggering an extraction.
    /// </remarks>
    public IReadOnlyList<BackendStatus> GetBackendStatus()
    {
        // Project each candidate's descriptor and cached availability into a flat status record
        var statuses = new List<BackendStatus>();
        foreach (var candidate in _registry.GetCandidates())
        {
            var descriptor = candidate.Descriptor;
            var availability = candidate.Availability;
            statuses.Add(new BackendStatus(
                descriptor.Id, descriptor.DisplayName, descriptor.SupportedFormats,
                descriptor.Capabilities, availability.EffectiveCapabilities, descriptor.Priority,
                availability.IsAvailable, availability.UnavailableReason));
        }

        return statuses;
    }

    /// <summary>
    ///     Discards cached backend availability so the next query or extraction re-probes.
    /// </summary>
    /// <remarks>
    ///     Delegates to the registry's refresh. The escape hatch for a long-lived process whose
    ///     environment genuinely changed (for example a backend was installed); ordinary runs rely on
    ///     the per-engine cache.
    /// </remarks>
    public void RefreshAvailability() => _registry.RefreshAvailability();

    /// <summary>
    ///     Returns Core's own self-test cases followed by those of every registered extractor.
    /// </summary>
    /// <returns>
    ///     The self-test cases: three Core cases in category <c>core</c>, then the union of the cases
    ///     contributed by each registered <see cref="ISelfValidating"/> extractor, in registration
    ///     order.
    /// </returns>
    /// <remarks>
    ///     Core's three cases genuinely exercise the contract (layout invariance, gap accuracy via
    ///     <see cref="ContractVerifier"/>, and manifest schema validity) by running an in-process
    ///     extraction and cleaning up after themselves. An extractor whose cached probe reports
    ///     unavailable has its cases wrapped so they return <see cref="SelfTestResult.Skipped(string)"/>
    ///     without running, so a case that cannot run in this environment is never mistaken for a pass.
    /// </remarks>
    public IReadOnlyList<SelfTestCase> GetSelfTestCases()
    {
        // Core's own cases run first and actually execute the pipeline
        var cases = new List<SelfTestCase>
        {
            new("core.layout-invariance", "core", context => ExecuteCoreCase(context, AssertLayout)),
            new("core.gap-accuracy", "core", context => ExecuteCoreCase(context, AssertGapAccuracy)),
            new("core.manifest-schema", "core", context => ExecuteCoreCase(context, AssertManifestSchema))
        };

        // Index cached availability so each backend's cases can be wrapped when it is unavailable
        var availabilityById = new Dictionary<string, ExtractorCandidate>(StringComparer.Ordinal);
        foreach (var candidate in _registry.GetCandidates())
        {
            availabilityById[candidate.Descriptor.Id] = candidate;
        }

        // Append each self-validating backend's cases in registration order, skipping the unavailable
        foreach (var extractor in _registry.Extractors)
        {
            if (extractor is not ISelfValidating validating)
            {
                continue;
            }

            var isAvailable = availabilityById.TryGetValue(extractor.Id, out var candidate) && candidate.Availability.IsAvailable;
            var skipReason = BuildSkipReason(extractor.Id, candidate);
            AppendBackendCases(cases, validating, isAvailable, skipReason);
        }

        return cases;
    }

    /// <summary>
    ///     Runs the pipeline steps that require a prepared scratch folder.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="source">The source document.</param>
    /// <param name="options">The already-cloned effective options.</param>
    /// <param name="timestamp">The resolved extraction timestamp.</param>
    /// <param name="mode">The selection mode implied by the options.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result for this run.</returns>
    /// <remarks>
    ///     Kept separate from the entry method so the pipeline body — read, sniff, select, invoke — is
    ///     readable and each failure branch writes the full layout through one shared path. Performs
    ///     filesystem I/O.
    /// </remarks>
    private async ValueTask<ExtractionResult> RunPipelineAsync(
        ScratchFolder folder, DocumentSource source, ExtractionOptions options,
        DateTimeOffset timestamp, SelectionMode mode, CancellationToken cancellationToken)
    {
        // The sink is the sole write path; create it early so every failure branch can write the layout
        var sink = new ExtractionSink(folder, options);
        var candidates = _registry.GetCandidates();

        // Surface why any backend was excluded so the manifest and summary can explain the environment
        foreach (var diagnostic in _registry.AvailabilityDiagnostics)
        {
            sink.ReportDiagnostic(diagnostic);
        }

        // Step 4: read the source fully so it can be hashed and sniffed from a seekable buffer
        byte[] bytes;
        try
        {
            bytes = await ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a caller signal, not a failure to record
            throw;
        }
        catch (Exception exception) when (IsIoFault(exception))
        {
            var failure = MakeSimpleFailure(ExtractionFailureKind.SourceUnreadable, DiagnosticCodes.SourceUnreadable,
                $"The source document '{source.FileName}' could not be read.", UnknownDetection(),
                "Verify the document exists and is readable, then retry.");
            var inputs = new PipelineInputs(source, null, UnknownDetection(), options, timestamp, candidates);
            return await WriteFailureAsync(folder, sink, inputs, EmptySelection(mode, failure), failure, cancellationToken).ConfigureAwait(false);
        }

        var sha = ComputeSha(bytes);
        var detection = Sniff(bytes, source.FileName);
        var baseInputs = new PipelineInputs(source, sha, detection, options, timestamp, candidates);

        // Step 5: an unrecognized format cannot be routed to any backend
        if (detection.Format.IsUnknown)
        {
            var failure = MakeSimpleFailure(ExtractionFailureKind.FormatNotRecognized, DiagnosticCodes.FormatNotRecognized,
                $"The format of '{source.FileName}' could not be recognized.", detection,
                "Supply a document with a recognized format or a known file extension.");
            return await WriteFailureAsync(folder, sink, baseInputs, EmptySelection(mode, failure), failure, cancellationToken).ConfigureAwait(false);
        }

        // Step 6: rank the candidates; a selection failure is fully explained by the selector
        var selection = _selector.Select(detection, options, candidates);
        if (selection.Failure is not null)
        {
            return await WriteFailureAsync(folder, sink, baseInputs, selection, selection.Failure, cancellationToken).ConfigureAwait(false);
        }

        // Step 7 onward: invoke the backend and finalize the successful (or degraded) layout
        return await RunExtractionAsync(folder, sink, baseInputs, selection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Invokes the selected backend and finalizes the output layout for a non-selection failure.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink the backend writes through.</param>
    /// <param name="inputs">The gathered pipeline inputs (source, hash, detection, options, timestamp, candidates).</param>
    /// <param name="selection">The successful selection whose extractor will run.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result for this run.</returns>
    /// <remarks>
    ///     The backend receives only the <see cref="DocumentSource"/> and an
    ///     <see cref="IExtractionContext"/>; it never learns the scratch path. A thrown exception
    ///     (other than <see cref="OperationCanceledException"/>, which propagates) is contained and
    ///     converted into an <see cref="ExtractionFailureKind.ExtractorFailed"/> failure with the full
    ///     layout still written. Performs filesystem I/O.
    /// </remarks>
    private async ValueTask<ExtractionResult> RunExtractionAsync(
        ScratchFolder folder, ExtractionSink sink, PipelineInputs inputs, SelectionResult selection, CancellationToken cancellationToken)
    {
        var selected = selection.Selected!;
        var selectedCandidate = inputs.Candidates.First(
            candidate => string.Equals(candidate.Descriptor.Id, selected.Id, StringComparison.Ordinal));
        var selectedEffective = selectedCandidate.Availability.EffectiveCapabilities;

        // Build the context the backend sees; it exposes the sink and options but no output path
        var baseEnvironment = BuildEnvironment(AvailabilityFacts(inputs.Candidates, selected.Id));
        var context = new ExtractionContext(inputs.Options, sink, inputs.Detection, selected, baseEnvironment, cancellationToken);
        var extractor = _registry.Resolve(selected.Id);

        ExtractionOutcome extractorOutcome;
        try
        {
            extractorOutcome = await extractor.ExtractAsync(inputs.Source, context).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation propagates rather than being recorded as a backend failure
            throw;
        }
#pragma warning disable CA1031 // The backend is untrusted; any fault becomes a structured failure
        catch (Exception exception)
#pragma warning restore CA1031
        {
            var failure = MakeExtractorFailure(selected, exception, inputs.Detection, selection.Trace);
            return await WriteFailureAsync(folder, sink, inputs, selection, failure, cancellationToken).ConfigureAwait(false);
        }

        // Step 8: record the gaps Core knows about that the backend cannot report itself
        EmitCoreDerivedGaps(sink, inputs.Options, selected, selectedEffective, inputs.Detection, inputs.Candidates);

        // Step 9: finalize content, then reconcile and serialize the manifest and summary
        var content = await ContentWriter.WriteAsync(
            sink, inputs.Options.ContentSplit, sink.DocumentInfo?.Title, cancellationToken).ConfigureAwait(false);
        if (!content.ContentPresent)
        {
            EmitNoTextGap(sink);
        }

        var environment = BuildEnvironment(FinalFacts(sink, inputs.Candidates, selected.Id));
        var report = new ExtractionReport(
            ExtractionOutcome.Succeeded, inputs.Source, inputs.Sha, inputs.Detection, selection,
            environment, inputs.Options, inputs.Timestamp, null);
        return await ProduceResultAsync(folder, sink, report, content, extractorOutcome, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes the full layout for a failed extraction and builds its result.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink to record the failure artifacts through.</param>
    /// <param name="inputs">The gathered pipeline inputs.</param>
    /// <param name="selection">The selection (possibly empty) associated with the failure.</param>
    /// <param name="failure">The structured failure to record and serialize.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The failed extraction result, with the full layout written.</returns>
    /// <remarks>
    ///     Records a failure diagnostic and the failed-artifact gaps, then serializes the manifest and
    ///     summary so even a failure produces a self-describing layout. Content is not written, so
    ///     <c>content.md</c> is honestly absent. Performs filesystem I/O.
    /// </remarks>
    private static async ValueTask<ExtractionResult> WriteFailureAsync(
        ScratchFolder folder, ExtractionSink sink, PipelineInputs inputs,
        SelectionResult selection, ExtractionFailure failure, CancellationToken cancellationToken)
    {
        // Record the failure once as a diagnostic and as failed-artifact gaps so nothing is unexplained
        EmitFailureArtifacts(sink, failure);

        var environment = BuildEnvironment(FinalFacts(sink, inputs.Candidates, selection.Selected?.Id));
        var report = new ExtractionReport(
            ExtractionOutcome.Failed, inputs.Source, inputs.Sha, inputs.Detection, selection,
            environment, inputs.Options, inputs.Timestamp, failure);
        return await ProduceResultAsync(folder, sink, report, null, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reconciles, resolves the outcome, serializes the manifest and summary, and builds the result.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink holding the recorded content.</param>
    /// <param name="report">The report with a provisional outcome; the final outcome is resolved here.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <param name="extractorOutcome">The backend's own reported outcome, or <see langword="null"/> when it did not run.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The assembled extraction result.</returns>
    /// <remarks>
    ///     Reconciliation runs before serialization so the ledger and gaps are consistent; the outcome
    ///     is resolved from the failure, the backend's outcome, the presence of any gap, and whether
    ///     selection satisfied fewer than the required capabilities. Performs filesystem I/O.
    /// </remarks>
    private static async ValueTask<ExtractionResult> ProduceResultAsync(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
        ContentWriteResult? content, ExtractionOutcome? extractorOutcome, CancellationToken cancellationToken)
    {
        // Reconcile first so ledger, gaps, and diagnostics are settled before anything is serialized
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        var outcome = ResolveOutcome(report, reconciliation, extractorOutcome);
        var finalReport = report with { Outcome = outcome };

        await ManifestWriter.WriteAsync(folder, sink, finalReport, content, reconciliation, cancellationToken).ConfigureAwait(false);
        await MetadataWriter.WriteAsync(folder, sink, cancellationToken).ConfigureAwait(false);
        await SummaryWriter.WriteAsync(folder, sink, finalReport, content, reconciliation, cancellationToken).ConfigureAwait(false);

        return BuildResult(folder, sink, finalReport, content, reconciliation);
    }

    /// <summary>
    ///     Resolves the final extraction outcome from the failure, backend outcome, gaps, and capability fit.
    /// </summary>
    /// <param name="report">The report carrying any failure and the selection.</param>
    /// <param name="reconciliation">The reconciliation output whose gaps signal degradation.</param>
    /// <param name="extractorOutcome">The backend's own reported outcome, or <see langword="null"/>.</param>
    /// <returns>The resolved outcome.</returns>
    /// <remarks>
    ///     A failure is decisive. Otherwise the run is degraded when the backend degraded, any gap
    ///     exists, or the selected backend satisfied fewer than the required capabilities; only a run
    ///     with none of these is a clean success. Pure.
    /// </remarks>
    private static ExtractionOutcome ResolveOutcome(
        ExtractionReport report, ReconciliationResult reconciliation, ExtractionOutcome? extractorOutcome)
    {
        // A recorded failure always wins over any partial-success signal
        if (report.Failure is not null)
        {
            return ExtractionOutcome.Failed;
        }

        // Any gap, a self-reported degrade, or an unmet requirement all mean the result is not clean.
        // Page rendering that does not apply to a non-paginated format is not an unmet requirement: the
        // request was honored with silence, so renderedPages is masked out of the fit comparison here.
        var required = report.Selection.RequiredCapabilities;
        var satisfied = report.Selection.SatisfiedCapabilities;
        if (report.Selection.Selected is { PageRenderingApplicable: false })
        {
            required &= ~ExtractorCapabilities.RenderedPages;
            satisfied &= ~ExtractorCapabilities.RenderedPages;
        }

        var degraded = extractorOutcome == ExtractionOutcome.Degraded
            || reconciliation.Gaps.Count > 0
            || satisfied != required;
        return degraded ? ExtractionOutcome.Degraded : ExtractionOutcome.Succeeded;
    }

    /// <summary>
    ///     Assembles the immutable <see cref="ExtractionResult"/> from the finalized facts.
    /// </summary>
    /// <param name="folder">The prepared scratch folder, whose absolute path anchors the result.</param>
    /// <param name="sink">The sink holding the recorded images and pages.</param>
    /// <param name="report">The final report (with resolved outcome, selection, detection, environment).</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when none was written.</param>
    /// <param name="reconciliation">The reconciliation output supplying gaps, diagnostics, and the ledger.</param>
    /// <returns>The assembled result.</returns>
    /// <remarks>
    ///     The summary and manifest paths are absolute so a caller can use them directly; the content,
    ///     image, page, and part paths stay relative to the scratch folder to match the manifest. Pure.
    /// </remarks>
    private static ExtractionResult BuildResult(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
        ContentWriteResult? content, ReconciliationResult reconciliation)
    {
        var absolute = folder.AbsolutePath;
        return new ExtractionResult(
            report.Outcome,
            absolute,
            Path.Combine(absolute, "summary.txt"),
            Path.Combine(absolute, "manifest.json"),
            content?.ContentPath,
            sink.Images.Select(image => image.Path).ToList(),
            sink.Pages.Select(page => page.Path).ToList(),
            content?.PartPaths ?? [],
            report.DetectedFormat,
            report.Selection.Selected,
            report.Selection.Mode,
            report.Selection.Trace,
            report.Failure,
            reconciliation.Diagnostics,
            reconciliation.IsComplete,
            report.Environment,
            reconciliation.Gaps,
            reconciliation.Ledger);
    }

    /// <summary>
    ///     Builds the result for a refused scratch folder, the one failure that writes no layout.
    /// </summary>
    /// <param name="scratchFolder">The originally requested scratch folder path.</param>
    /// <param name="mode">The selection mode implied by the options.</param>
    /// <param name="exception">The refusal carrying the reason.</param>
    /// <returns>A failed result with the requested absolute path and the would-be summary and manifest paths.</returns>
    /// <remarks>
    ///     Core cannot write into a folder it refused, so this is the single case where
    ///     <c>summary.txt</c> and <c>manifest.json</c> are not produced. The result nonetheless carries
    ///     the requested absolute path and the paths the summary and manifest would have used so the
    ///     caller can report them. Pure apart from resolving the absolute path.
    /// </remarks>
    private static ExtractionResult BuildScratchRefusedResult(string scratchFolder, SelectionMode mode, ScratchFolderException exception)
    {
        var requested = SafeFullPath(scratchFolder);
        var summary = $"The scratch folder '{requested}' was refused: {exception.Message}";
        var failure = MakeSimpleFailure(ExtractionFailureKind.ScratchFolderRefused, DiagnosticCodes.ScratchFolderRefused,
            summary, UnknownDetection(),
            "Choose an empty folder, a dedicated DocDown output folder, or a different scratch-folder mode.");
        var diagnostics = new List<ExtractionDiagnostic>
        {
            new(DiagnosticCodes.ScratchFolderRefused, DiagnosticSeverity.Error, summary)
        };

        return new ExtractionResult(
            ExtractionOutcome.Failed,
            requested,
            Path.Combine(requested, "summary.txt"),
            Path.Combine(requested, "manifest.json"),
            null,
            [],
            [],
            [],
            UnknownDetection(),
            null,
            mode,
            [],
            failure,
            diagnostics,
            false,
            BuildEnvironment([]),
            [],
            AbsentLedger());
    }

    /// <summary>
    ///     Emits the gaps and diagnostics Core can derive without any backend cooperation.
    /// </summary>
    /// <param name="sink">The sink to report the gaps and diagnostics through.</param>
    /// <param name="options">The effective options driving the suppression and render requests.</param>
    /// <param name="selected">The selected extractor descriptor.</param>
    /// <param name="selectedEffective">The selected extractor's effective capabilities in this environment.</param>
    /// <param name="detection">The detected format, used to identify alternate page-rendering backends.</param>
    /// <param name="candidates">All candidates, used to name unavailable backends that could have rendered pages.</param>
    /// <remarks>
    ///     Encodes the knowledge the engine has that the backend does not: suppressed images, a render
    ///     request the selected backend cannot satisfy (naming every unavailable backend that could
    ///     have), and a render request that produced no pages. Each condition emits both the gap and
    ///     the matching diagnostics. Side effect: records on the sink.
    /// </remarks>
    private static void EmitCoreDerivedGaps(
        ExtractionSink sink, ExtractionOptions options, ExtractorDescriptor selected,
        ExtractorCapabilities selectedEffective, FormatDetection detection, IReadOnlyList<ExtractorCandidate> candidates)
    {
        // Suppressed embedded images: record the deliberate, gap-worthy absence
        if (!options.IncludeEmbeddedImages)
        {
            // Avoid duplicating the sink's own suppression diagnostic when the backend already tripped it
            if (!sink.ImagesSuppressed)
            {
                sink.ReportDiagnostic(new ExtractionDiagnostic(
                    DiagnosticCodes.EmbeddedImagesDisabled, DiagnosticSeverity.Info,
                    "Embedded-image extraction was disabled by the caller options."));
            }

            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Images, "images/", GapScope.NotAttempted,
                "Embedded-image extraction was disabled by the caller options.",
                Impact: "Images embedded in the document are not available.",
                Remedy: "Enable IncludeEmbeddedImages to extract embedded images."));
        }

        // A render request the selected backend cannot meet degrades the run and names who could have
        if (options.RenderPages && !selectedEffective.HasFlag(ExtractorCapabilities.RenderedPages))
        {
            if (selected.PageRenderingApplicable)
            {
                EmitRenderUnavailableGap(sink, selected, detection, candidates);
            }
            else
            {
                // Page rendering does not apply to a non-paginated format, so the request is honored
                // with silence: an informational diagnostic records that it applied to nothing, and no
                // gap is emitted, so the run is not falsely degraded.
                sink.ReportDiagnostic(new ExtractionDiagnostic(
                    DiagnosticCodes.PageRenderingNotApplicable, DiagnosticSeverity.Info,
                    $"Page rendering was requested, but the '{detection.Format.Id}' format is not paginated, "
                    + "so there is no page grid to render and the request applies to nothing."));
            }
        }
        else if (options.RenderPages && sink.Pages.Count == 0)
        {
            // Rendering was possible but produced nothing; a partial-extraction gap explains the shortfall
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                DiagnosticCodes.NoPagesProduced, DiagnosticSeverity.Warning,
                "Page rendering was requested but the backend produced no pages."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Pages, "pages/", GapScope.PartiallyExtracted,
                "Page rendering was requested and supported, but the backend produced no pages.",
                Impact: "Rendered page images are not available."));
        }
    }

    /// <summary>
    ///     Emits the render-unavailable gap and diagnostics, naming the unavailable backends that could
    ///     have rendered pages.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="selected">The selected extractor descriptor.</param>
    /// <param name="detection">The detected format, used to filter alternate backends by format.</param>
    /// <param name="candidates">All candidates, scanned for unavailable page-rendering backends.</param>
    /// <remarks>
    ///     The clause naming the unavailable-but-capable backends is the whole point: it tells the
    ///     reader precisely which backend would have delivered rendered pages and why it did not. Side
    ///     effect: records on the sink.
    /// </remarks>
    private static void EmitRenderUnavailableGap(
        ExtractionSink sink, ExtractorDescriptor selected, FormatDetection detection, IReadOnlyList<ExtractorCandidate> candidates)
    {
        // Collect the registered-but-unavailable backends that declare page rendering for this format
        var offenders = candidates
            .Where(candidate => !string.Equals(candidate.Descriptor.Id, selected.Id, StringComparison.Ordinal))
            .Where(candidate => !candidate.Availability.IsAvailable)
            .Where(candidate => candidate.Descriptor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages))
            .Where(candidate => Supports(candidate.Descriptor, detection.Format))
            .Select(candidate => $"{candidate.Descriptor.DisplayName} ({candidate.Descriptor.Id}): {ReasonOf(candidate.Availability)}")
            .ToList();

        var reason = new StringBuilder();
        reason.Append("Page rendering was requested but the selected backend '").Append(selected.Id)
            .Append("' does not provide the renderedPages capability in this environment.");
        if (offenders.Count > 0)
        {
            reason.Append(" Backends that could render pages but are unavailable: ")
                .Append(string.Join("; ", offenders)).Append('.');
        }

        sink.ReportDiagnostic(new ExtractionDiagnostic(
            DiagnosticCodes.RenderedPagesUnavailable, DiagnosticSeverity.Warning,
            "The requested renderedPages capability is unavailable in this environment."));
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            DiagnosticCodes.DegradedMissingCapability, DiagnosticSeverity.Warning,
            $"Degraded: the selected backend '{selected.Id}' lacks the requested renderedPages capability."));
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Pages, "pages/", GapScope.Unavailable, reason.ToString(),
            Impact: "Rendered page images are not available.",
            Remedy: "Run in an environment where a page-rendering backend for this format is available."));
    }

    /// <summary>
    ///     Emits the no-text gap and diagnostic for an extraction that produced no textual content.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <remarks>
    ///     A document-to-markdown extraction that yields no text is a real shortfall worth flagging, so
    ///     the absence is both diagnosed and recorded as a gap. Side effect: records on the sink.
    /// </remarks>
    private static void EmitNoTextGap(ExtractionSink sink)
    {
        // Text is the primary artifact; its absence must be explained rather than passed over
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            DiagnosticCodes.NoTextContent, DiagnosticSeverity.Warning, "The extraction produced no text content."));
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Text, "content.md", GapScope.PartiallyExtracted,
            "The extractor produced no text content.",
            Impact: "No textual content is available for downstream consumers."));
    }

    /// <summary>
    ///     Records a failed extraction's diagnostic and the failed-artifact gaps.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="failure">The structured failure whose summary explains each gap.</param>
    /// <remarks>
    ///     Records one error diagnostic plus a failed gap for text, images, and pages so a failed run's
    ///     ledger has no unexplained absence. Side effect: records on the sink.
    /// </remarks>
    private static void EmitFailureArtifacts(ExtractionSink sink, ExtractionFailure failure)
    {
        // One diagnostic captures the failure with its code; the gaps explain each missing artifact
        sink.ReportDiagnostic(new ExtractionDiagnostic(failure.Code, DiagnosticSeverity.Error, failure.Summary));
        sink.ReportGap(new ExtractionGap(string.Empty, GapKind.Text, "content.md", GapScope.Failed, failure.Summary));
        sink.ReportGap(new ExtractionGap(string.Empty, GapKind.Images, "images/", GapScope.Failed, failure.Summary));
        sink.ReportGap(new ExtractionGap(string.Empty, GapKind.Pages, "pages/", GapScope.Failed, failure.Summary));
    }

    /// <summary>
    ///     Builds an <see cref="ExtractionEnvironment"/> from the runtime plus the supplied facts.
    /// </summary>
    /// <param name="facts">The facts to attach, already in their intended order.</param>
    /// <returns>The environment description.</returns>
    /// <remarks>
    ///     The four runtime fields are read from <see cref="RuntimeInformation"/> so a degraded result
    ///     is reproducible and explicable by its platform; the facts are attached verbatim and never
    ///     re-sorted. Pure apart from reading immutable runtime information.
    /// </remarks>
    private static ExtractionEnvironment BuildEnvironment(IReadOnlyList<EnvironmentFact> facts) => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.RuntimeIdentifier,
        facts);

    /// <summary>
    ///     Builds the final environment facts: the backend's contributed facts followed by
    ///     availability-derived facts for the non-selected candidates.
    /// </summary>
    /// <param name="sink">The sink holding any facts the backend contributed.</param>
    /// <param name="candidates">All candidates; the non-selected ones contribute availability facts.</param>
    /// <param name="selectedId">The selected extractor identifier, or <see langword="null"/> when none was selected.</param>
    /// <returns>The ordered fact list.</returns>
    /// <remarks>
    ///     Emitting the backend's facts first and then what was missing lets the summary explain a
    ///     degraded result by its environment. Pure.
    /// </remarks>
    private static IReadOnlyList<EnvironmentFact> FinalFacts(
        ExtractionSink sink, IReadOnlyList<ExtractorCandidate> candidates, string? selectedId)
    {
        // Backend-reported facts come first, then the availability context for what was not selected
        var facts = new List<EnvironmentFact>(sink.EnvironmentFacts);
        facts.AddRange(AvailabilityFacts(candidates, selectedId));
        return facts;
    }

    /// <summary>
    ///     Derives environment facts describing the availability of every non-selected candidate.
    /// </summary>
    /// <param name="candidates">All candidates.</param>
    /// <param name="selectedId">The selected extractor identifier to exclude, or <see langword="null"/>.</param>
    /// <returns>One fact per non-selected candidate stating its availability.</returns>
    /// <remarks>
    ///     Recording what each unused backend could or could not do turns an opaque degradation into an
    ///     explained one. Pure.
    /// </remarks>
    private static List<EnvironmentFact> AvailabilityFacts(IReadOnlyList<ExtractorCandidate> candidates, string? selectedId)
    {
        // One fact per backend that did not run, stating whether it was available and why not
        var facts = new List<EnvironmentFact>();
        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate.Descriptor.Id, selectedId, StringComparison.Ordinal))
            {
                continue;
            }

            var availability = candidate.Availability;
            facts.Add(new EnvironmentFact(
                candidate.Descriptor.DisplayName,
                $"backend.{candidate.Descriptor.Id}",
                availability.IsAvailable ? "available" : ReasonOf(availability),
                availability.IsAvailable,
                EnvironmentFactOrigin.CandidateAvailability));
        }

        return facts;
    }

    /// <summary>
    ///     Appends a self-validating backend's cases, wrapping them to skip when the backend is unavailable.
    /// </summary>
    /// <param name="cases">The case list to append to.</param>
    /// <param name="validating">The self-validating backend contributing cases.</param>
    /// <param name="isAvailable">Whether the backend's cached probe reports it available.</param>
    /// <param name="skipReason">The reason to report when a case is skipped.</param>
    /// <remarks>
    ///     Enumerating a backend's cases is defended against a throwing implementation so one
    ///     misbehaving backend cannot break the whole suite; an unavailable backend's cases are wrapped
    ///     to return <see cref="SelfTestResult.Skipped(string)"/> without running. Side effect: appends
    ///     to <paramref name="cases"/>.
    /// </remarks>
    private static void AppendBackendCases(
        List<SelfTestCase> cases, ISelfValidating validating, bool isAvailable, string skipReason)
    {
        List<SelfTestCase> contributed;
        try
        {
            contributed = validating.GetSelfTestCases()?.ToList() ?? [];
        }
#pragma warning disable CA1031 // A backend enumerating its cases is untrusted; a fault must not break the suite
        catch (Exception)
#pragma warning restore CA1031
        {
            contributed = [];
        }

        foreach (var testCase in contributed)
        {
            // An available backend runs its cases as-is; an unavailable one has them wrapped to skip
            cases.Add(isAvailable
                ? testCase
                : testCase with { Run = _ => SelfTestResult.Skipped(skipReason) });
        }
    }

    /// <summary>
    ///     Builds the skip reason for an unavailable backend's self-test cases.
    /// </summary>
    /// <param name="extractorId">The backend identifier.</param>
    /// <param name="candidate">The backend's candidate, or <see langword="null"/> when it has no cached availability.</param>
    /// <returns>A non-empty skip reason.</returns>
    /// <remarks>Names the backend and its unavailable reason so a skip is never silent. Pure.</remarks>
    private static string BuildSkipReason(string extractorId, ExtractorCandidate? candidate)
    {
        // Surface the recorded reason when present so the skip explains itself
        var reason = candidate is null || string.IsNullOrEmpty(candidate.Availability.UnavailableReason)
            ? "unavailable in this environment"
            : candidate.Availability.UnavailableReason;
        return $"backend '{extractorId}' is {reason}";
    }

    /// <summary>
    ///     Executes a Core self-test case: runs an in-process extraction, asserts, and cleans up.
    /// </summary>
    /// <param name="context">The self-test context providing the work folder and cancellation token.</param>
    /// <param name="assert">The assertion applied to the extraction result.</param>
    /// <returns>The self-test result.</returns>
    /// <remarks>
    ///     Creates a unique case folder under the work folder, runs a trivial extraction through the
    ///     real pipeline with an in-process stub, applies the assertion, and always removes the case
    ///     folder afterwards so a self-test leaves nothing behind. Any thrown exception is reported as a
    ///     failure. Performs filesystem I/O.
    /// </remarks>
    private static SelfTestResult ExecuteCoreCase(SelfTestContext context, Func<ExtractionResult, (bool Ok, string? Message)> assert)
    {
        var stopwatch = Stopwatch.StartNew();
        var caseFolder = Path.Combine(context.WorkFolder, "core-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(caseFolder);
            var result = RunSelfExtraction(caseFolder, context.CancellationToken);
            var (ok, message) = assert(result);
            return ok
                ? SelfTestResult.Passed(stopwatch.Elapsed)
                : SelfTestResult.Failed(message ?? "the self-test assertion failed", stopwatch.Elapsed);
        }
#pragma warning disable CA1031 // A self-test contains all faults and reports them as a failed result
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed($"{exception.GetType().Name}: {exception.Message}", stopwatch.Elapsed);
        }
        finally
        {
            TryDeleteFolder(caseFolder);
        }
    }

    /// <summary>
    ///     Runs a trivial in-process extraction through the real pipeline for a self-test case.
    /// </summary>
    /// <param name="caseFolder">The case folder to place the source and scratch subfolder in.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result of the trivial run.</returns>
    /// <remarks>
    ///     Uses a dedicated stub extractor and a fresh engine so the self-test exercises the genuine
    ///     path — scratch preparation, sink, content, manifest, and summary — rather than a mock.
    ///     Blocks on the asynchronous pipeline because self-test delegates are synchronous. Performs
    ///     filesystem I/O.
    /// </remarks>
    private static ExtractionResult RunSelfExtraction(string caseFolder, CancellationToken cancellationToken)
    {
        // Write a trivial text source the stub extractor can process
        var sourcePath = Path.Combine(caseFolder, "source.txt");
        File.WriteAllText(sourcePath, "DocDown core self-test source content.");

        var scratch = Path.Combine(caseFolder, "scratch");
        var registry = new ExtractorRegistry(new Func<IDocumentExtractor>[] { static () => new InProcessSelfTestExtractor() });
        var engine = new DocDownEngine(registry, new ExtractionOptions());

        // Self-test delegates are synchronous, so block on the real asynchronous pipeline
        return engine.ExtractAsync(sourcePath, scratch, new ExtractionOptions(), cancellationToken)
            .AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Asserts that the standard output layout exists after a self-test extraction.
    /// </summary>
    /// <param name="result">The extraction result to inspect.</param>
    /// <returns>A success flag and, on failure, the reason.</returns>
    /// <remarks>Confirms the layout invariant that summary, manifest, and content are always written on success. Read-only I/O.</remarks>
    private static (bool Ok, string? Message) AssertLayout(ExtractionResult result)
    {
        // The trivial extraction must succeed and produce the always-present artifacts
        if (result.Outcome == ExtractionOutcome.Failed)
        {
            return (false, $"the self-test extraction failed: {result.Failure?.Summary}");
        }

        if (!File.Exists(result.SummaryPath))
        {
            return (false, "summary.txt was not written");
        }

        if (!File.Exists(result.ManifestPath))
        {
            return (false, "manifest.json was not written");
        }

        return File.Exists(Path.Combine(result.ScratchFolder, "content.md"))
            ? (true, null)
            : (false, "content.md was not written");
    }

    /// <summary>
    ///     Asserts that the contract verifier finds no violations after a self-test extraction.
    /// </summary>
    /// <param name="result">The extraction result to verify.</param>
    /// <returns>A success flag and, on failure, the list of violations.</returns>
    /// <remarks>Runs the same machine-checkable honesty verification a consumer would, over a genuine run. Read-only I/O.</remarks>
    private static (bool Ok, string? Message) AssertGapAccuracy(ExtractionResult result)
    {
        // An honest extraction must verify clean; any violation names a broken honesty invariant
        var violations = ContractVerifier.Verify(result.ScratchFolder);
        return violations.Count == 0
            ? (true, null)
            : (false, "contract violations: " + string.Join("; ", violations.Select(violation => $"{violation.Code} {violation.Detail}")));
    }

    /// <summary>
    ///     Asserts that the manifest is schema-valid after a self-test extraction.
    /// </summary>
    /// <param name="result">The extraction result whose manifest is parsed.</param>
    /// <returns>A success flag and, on failure, the reason.</returns>
    /// <remarks>Parses the manifest and checks the schema version and a few mandatory fields exist. Read-only I/O.</remarks>
    private static (bool Ok, string? Message) AssertManifestSchema(ExtractionResult result)
    {
        // Parse the manifest from disk and confirm the pinned schema and mandatory fields are present
        using var document = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        var root = document.RootElement;

        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "1.2")
        {
            return (false, "manifest schemaVersion is missing or unsupported");
        }

        foreach (var field in new[] { "tool", "status", "complete", "artifacts", "gaps" })
        {
            if (!root.TryGetProperty(field, out _))
            {
                return (false, $"manifest is missing the mandatory field '{field}'");
            }
        }

        return (true, null);
    }

    /// <summary>
    ///     Deletes a folder and its contents, ignoring any error.
    /// </summary>
    /// <param name="folder">The folder to delete.</param>
    /// <remarks>
    ///     A self-test must clean up after itself, but a cleanup failure must never turn a passing test
    ///     into a failing one, so all errors are swallowed. Performs filesystem I/O.
    /// </remarks>
    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
#pragma warning disable CA1031 // Cleanup is best-effort; a failure must not affect the test outcome
        catch (Exception)
#pragma warning restore CA1031
        {
            // Intentionally ignored: leftover temporary files are harmless and must not fail the test
        }
    }

    /// <summary>
    ///     Reads the entire source into a byte array via a seekable buffer.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The source bytes.</returns>
    /// <remarks>
    ///     Buffering the whole source lets Core both hash it and sniff it from a seekable stream without
    ///     assuming the source stream itself is seekable. Performs I/O.
    /// </remarks>
    private static async ValueTask<byte[]> ReadAllBytesAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        // Copy into a memory buffer so the same bytes serve hashing and format sniffing
        using var raw = source.OpenRead();
        using var buffer = new MemoryStream();
        await raw.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-256 of the source bytes.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The lowercase hex digest.</returns>
    /// <remarks>Records the source integrity hash in the manifest so a consumer can confirm provenance. Pure.</remarks>
    private static string ComputeSha(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    ///     Sniffs the format of the buffered source bytes.
    /// </summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="fileName">The source file name for the extension fallback.</param>
    /// <returns>The format detection.</returns>
    /// <remarks>Wraps the bytes in a seekable stream the sniffer requires; the stream is disposed after use. Pure over the inputs.</remarks>
    private static FormatDetection Sniff(byte[] bytes, string fileName)
    {
        // The sniffer needs a seekable stream; a read-only memory stream over the buffer suffices
        using var stream = new MemoryStream(bytes, writable: false);
        return FormatSniffer.Detect(stream, fileName);
    }

    /// <summary>
    ///     Resolves a path to its absolute form, tolerating a malformed path.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The absolute path, or the original path when it cannot be resolved.</returns>
    /// <remarks>
    ///     Used only to report the requested folder on a scratch refusal, where a best-effort absolute
    ///     path is more useful than a second exception. Pure.
    /// </remarks>
    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            // A malformed path cannot be normalized; report it verbatim rather than throwing again
            return path;
        }
    }

    /// <summary>
    ///     Determines whether an exception represents a source-read I/O fault.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> when the exception is a recognized I/O or access fault.</returns>
    /// <remarks>
    ///     Narrows the caught exceptions so a genuine programming error is not misreported as an
    ///     unreadable source. Pure.
    /// </remarks>
    private static bool IsIoFault(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException;

    /// <summary>
    ///     Reads an availability's unavailable reason, substituting a stable fallback when absent.
    /// </summary>
    /// <param name="availability">The availability to read.</param>
    /// <returns>The recorded reason, or a generic fallback.</returns>
    /// <remarks>Keeps the reason non-empty so it is always displayable in facts and gaps. Pure.</remarks>
    private static string ReasonOf(ExtractorAvailability availability) =>
        string.IsNullOrEmpty(availability.UnavailableReason)
            ? "unavailable in this environment"
            : availability.UnavailableReason;

    /// <summary>
    ///     Determines whether an extractor supports the given format by identifier.
    /// </summary>
    /// <param name="descriptor">The extractor descriptor.</param>
    /// <param name="format">The detected format.</param>
    /// <returns><see langword="true"/> when the descriptor lists a format with a matching identifier.</returns>
    /// <remarks>Matches on the format identifier so a same-identifier custom format still matches. Pure.</remarks>
    private static bool Supports(ExtractorDescriptor descriptor, DocumentFormat format) =>
        descriptor.SupportedFormats.Any(supported => string.Equals(supported.Id, format.Id, StringComparison.Ordinal));

    /// <summary>
    ///     Builds a simple, candidate-free selection failure with a displayable explanation.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    /// <param name="code">The fixed diagnostic code.</param>
    /// <param name="summary">The one-line headline.</param>
    /// <param name="detection">The detected format named in the explanation.</param>
    /// <param name="remedy">A suggested remedy, or <see langword="null"/>.</param>
    /// <returns>The composed failure.</returns>
    /// <remarks>Used for failures that occur before candidate ranking, so there are no candidate verdicts to render. Pure.</remarks>
    private static ExtractionFailure MakeSimpleFailure(
        ExtractionFailureKind kind, string code, string summary, FormatDetection detection, string? remedy)
    {
        // Compose a headline plus detected format, and a remedy line when one exists
        var builder = new StringBuilder();
        builder.Append(summary).Append('\n');
        builder.Append("Detected format: ").Append(detection.Describe()).Append('\n');
        if (!string.IsNullOrEmpty(remedy))
        {
            builder.Append('\n').Append("Remedy: ").Append(remedy).Append('\n');
        }

        return new ExtractionFailure(kind, code, summary, builder.ToString().TrimEnd('\n'), [], remedy);
    }

    /// <summary>
    ///     Builds the failure for a backend that threw during extraction.
    /// </summary>
    /// <param name="selected">The selected extractor descriptor.</param>
    /// <param name="exception">The exception the backend threw.</param>
    /// <param name="detection">The detected format named in the explanation.</param>
    /// <param name="trace">The selection trace to carry as the failure's candidate list.</param>
    /// <returns>The composed failure carrying the exception type and message.</returns>
    /// <remarks>Records the exception type and message so a backend fault is diagnosable from the output alone. Pure.</remarks>
    private static ExtractionFailure MakeExtractorFailure(
        ExtractorDescriptor selected, Exception exception, FormatDetection detection, IReadOnlyList<CandidateVerdict> trace)
    {
        var summary = $"The selected extractor '{selected.Id}' failed while extracting.";
        var builder = new StringBuilder();
        builder.Append(summary).Append('\n');
        builder.Append("Detected format: ").Append(detection.Describe()).Append('\n');
        builder.Append('\n').Append("The backend threw ").Append(exception.GetType().Name)
            .Append(": ").Append(exception.Message).Append('\n');
        return new ExtractionFailure(
            ExtractionFailureKind.ExtractorFailed, DiagnosticCodes.ExtractorFailed, summary,
            builder.ToString().TrimEnd('\n'), trace, null);
    }

    /// <summary>
    ///     Creates the placeholder detection used when no real detection is available.
    /// </summary>
    /// <returns>An unknown detection with zero confidence.</returns>
    /// <remarks>Used for scratch-refusal and unreadable-source failures, which fail before sniffing. Pure.</remarks>
    private static FormatDetection UnknownDetection() =>
        new(DocumentFormat.Unknown, DetectionBasis.Extension, 0.0);

    /// <summary>
    ///     Creates an empty selection carrying a failure for a pre-ranking failure.
    /// </summary>
    /// <param name="mode">The selection mode implied by the options.</param>
    /// <param name="failure">The failure to attach.</param>
    /// <returns>A selection with no winner, no required or satisfied capabilities, and an empty trace.</returns>
    /// <remarks>Gives the manifest and summary a consistent selection object even when ranking never ran. Pure.</remarks>
    private static SelectionResult EmptySelection(SelectionMode mode, ExtractionFailure failure) =>
        new(null, mode, ExtractorCapabilities.None, ExtractorCapabilities.None, [], failure);

    /// <summary>
    ///     Builds an all-absent ledger for a run that wrote no layout.
    /// </summary>
    /// <returns>A ledger marking every artifact absent.</returns>
    /// <remarks>Used only for a scratch refusal, where nothing was written; keeps the result's ledger non-null. Pure.</remarks>
    private static ArtifactLedger AbsentLedger() => new(
        new ArtifactEntry("summary.txt", ArtifactStatus.Absent),
        new ArtifactEntry("manifest.json", ArtifactStatus.Absent),
        new ArtifactEntry("metadata.json", ArtifactStatus.Absent),
        new ArtifactEntry("content.md", ArtifactStatus.Absent),
        new ArtifactEntry("images/", ArtifactStatus.Absent, 0, 0),
        new ArtifactEntry("pages/", ArtifactStatus.Absent, 0, 0));

    /// <summary>
    ///     The immutable bundle of per-run inputs shared by the pipeline's finalization helpers.
    /// </summary>
    /// <param name="Source">The source document.</param>
    /// <param name="Sha">The source SHA-256, or <see langword="null"/> when it could not be computed.</param>
    /// <param name="Detection">The detected format.</param>
    /// <param name="Options">The already-cloned effective options.</param>
    /// <param name="Timestamp">The resolved extraction timestamp.</param>
    /// <param name="Candidates">The candidates with cached availability.</param>
    /// <remarks>
    ///     Bundling these values keeps the finalization helpers' signatures small and their inputs
    ///     identical, so failure and success paths cannot diverge on what they record. Immutable and
    ///     thread-safe.
    /// </remarks>
    private sealed record PipelineInputs(
        DocumentSource Source, string? Sha, FormatDetection Detection,
        ExtractionOptions Options, DateTimeOffset Timestamp, IReadOnlyList<ExtractorCandidate> Candidates);

    /// <summary>
    ///     A minimal in-process extractor that Core's own self-tests run against.
    /// </summary>
    /// <remarks>
    ///     Deliberately trivial and dependency-free: it writes a small block of markdown so a self-test
    ///     exercises the genuine pipeline (sink, content, manifest, summary, and contract verification)
    ///     without needing any real backend. It is always available and supports only plain text.
    ///     Stateless and safe to construct per self-test run.
    /// </remarks>
    private sealed class InProcessSelfTestExtractor : IDocumentExtractor
    {
        /// <summary>The single format this stub supports.</summary>
        /// <remarks>Plain text so a trivial <c>.txt</c> source routes to this extractor.</remarks>
        private static readonly DocumentFormat[] SupportedFormatsValue = [DocumentFormat.Text];

        /// <inheritdoc />
        public string Id => "core.selftest";

        /// <inheritdoc />
        public string DisplayName => "DocDown Core self-test extractor";

        /// <inheritdoc />
        public IReadOnlyCollection<DocumentFormat> SupportedFormats => SupportedFormatsValue;

        /// <inheritdoc />
        public ExtractorCapabilities Capabilities => ExtractorCapabilities.Text;

        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public ExtractorAvailability ProbeAvailability() =>
            ExtractorAvailability.Available(ExtractorCapabilities.Text);

        /// <inheritdoc />
        public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
        {
            // Write a trivial, deterministic block so the self-test produces genuine, verifiable content
            await context.Sink.WriteContentAsync(
                "# DocDown Core Self-Test\n\nThis content validates the extraction contract.\n",
                context.CancellationToken).ConfigureAwait(false);
            return ExtractionOutcome.Succeeded;
        }
    }
}
