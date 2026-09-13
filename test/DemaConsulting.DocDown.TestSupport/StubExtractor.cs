using DocDown.Core;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     The availability behavior a <see cref="StubExtractor"/> exhibits when probed.
/// </summary>
/// <remarks>
///     Lets a test drive every branch of the engine's availability handling — a normally
///     available backend, one that reports itself unavailable with a reason, and one whose probe
///     throws (which Core must contain and treat as unavailable).
/// </remarks>
public enum StubAvailabilityMode
{
    /// <summary>The extractor is available.</summary>
    Available,

    /// <summary>The extractor reports itself unavailable with a reason.</summary>
    Unavailable,

    /// <summary>The extractor's availability probe throws, which Core must contain.</summary>
    ThrowingProbe
}

/// <summary>
///     A configurable, framework-agnostic <see cref="IDocumentExtractor"/> for driving the engine
///     through every extraction scenario, including its self-validation seam.
/// </summary>
/// <remarks>
///     <para>
///         Identity, formats, priority, page-rendering support, and availability are all set through
///         init-only properties, and the extraction body is a scripted delegate so a test can make
///         the stub write text, add images (including identical bytes to exercise deduplication), add
///         pages and parts, report document info, notes, and environment facts, return an
///         <see cref="ExtractionOutcome"/>, or throw. The static factories cover the common shapes;
///         the <see cref="ExtractBehavior"/> hook covers the rest.
///     </para>
///     <para>
///         The <see cref="SelfTestCases"/> collection is mutable so a test can attach cases and
///         observe how the engine surfaces them — running them for an available backend and
///         wrapping them as skipped for an unavailable one. Instances are intended to be
///         configured once and used from a single extraction thread.
///     </para>
/// </remarks>
public sealed class StubExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>Gets the extractor's stable, unique identifier.</summary>
    /// <remarks>Defaults to <c>stub</c>; override per test when several stubs coexist.</remarks>
    public string Id { get; init; } = "stub";

    /// <summary>Gets the extractor's human-readable name.</summary>
    /// <remarks>Defaults to a name derived from the identifier so summaries read clearly.</remarks>
    public string DisplayName { get; init; } = "Stub Extractor";

    /// <summary>Gets the document formats this extractor supports.</summary>
    /// <remarks>Defaults to plain text, the one format Core can exercise end to end without a real backend.</remarks>
    public IReadOnlyCollection<DocumentFormat> SupportedFormats { get; init; } = [DocumentFormat.Text];

    /// <summary>
    ///     Gets a value indicating whether this stub can render document pages to images when
    ///     available.
    /// </summary>
    /// <remarks>
    ///     Set to <see langword="true"/> to model a page-rendering backend, which selection prefers
    ///     when the caller requested pages. Defaults to <see langword="false"/> (a managed backend
    ///     that only extracts text). Ignored when the stub is unavailable.
    /// </remarks>
    public bool ProvidesRenderedPages { get; init; }

    /// <summary>Gets the extractor's ranking priority; higher wins when more than one candidate remains.</summary>
    /// <remarks>Used to model ordering between two stubs for the same format.</remarks>
    public int Priority { get; init; }

    /// <summary>Gets a value indicating whether page rendering is applicable to this extractor's format.</summary>
    /// <remarks>
    ///     Defaults to <see langword="true"/> (a paginated format). Set to <see langword="false"/> to
    ///     model a non-paginated format such as a spreadsheet, so a test can prove a page request is
    ///     honored with silence rather than a note.
    /// </remarks>
    public bool PageRenderingApplicable { get; init; } = true;

    /// <summary>Gets the availability behavior the stub exhibits when probed.</summary>
    /// <remarks>Selects one of the three availability branches the engine must handle.</remarks>
    public StubAvailabilityMode AvailabilityMode { get; init; } = StubAvailabilityMode.Available;

    /// <summary>Gets the reason reported when <see cref="AvailabilityMode"/> is unavailable.</summary>
    /// <remarks>Surfaced verbatim in the environment facts and summary, so tests can assert on it.</remarks>
    public string UnavailableReason { get; init; } = "the stub extractor is unavailable in this environment";

    /// <summary>
    ///     Gets the scripted extraction body, or <see langword="null"/> to write a small block of
    ///     default text and produce output.
    /// </summary>
    /// <remarks>
    ///     The delegate receives the source and the context and returns the outcome, so it can drive
    ///     the sink through any sequence of writes and reports the scenario requires.
    /// </remarks>
    public Func<DocumentSource, IExtractionContext, ValueTask<ExtractionOutcome>>? ExtractBehavior { get; init; }

    /// <summary>Gets the mutable self-test cases this extractor contributes.</summary>
    /// <remarks>
    ///     Exposed as a mutable list so a test can attach cases and observe the engine running them
    ///     (available backend) or wrapping them as skipped (unavailable backend).
    /// </remarks>
    public IList<SelfTestCase> SelfTestCases { get; } = [];

    /// <inheritdoc/>
    public ExtractorAvailability ProbeAvailability() => AvailabilityMode switch
    {
        StubAvailabilityMode.ThrowingProbe =>
            throw new InvalidOperationException("stub availability probe deliberately threw"),
        StubAvailabilityMode.Unavailable =>
            ExtractorAvailability.Unavailable(UnavailableReason),
        _ => ExtractorAvailability.Available(ProvidesRenderedPages)
    };

    /// <inheritdoc/>
    public ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Delegate to the scripted body when one is configured; otherwise write default text
        return ExtractBehavior is not null
            ? ExtractBehavior(source, context)
            : WriteDefaultTextAsync(context);
    }

    /// <inheritdoc/>
    public IEnumerable<SelfTestCase> GetSelfTestCases() => SelfTestCases;

    /// <summary>
    ///     Creates an available stub for the given formats that writes default text.
    /// </summary>
    /// <param name="id">The extractor identifier. Must not be null or empty.</param>
    /// <param name="formats">The supported formats. Must not be null.</param>
    /// <param name="providesRenderedPages">Whether the stub can render pages here. Defaults to <see langword="false"/>.</param>
    /// <returns>A configured available <see cref="StubExtractor"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <remarks>The default extraction body writes a small text document and produces output.</remarks>
    public static StubExtractor Available(
        string id, IReadOnlyCollection<DocumentFormat> formats, bool providesRenderedPages = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(formats);

        return new StubExtractor
        {
            Id = id,
            DisplayName = id + " (stub)",
            SupportedFormats = formats,
            ProvidesRenderedPages = providesRenderedPages,
            AvailabilityMode = StubAvailabilityMode.Available
        };
    }

    /// <summary>
    ///     Creates a stub that reports itself unavailable with the given reason.
    /// </summary>
    /// <param name="id">The extractor identifier. Must not be null or empty.</param>
    /// <param name="formats">The supported formats. Must not be null.</param>
    /// <param name="reason">The unavailability reason. Must not be null or empty.</param>
    /// <returns>A configured unavailable <see cref="StubExtractor"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="reason"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <remarks>The reason is surfaced verbatim in the environment facts and summary.</remarks>
    public static StubExtractor Unavailable(
        string id, IReadOnlyCollection<DocumentFormat> formats, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return new StubExtractor
        {
            Id = id,
            DisplayName = id + " (stub)",
            SupportedFormats = formats,
            ProvidesRenderedPages = true,
            AvailabilityMode = StubAvailabilityMode.Unavailable,
            UnavailableReason = reason
        };
    }

    /// <summary>
    ///     Creates a stub whose availability probe throws, which Core must contain as unavailable.
    /// </summary>
    /// <param name="id">The extractor identifier. Must not be null or empty.</param>
    /// <param name="formats">The supported formats. Must not be null.</param>
    /// <returns>A configured throwing-probe <see cref="StubExtractor"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <remarks>Used to prove the engine defensively treats a thrown probe as unavailable.</remarks>
    public static StubExtractor ThrowingProbe(string id, IReadOnlyCollection<DocumentFormat> formats)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(formats);

        return new StubExtractor
        {
            Id = id,
            DisplayName = id + " (stub)",
            SupportedFormats = formats,
            AvailabilityMode = StubAvailabilityMode.ThrowingProbe
        };
    }

    /// <summary>
    ///     Creates an available stub whose extraction body throws, which Core must convert into a
    ///     prose failure.
    /// </summary>
    /// <param name="id">The extractor identifier. Must not be null or empty.</param>
    /// <param name="formats">The supported formats. Must not be null.</param>
    /// <returns>A configured failing <see cref="StubExtractor"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <remarks>Proves a backend exception becomes an unreadable result with the layout still written.</remarks>
    public static StubExtractor Failing(string id, IReadOnlyCollection<DocumentFormat> formats)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(formats);

        return new StubExtractor
        {
            Id = id,
            DisplayName = id + " (stub)",
            SupportedFormats = formats,
            AvailabilityMode = StubAvailabilityMode.Available,
            ExtractBehavior = FailingBehaviorAsync
        };
    }

    /// <summary>
    ///     Writes a small default text document and produces output.
    /// </summary>
    /// <param name="context">The extraction context to write through.</param>
    /// <returns>An outcome of <see cref="ExtractionOutcome.Produced"/>.</returns>
    /// <remarks>The default body when no scripted behavior is configured.</remarks>
    private static async ValueTask<ExtractionOutcome> WriteDefaultTextAsync(IExtractionContext context)
    {
        await context.Sink.WriteContentAsync(
            "# Stub Document\n\nThis content was produced by the stub extractor.\n",
            context.CancellationToken).ConfigureAwait(false);
        return ExtractionOutcome.Produced;
    }

    /// <summary>
    ///     Throws to model a backend that fails partway through extraction.
    /// </summary>
    /// <param name="source">The source document (unused).</param>
    /// <param name="context">The extraction context (unused).</param>
    /// <returns>This method never returns normally.</returns>
    /// <exception cref="InvalidOperationException">Always thrown to simulate a backend failure.</exception>
    /// <remarks>The engine catches this and records an unreadable result with a prose failure.</remarks>
    private static ValueTask<ExtractionOutcome> FailingBehaviorAsync(DocumentSource source, IExtractionContext context) =>
        throw new InvalidOperationException("the stub extractor deliberately failed");
}
