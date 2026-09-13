using DocDown.Core;

namespace DocDown.Visio.Com;

/// <summary>
///     An <see cref="IExtractionSink"/> wrapper that composes the delegated managed backend's output
///     into a COM run: it drops the managed backend's "rendering not provided" environment fact,
///     because under COM the pages the managed backend could not render are in fact rendered by this
///     backend.
/// </summary>
/// <remarks>
///     <para>
///         The COM extractor runs the managed Open Packaging backend against the same sink to reuse
///         its topology and embedded-image extraction, then renders each page itself. The managed
///         backend, unaware that a rendering host is composing its output, unconditionally reports
///         <c>visio.pageRendering : not provided by this extractor (NOT available)</c>. In a
///         successful COM run that statement is false: rendering was performed. This wrapper
///         suppresses that one contradictory fact, forwarding everything else — content, images, gaps,
///         and diagnostics — verbatim, so the composed report is internally consistent.
///     </para>
///     <para>
///         The filter is deliberately narrow — it suppresses only the exact
///         <c>visio.pageRendering</c> fact when it is reported unavailable — so no other honest fact
///         or gap is lost. The COM extractor emits its own authoritative
///         <c>pages.renderer : available</c> fact on the real sink, which this wrapper never touches.
///         Used from the single extraction thread; not required to be thread-safe.
///     </para>
/// </remarks>
internal sealed class ComposingDelegatedSink : IExtractionSink
{
    /// <summary>The environment-fact key the managed backend uses for its rendering-capability statement.</summary>
    private const string PageRenderingFactKey = "visio.pageRendering";

    /// <summary>The real sink every forwarded call reaches.</summary>
    private readonly IExtractionSink _inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposingDelegatedSink"/> class.
    /// </summary>
    /// <param name="inner">The real sink to forward to. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    public ComposingDelegatedSink(IExtractionSink inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public ValueTask<string> AddImageAsync(Stream content, ImageHint hint, CancellationToken cancellationToken) =>
        _inner.AddImageAsync(content, hint, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> AddPageAsync(int pageNumber, Stream pngContent, CancellationToken cancellationToken) =>
        _inner.AddPageAsync(pageNumber, pngContent, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteContentAsync(string markdown, CancellationToken cancellationToken) =>
        _inner.WriteContentAsync(markdown, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> AddContentPartAsync(ContentPart part, string markdown, CancellationToken cancellationToken) =>
        _inner.AddContentPartAsync(part, markdown, cancellationToken);

    /// <inheritdoc />
    public void ReportDocumentInfo(DocumentInfo info) => _inner.ReportDocumentInfo(info);

    /// <inheritdoc />
    public void ReportDocumentMetadata(DocumentMetadata metadata) => _inner.ReportDocumentMetadata(metadata);

    /// <inheritdoc />
    public void ReportDiagnostic(ExtractionDiagnostic diagnostic) => _inner.ReportDiagnostic(diagnostic);

    /// <inheritdoc />
    public void ReportGap(ExtractionGap gap) => _inner.ReportGap(gap);

    /// <inheritdoc />
    /// <remarks>
    ///     Suppresses the managed backend's <c>visio.pageRendering</c> "not available" fact, which
    ///     contradicts the rendering this COM run performed; forwards every other fact unchanged.
    /// </remarks>
    public void ReportEnvironmentFact(EnvironmentFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (string.Equals(fact.Key, PageRenderingFactKey, StringComparison.Ordinal) && fact.Available == false)
        {
            return;
        }

        _inner.ReportEnvironmentFact(fact);
    }

    /// <inheritdoc />
    public void ReportContentFeature(ContentFeature feature) => _inner.ReportContentFeature(feature);

    /// <inheritdoc />
    public void ReportFound(GapKind kind, int foundCount) => _inner.ReportFound(kind, foundCount);
}
