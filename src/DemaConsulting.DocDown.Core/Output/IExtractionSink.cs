namespace DocDown.Core;

/// <summary>
///     The write-only channel through which an extractor emits all output during an extraction.
/// </summary>
/// <remarks>
///     <para>
///         The sink is the load-bearing boundary of the library's path-safety design: extractors
///         hand Core content and receive back the relative, forward-slash-separated path they
///         must use in markdown links. Extractors never construct a path, never see a
///         <see cref="System.IO"/> output API, and never learn the absolute scratch location, so
///         Core alone controls naming, containment, and deduplication.
///     </para>
///     <para>
///         The <c>Add*</c> methods return the allocated relative path (for example
///         <c>images/0001-logo.png</c>). Ordinals and gap identifiers passed in are advisory: Core
///         allocates the real, dense values. Implementations are used from the single extraction
///         thread and are not required to be thread-safe.
///     </para>
/// </remarks>
public interface IExtractionSink
{
    /// <summary>
    ///     Adds an embedded image and returns the relative path to reference it by.
    /// </summary>
    /// <param name="content">The image bytes. The sink reads but does not take ownership of the stream.</param>
    /// <param name="hint">Advisory naming and provenance information for the image.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A task yielding the relative, forward-slash-separated path (for example
    ///     <c>images/0001-logo.png</c>) the extractor must use in markdown links.
    /// </returns>
    /// <remarks>
    ///     Core allocates the ordinal, slug, and extension and deduplicates identical bytes by
    ///     SHA-256, returning the existing path for a repeat so the same image is stored once.
    /// </remarks>
    ValueTask<string> AddImageAsync(Stream content, ImageHint hint, CancellationToken cancellationToken);

    /// <summary>
    ///     Adds a rendered page image and returns the relative path to reference it by.
    /// </summary>
    /// <param name="pageNumber">The 1-based document page number the image renders.</param>
    /// <param name="pngContent">The PNG bytes of the rendered page. The sink reads but does not own the stream.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A task yielding the relative path (for example <c>pages/page0001.png</c>) for the
    ///     rendered page.
    /// </returns>
    /// <remarks>
    ///     The page file is named from the document page number so the mapping from output file to
    ///     source page is unambiguous.
    /// </remarks>
    ValueTask<string> AddPageAsync(int pageNumber, Stream pngContent, CancellationToken cancellationToken);

    /// <summary>
    ///     Writes the single-flow markdown content for the document.
    /// </summary>
    /// <param name="markdown">The markdown content to write.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the content has been accepted.</returns>
    /// <remarks>
    ///     Used when the document is emitted as one flow; for split content the extractor calls
    ///     <see cref="AddContentPartAsync"/> per part instead.
    /// </remarks>
    ValueTask WriteContentAsync(string markdown, CancellationToken cancellationToken);

    /// <summary>
    ///     Adds one content part and returns the relative path to reference it by.
    /// </summary>
    /// <param name="part">The part descriptor (kind, advisory ordinal, and title).</param>
    /// <param name="markdown">The markdown content of the part.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A task yielding the relative path (for example <c>parts/0001-sheet-summary.md</c>) for
    ///     the part.
    /// </returns>
    /// <remarks>
    ///     Core assigns the real part ordinal in call order regardless of the ordinal on
    ///     <paramref name="part"/>, keeping part numbering dense and stable.
    /// </remarks>
    ValueTask<string> AddContentPartAsync(ContentPart part, string markdown, CancellationToken cancellationToken);

    /// <summary>
    ///     Reports document-level metadata for inclusion in the manifest.
    /// </summary>
    /// <param name="info">The document metadata to record.</param>
    /// <remarks>Recorded in the manifest's <c>document</c> block; later calls supersede earlier ones.</remarks>
    void ReportDocumentInfo(DocumentInfo info);

    /// <summary>
    ///     Reports what the document asserts about itself for inclusion in <c>metadata.json</c>.
    /// </summary>
    /// <param name="metadata">The self-reported metadata to record. Must not be null.</param>
    /// <remarks>
    ///     Distinct from <see cref="ReportDocumentInfo"/>: this carries the document's authored (or,
    ///     where explicitly tagged, backend-derived) claims with per-field provenance for the
    ///     <c>metadata.json</c> artifact, whereas <see cref="ReportDocumentInfo"/> carries the
    ///     orientation and integrity metadata (title, counts) the manifest and headings use. Later
    ///     calls supersede earlier ones. <c>metadata.json</c> is written whether or not this is
    ///     called, so a backend that reads nothing may simply not call it.
    /// </remarks>
    void ReportDocumentMetadata(DocumentMetadata metadata);


    /// <summary>
    ///     Reports a diagnostic to include in the extraction record.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to record.</param>
    /// <remarks>Appended to the ordered diagnostic stream surfaced in the result and manifest.</remarks>
    void ReportDiagnostic(ExtractionDiagnostic diagnostic);

    /// <summary>
    ///     Reports a gap explaining an absent or partial artifact.
    /// </summary>
    /// <param name="gap">The gap to record. Its identifier is assigned by Core in emission order.</param>
    /// <remarks>
    ///     Any identifier on <paramref name="gap"/> is overwritten so gap identifiers are always
    ///     dense and ordered; the reason must be non-empty.
    /// </remarks>
    void ReportGap(ExtractionGap gap);

    /// <summary>
    ///     Reports an environment fact contributed by the extractor.
    /// </summary>
    /// <param name="fact">The environment fact to record.</param>
    /// <remarks>Appended in insertion order so environment provenance reads chronologically.</remarks>
    void ReportEnvironmentFact(EnvironmentFact fact);

    /// <summary>
    ///     Reports a counted structural feature of the content the extractor produced, so the
    ///     summary can describe what is <em>in</em> <c>content.md</c> and not only how large it is.
    /// </summary>
    /// <param name="feature">The feature and its count. Must not be null.</param>
    /// <remarks>
    ///     Called by the backend, which walked the real document structure and therefore knows the
    ///     counts exactly; deriving them by scanning the rendered markdown would be a guess about a
    ///     rendering rather than a statement about the document. Repeated calls for the same label
    ///     accumulate, so a backend emitting per-part may report each part's contribution
    ///     separately. A zero count is ignored: the summary never prints a line of zeroes.
    /// </remarks>
    void ReportContentFeature(ContentFeature feature);

    /// <summary>
    ///     Reports how many items of a given kind the extractor discovered as expected.
    /// </summary>
    /// <param name="kind">The kind of content the count refers to.</param>
    /// <param name="foundCount">The number of items found in the document.</param>
    /// <remarks>
    ///     Lets Core populate the ledger's <c>found</c> counts so partial success can be stated
    ///     precisely (for example 3 of 4 images) and reconciled against what was actually written.
    /// </remarks>
    void ReportFound(GapKind kind, int foundCount);
}
