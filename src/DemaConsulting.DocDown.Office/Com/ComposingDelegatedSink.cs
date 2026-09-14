using DocDown.Core;

namespace DocDown.Office.Com;

/// <summary>
///     An <see cref="IExtractionSink"/> wrapper that composes the delegated managed backend's output
///     into a COM run: it drops the managed backend's "rendering not provided" environment fact,
///     because under COM the slides the managed backend could not render are in fact rendered by this
///     backend.
/// </summary>
/// <remarks>
///     <para>
///         The COM extractor runs the managed Open XML backend against the same sink to reuse its
///         slide-text, speaker-notes, and embedded-image extraction, then renders each slide itself.
///         The managed backend, unaware that a rendering host is composing its output, unconditionally
///         reports <c>&lt;backend&gt;.pageRendering : not provided by this extractor (NOT available)</c>. In
///         a successful COM run that statement is false: rendering was performed. This wrapper
///         suppresses that one contradictory fact, forwarding everything else — content, images,
///         notes, and inventory — verbatim, so the composed report is internally consistent.
///     </para>
///     <para>
///         The filter is deliberately narrow — it suppresses only the exact
///         the managed backend's page-rendering fact when it is reported unavailable — so no other honest
///         fact or note is lost. The COM extractor emits its own authoritative
///         <c>pages.renderer : available</c> fact on the real sink, which this wrapper never touches.
///         Used from the single extraction thread; not required to be thread-safe.
///     </para>
/// </remarks>
internal sealed class ComposingDelegatedSink : IExtractionSink
{
    /// <summary>The real sink every forwarded call reaches.</summary>
    private readonly IExtractionSink _inner;

    /// <summary>The environment-fact key whose unavailable statement this sink suppresses.</summary>
    private readonly string _pageRenderingFactKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposingDelegatedSink"/> class.
    /// </summary>
    /// <param name="inner">The real sink to forward to. Must not be null.</param>
    /// <param name="pageRenderingFactKey">
    ///     The environment-fact key the delegated managed backend uses for its rendering-capability
    ///     statement, for example the managed backend's page-rendering. Must not be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pageRenderingFactKey"/> is null or empty.</exception>
    /// <remarks>
    ///     The key is supplied rather than hard-coded because each managed backend names its own fact.
    ///     That is the only difference between what were two copies of this class.
    /// </remarks>
    public ComposingDelegatedSink(IExtractionSink inner, string pageRenderingFactKey)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrEmpty(pageRenderingFactKey);
        _inner = inner;
        _pageRenderingFactKey = pageRenderingFactKey;
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
    public void ReportNote(ExtractionNote note) => _inner.ReportNote(note);

    /// <inheritdoc />
    /// <remarks>
    ///     Suppresses the managed backend's the managed backend's page-rendering "not available" fact,
    ///     which contradicts the rendering this COM run performed; forwards every other fact
    ///     unchanged.
    /// </remarks>
    public void ReportEnvironmentFact(EnvironmentFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (string.Equals(fact.Key, _pageRenderingFactKey, StringComparison.Ordinal) && fact.Available == false)
        {
            return;
        }

        _inner.ReportEnvironmentFact(fact);
    }

    /// <inheritdoc />
    public void ReportContentFeature(ContentFeature feature) => _inner.ReportContentFeature(feature);
}
