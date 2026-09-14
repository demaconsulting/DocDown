using System.Diagnostics;
using System.Runtime.InteropServices;
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
    /// <remarks>Probe-free, so reading it never inspects the environment; use <see cref="GetBackends"/> for availability.</remarks>
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
    /// <example>
    ///     A complete extraction, from registration to reading the produced paths and notes.
    ///     <code language="csharp">
    ///     using System;
    ///     using System.Threading;
    ///     using DocDown.Core;
    ///     using DocDown.Pdf;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddPdf() // .pdf - text, embedded images, metadata
    ///         .Build();
    ///
    ///     // RenderPages asks for rasterized page images. A backend that can render pages is
    ///     // preferred when one is registered; when none is, the layout is still produced and a
    ///     // note records that pages were not rendered.
    ///     var options = new ExtractionOptions { RenderPages = true };
    ///
    ///     var result = await engine.ExtractAsync(
    ///         "invoices/sample-invoice.pdf",
    ///         "scratch/sample-invoice",
    ///         options,
    ///         CancellationToken.None);
    ///
    ///     if (result.Outcome == ExtractionOutcome.Produced)
    ///     {
    ///         Console.WriteLine(result.SummaryPath);   // absolute path to summary.txt
    ///         Console.WriteLine(result.ManifestPath);  // absolute path to manifest.json
    ///         Console.WriteLine(result.ContentPath);   // content.md, relative to the scratch folder
    ///         Console.WriteLine($"{result.ImagePaths.Count} images, {result.PagePaths.Count} pages");
    ///
    ///         foreach (var note in result.Notes)
    ///         {
    ///             Console.WriteLine($"Note: {note.Message}");
    ///         }
    ///     }
    ///     else
    ///     {
    ///         // Unreadable: the document or the scratch folder could not be read.
    ///         Console.Error.WriteLine(result.Failure?.Explanation);
    ///     }
    ///     </code>
    /// </example>
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
        var timestamp = DateTimeOffset.UtcNow;

        // Step 3: resolve the scratch folder; a refusal is the one failure with no writable layout
        ScratchFolder folder;
        try
        {
            folder = ScratchFolder.Prepare(scratchFolder, effective.ScratchFolder);
        }
        catch (ScratchFolderException exception)
        {
            return BuildScratchRefusedResult(scratchFolder, exception);
        }

        return await RunPipelineAsync(folder, source, effective, timestamp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the registered backends with their current cached availability.
    /// </summary>
    /// <returns>One candidate (descriptor plus availability) per registered extractor, in registration order.</returns>
    /// <remarks>
    ///     Uses the registry's cached availability, so it reflects the last probe pass without opening
    ///     any document. Useful for diagnosing a deployment — for example seeing a backend present but
    ///     unavailable and the reason — without triggering an extraction.
    /// </remarks>
    public IReadOnlyList<ExtractorCandidate> GetBackends() => _registry.GetCandidates();

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
    ///     The self-test cases: two Core cases in category <c>core</c>, then the union of the cases
    ///     contributed by each registered <see cref="ISelfValidating"/> extractor, in registration
    ///     order.
    /// </returns>
    /// <remarks>
    ///     Core's two cases genuinely exercise the contract (layout invariance and manifest schema
    ///     validity) by running an in-process extraction and cleaning up after themselves. An
    ///     extractor whose cached probe reports unavailable has its cases wrapped so they return
    ///     <see cref="SelfTestResult.Skipped(string)"/> without running, so a case that cannot run in
    ///     this environment is never mistaken for a pass.
    /// </remarks>
    public IReadOnlyList<SelfTestCase> GetSelfTestCases()
    {
        // Core's own cases run first and actually execute the pipeline
        var cases = new List<SelfTestCase>
        {
            new("core.layout-invariance", "core", context => ExecuteCoreCase(context, AssertLayout)),
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
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result for this run.</returns>
    /// <remarks>
    ///     Kept separate from the entry method so the pipeline body — read, sniff, select, invoke — is
    ///     readable and each failure branch writes the full layout through one shared path. Performs
    ///     filesystem I/O.
    /// </remarks>
    private async ValueTask<ExtractionResult> RunPipelineAsync(
        ScratchFolder folder, DocumentSource source, ExtractionOptions options,
        DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        // The sink is the sole write path; create it early so every failure branch can write the layout
        var sink = new ExtractionSink(folder, options);
        var candidates = _registry.GetCandidates();

        // Step 4: read the source fully so it can be sniffed from a seekable buffer
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
            var failure = MakeSimpleFailure($"The source document '{source.FileName}' could not be read.", UnknownDetection());
            var inputs = new PipelineInputs(source, UnknownDetection(), options, timestamp, candidates);
            return await WriteFailureAsync(folder, sink, inputs, null, failure, cancellationToken).ConfigureAwait(false);
        }

        var detection = Sniff(bytes, source.FileName);
        var baseInputs = new PipelineInputs(source, detection, options, timestamp, candidates);

        // Step 5: an unrecognized format cannot be routed to any backend
        if (detection.Format.IsUnknown)
        {
            var failure = MakeSimpleFailure($"The format of '{source.FileName}' could not be recognized.", detection);
            return await WriteFailureAsync(folder, sink, baseInputs, null, failure, cancellationToken).ConfigureAwait(false);
        }

        // Step 6: choose a backend; a selection failure carries prose naming the format and its package
        var selected = ExtractorSelector.Select(detection, options, candidates, out var selectionFailure);
        if (selectionFailure is not null)
        {
            return await WriteFailureAsync(folder, sink, baseInputs, null, selectionFailure, cancellationToken).ConfigureAwait(false);
        }

        // Step 7 onward: invoke the backend and finalize the produced layout
        return await RunExtractionAsync(folder, sink, baseInputs, selected!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Invokes the selected backend and finalizes the produced output layout.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink the backend writes through.</param>
    /// <param name="inputs">The gathered pipeline inputs (source, hash, detection, options, timestamp, candidates).</param>
    /// <param name="selected">The selected extractor descriptor that will run.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The extraction result for this run.</returns>
    /// <remarks>
    ///     The backend receives only the <see cref="DocumentSource"/> and an
    ///     <see cref="IExtractionContext"/>; it never learns the scratch path. A thrown exception
    ///     (other than <see cref="OperationCanceledException"/>, which propagates) is contained and
    ///     converted into an unreadable result with a prose failure, with the full layout still
    ///     written. Performs filesystem I/O.
    /// </remarks>
    private async ValueTask<ExtractionResult> RunExtractionAsync(
        ScratchFolder folder, ExtractionSink sink, PipelineInputs inputs, ExtractorDescriptor selected, CancellationToken cancellationToken)
    {
        var selectedCandidate = inputs.Candidates.First(
            candidate => string.Equals(candidate.Descriptor.Id, selected.Id, StringComparison.Ordinal));
        var providesRenderedPages = selectedCandidate.Availability.ProvidesRenderedPages;

        // Build the context the backend sees; it exposes the sink and options but no output path
        var baseEnvironment = BuildEnvironment(AvailabilityFacts(inputs.Candidates, selected.Id));
        var context = new ExtractionContext(inputs.Options, sink, inputs.Detection, selected, baseEnvironment, cancellationToken);
        var extractor = _registry.Resolve(selected.Id);

        try
        {
            // The backend's returned outcome is informational; the engine derives the outcome from
            // whether a failure occurred, so the value is deliberately discarded
            _ = await extractor.ExtractAsync(inputs.Source, context).ConfigureAwait(false);
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
            var failure = MakeExtractorFailure(selected, exception, inputs.Detection);
            return await WriteFailureAsync(folder, sink, inputs, selected, failure, cancellationToken).ConfigureAwait(false);
        }

        // Step 8: record the notes Core knows about that the backend cannot report itself
        EmitCoreDerivedNotes(sink, inputs.Options, selected, providesRenderedPages, inputs.Detection);

        // Step 9: finalize content, then serialize the manifest and summary
        var content = await ContentWriter.WriteAsync(
            sink, sink.DocumentInfo?.Title, cancellationToken).ConfigureAwait(false);

        var environment = BuildEnvironment(FinalFacts(sink, inputs.Candidates, selected.Id));
        var report = new ExtractionReport(
            ExtractionOutcome.Produced, inputs.Source, inputs.Detection, selected,
            environment, inputs.Options, inputs.Timestamp, null);
        return await ProduceResultAsync(folder, sink, report, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes the full layout for an unreadable extraction and builds its result.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink to record through.</param>
    /// <param name="inputs">The gathered pipeline inputs.</param>
    /// <param name="selected">The selected extractor descriptor, or <see langword="null"/> when none was selected.</param>
    /// <param name="failure">The structured failure to record and serialize.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The unreadable extraction result, with the full layout written.</returns>
    /// <remarks>
    ///     Serializes the manifest and summary so even an unreadable run produces a self-describing
    ///     layout carrying the prose failure. Content is not written, so <c>content.md</c> is honestly
    ///     absent. Performs filesystem I/O.
    /// </remarks>
    private static async ValueTask<ExtractionResult> WriteFailureAsync(
        ScratchFolder folder, ExtractionSink sink, PipelineInputs inputs,
        ExtractorDescriptor? selected, ExtractionFailure failure, CancellationToken cancellationToken)
    {
        var environment = BuildEnvironment(FinalFacts(sink, inputs.Candidates, selected?.Id));
        var report = new ExtractionReport(
            ExtractionOutcome.Unreadable, inputs.Source, inputs.Detection, selected,
            environment, inputs.Options, inputs.Timestamp, failure);
        return await ProduceResultAsync(folder, sink, report, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resolves the outcome, serializes the manifest and summary, and builds the result.
    /// </summary>
    /// <param name="folder">The prepared scratch folder.</param>
    /// <param name="sink">The sink holding the recorded content.</param>
    /// <param name="report">The report with a provisional outcome; the final outcome is resolved here.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The assembled extraction result.</returns>
    /// <remarks>
    ///     The outcome is resolved from a single fact — whether a failure was recorded: a failure is
    ///     <see cref="ExtractionOutcome.Unreadable"/>, and everything else is
    ///     <see cref="ExtractionOutcome.Produced"/>. Performs filesystem I/O.
    /// </remarks>
    private static async ValueTask<ExtractionResult> ProduceResultAsync(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
        ContentWriteResult? content, CancellationToken cancellationToken)
    {
        // The outcome is a fact about whether output exists: a failure is unreadable, anything else produced
        var outcome = report.Failure is not null ? ExtractionOutcome.Unreadable : ExtractionOutcome.Produced;
        var finalReport = report with { Outcome = outcome };

        await ManifestWriter.WriteAsync(folder, sink, finalReport, content, cancellationToken).ConfigureAwait(false);
        await MetadataWriter.WriteAsync(folder, sink, cancellationToken).ConfigureAwait(false);
        await SummaryWriter.WriteAsync(folder, sink, finalReport, content, cancellationToken).ConfigureAwait(false);

        return BuildResult(folder, sink, finalReport, content);
    }

    /// <summary>
    ///     Assembles the immutable <see cref="ExtractionResult"/> from the finalized facts.
    /// </summary>
    /// <param name="folder">The prepared scratch folder, whose absolute path anchors the result.</param>
    /// <param name="sink">The sink holding the recorded images, pages, and notes.</param>
    /// <param name="report">The final report (with resolved outcome, selected extractor, detection, environment).</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when none was written.</param>
    /// <returns>The assembled result.</returns>
    /// <remarks>
    ///     The summary and manifest paths are absolute so a caller can use them directly; the content,
    ///     image, page, and part paths stay relative to the scratch folder to match the manifest. Pure.
    /// </remarks>
    private static ExtractionResult BuildResult(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report, ContentWriteResult? content)
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
            report.SelectedExtractor,
            report.Failure,
            report.Environment,
            sink.Notes);
    }

    /// <summary>
    ///     Builds the result for a refused scratch folder, the one failure that writes no layout.
    /// </summary>
    /// <param name="scratchFolder">The originally requested scratch folder path.</param>
    /// <param name="exception">The refusal carrying the reason.</param>
    /// <returns>An unreadable result with the requested absolute path and the would-be summary and manifest paths.</returns>
    /// <remarks>
    ///     Core cannot write into a folder it refused, so this is the single case where
    ///     <c>summary.txt</c> and <c>manifest.json</c> are not produced. The result nonetheless carries
    ///     the requested absolute path and the paths the summary and manifest would have used so the
    ///     caller can report them. Pure apart from resolving the absolute path.
    /// </remarks>
    private static ExtractionResult BuildScratchRefusedResult(string scratchFolder, ScratchFolderException exception)
    {
        var requested = SafeFullPath(scratchFolder);
        var summary = $"The scratch folder '{requested}' was refused: {exception.Message}";
        var failure = MakeSimpleFailure(summary, UnknownDetection());

        return new ExtractionResult(
            ExtractionOutcome.Unreadable,
            requested,
            Path.Combine(requested, "summary.txt"),
            Path.Combine(requested, "manifest.json"),
            null,
            [],
            [],
            [],
            UnknownDetection(),
            null,
            failure,
            BuildEnvironment([]),
            []);
    }

    /// <summary>
    ///     Emits the notes Core can derive without any backend cooperation.
    /// </summary>
    /// <param name="sink">The sink to record notes through.</param>
    /// <param name="options">The effective options driving the suppression and render requests.</param>
    /// <param name="selected">The selected extractor descriptor.</param>
    /// <param name="providesRenderedPages">Whether the selected backend can render pages in this environment.</param>
    /// <param name="detection">The detected format, named in a note about an absent renderer.</param>
    /// <remarks>
    ///     Encodes the facts the engine knows that the backend does not: that embedded images were
    ///     suppressed by the caller, that page rendering was requested but no renderer was available
    ///     for a paginated format, or that a renderer ran but produced no pages. Each is a fact about
    ///     the extraction, not a grade of the document. Side effect: records on the sink.
    /// </remarks>
    private static void EmitCoreDerivedNotes(
        ExtractionSink sink, ExtractionOptions options, ExtractorDescriptor selected,
        bool providesRenderedPages, FormatDetection detection)
    {
        // Suppressed embedded images: state the caller-chosen absence plainly so it is never ambiguous
        if (!options.IncludeEmbeddedImages)
        {
            sink.ReportNote(new ExtractionNote(
                "Embedded image extraction was disabled by the caller; no images were written."));
        }

        // Page rendering applies only to a paginated format; a non-paginated one honors the request with silence
        if (options.RenderPages && selected.PageRenderingApplicable)
        {
            if (!providesRenderedPages)
            {
                sink.ReportNote(new ExtractionNote(
                    $"Page rendering was requested, but no page renderer is available for the '{detection.Format.Id}' "
                    + "format in this environment; pages were not rendered."));
            }
            else if (sink.Pages.Count == 0)
            {
                sink.ReportNote(new ExtractionNote(
                    "Page rendering was requested and a renderer was available, but no pages were produced."));
            }
        }
    }

    /// <summary>
    ///     Builds an <see cref="ExtractionEnvironment"/> from the runtime plus the supplied facts.
    /// </summary>
    /// <param name="facts">The facts to attach, already in their intended order.</param>
    /// <returns>The environment description.</returns>
    /// <remarks>
    ///     The four runtime fields are read from <see cref="RuntimeInformation"/> so an incomplete result
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
    ///     incomplete result by its environment. Pure.
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
        if (result.Outcome == ExtractionOutcome.Unreadable)
        {
            return (false, $"the self-test extraction was unreadable: {result.Failure?.Summary}");
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

        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "3.0")
        {
            return (false, "manifest schemaVersion is missing or unsupported");
        }

        foreach (var field in new[] { "tool", "status", "source", "notes" })
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
    ///     Builds a plain-language failure with a displayable explanation.
    /// </summary>
    /// <param name="summary">The one-line headline.</param>
    /// <param name="detection">The detected format named in the explanation.</param>
    /// <returns>The composed failure.</returns>
    /// <remarks>Used for failures that occur before a backend runs, carrying prose only. Pure.</remarks>
    private static ExtractionFailure MakeSimpleFailure(string summary, FormatDetection detection)
    {
        // Compose a headline plus the detected format so the failure is self-describing
        var builder = new StringBuilder();
        builder.Append(summary).Append('\n');
        builder.Append("Detected format: ").Append(detection.Describe());
        return new ExtractionFailure(summary, builder.ToString().TrimEnd('\n'));
    }

    /// <summary>
    ///     Builds the failure for a backend that threw during extraction.
    /// </summary>
    /// <param name="selected">The selected extractor descriptor.</param>
    /// <param name="exception">The exception the backend threw.</param>
    /// <param name="detection">The detected format named in the explanation.</param>
    /// <returns>The composed failure carrying the exception type and message.</returns>
    /// <remarks>Records the exception type and message so a backend fault is diagnosable from the output alone. Pure.</remarks>
    private static ExtractionFailure MakeExtractorFailure(
        ExtractorDescriptor selected, Exception exception, FormatDetection detection)
    {
        var summary = $"The selected extractor '{selected.Id}' failed while extracting.";
        var builder = new StringBuilder();
        builder.Append(summary).Append('\n');
        builder.Append("Detected format: ").Append(detection.Describe()).Append('\n');
        builder.Append('\n').Append("The backend threw ").Append(exception.GetType().Name)
            .Append(": ").Append(exception.Message);
        return new ExtractionFailure(summary, builder.ToString().TrimEnd('\n'));
    }

    /// <summary>
    ///     Creates the placeholder detection used when no real detection is available.
    /// </summary>
    /// <returns>An unknown detection.</returns>
    /// <remarks>Used for scratch-refusal and unreadable-source failures, which fail before sniffing. Pure.</remarks>
    private static FormatDetection UnknownDetection() =>
        new(DocumentFormat.Unknown, DetectionBasis.Extension);

    /// <summary>
    ///     The immutable bundle of per-run inputs shared by the pipeline's finalization helpers.
    /// </summary>
    /// <param name="Source">The source document.</param>
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
        DocumentSource Source, FormatDetection Detection,
        ExtractionOptions Options, DateTimeOffset Timestamp, IReadOnlyList<ExtractorCandidate> Candidates);

    /// <summary>
    ///     A minimal in-process extractor that Core's own self-tests run against.
    /// </summary>
    /// <remarks>
    ///     Deliberately trivial and dependency-free: it writes a small block of markdown so a self-test
    ///     exercises the genuine pipeline (sink, content, manifest, and summary) without needing any
    ///     real backend. It is always available and supports only plain text. Stateless and safe to
    ///     construct per self-test run.
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
        public int Priority => 0;

        /// <inheritdoc />
        public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available();

        /// <inheritdoc />
        public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
        {
            // Write a trivial, deterministic block so the self-test produces genuine, verifiable content
            await context.Sink.WriteContentAsync(
                "# DocDown Core Self-Test\n\nThis content validates the extraction contract.\n",
                context.CancellationToken).ConfigureAwait(false);
            return ExtractionOutcome.Produced;
        }
    }
}
