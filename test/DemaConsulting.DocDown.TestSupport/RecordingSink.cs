using System.Globalization;
using DocDown.Core;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     An <see cref="IExtractionSink"/> that records every call in order without touching the
///     filesystem, returning plausible synthetic relative paths.
/// </summary>
/// <remarks>
///     Used to assert exactly what an extractor emitted — and in what order — without running the
///     real writers or creating any files. Each <c>Add*</c> method returns a synthetic
///     forward-slash relative path shaped like the real sink's output so a caller can exercise
///     markdown-link handling. Not thread-safe; intended for the single extraction thread.
/// </remarks>
public sealed class RecordingSink : IExtractionSink
{
    /// <summary>The recorded images, in call order.</summary>
    private readonly List<RecordedImage> _images = [];

    /// <summary>The recorded rendered pages, in call order.</summary>
    private readonly List<RecordedPage> _pages = [];

    /// <summary>The recorded content parts, in call order.</summary>
    private readonly List<RecordedPart> _parts = [];

    /// <summary>The buffered single-flow content writes, in call order.</summary>
    private readonly List<string> _contentWrites = [];

    /// <summary>The reported document-info records, in call order.</summary>
    private readonly List<DocumentInfo> _documentInfos = [];

    /// <summary>The reported self-reported document-metadata records, in call order.</summary>
    private readonly List<DocumentMetadata> _documentMetadata = [];

    /// <summary>The reported notes, in call order.</summary>
    private readonly List<ExtractionNote> _notes = [];

    /// <summary>The reported environment facts, in call order.</summary>
    private readonly List<EnvironmentFact> _environmentFacts = [];

    /// <summary>The reported content features, in call order.</summary>
    private readonly List<ContentFeature> _contentFeatures = [];

    /// <summary>The ordered names of every sink call, across all methods.</summary>
    private readonly List<string> _calls = [];

    /// <summary>Gets the recorded images, in call order.</summary>
    /// <remarks>Each entry holds the bytes read from the caller's stream and the supplied hint.</remarks>
    public IReadOnlyList<RecordedImage> Images => _images;

    /// <summary>Gets the recorded rendered pages, in call order.</summary>
    /// <remarks>Each entry holds the page number and the PNG bytes read from the caller's stream.</remarks>
    public IReadOnlyList<RecordedPage> Pages => _pages;

    /// <summary>Gets the recorded content parts, in call order.</summary>
    /// <remarks>Each entry holds the part descriptor and its markdown body.</remarks>
    public IReadOnlyList<RecordedPart> Parts => _parts;

    /// <summary>Gets the buffered single-flow content writes, in call order.</summary>
    /// <remarks>One entry per <see cref="WriteContentAsync"/> call.</remarks>
    public IReadOnlyList<string> ContentWrites => _contentWrites;

    /// <summary>Gets the reported document-info records, in call order.</summary>
    /// <remarks>Later reports supersede earlier ones in the real sink; here all are retained.</remarks>
    public IReadOnlyList<DocumentInfo> DocumentInfos => _documentInfos;

    /// <summary>Gets the reported self-reported document-metadata records, in call order.</summary>
    /// <remarks>Later reports supersede earlier ones in the real sink; here all are retained for assertion.</remarks>
    public IReadOnlyList<DocumentMetadata> DocumentMetadata => _documentMetadata;

    /// <summary>Gets the reported notes, in call order.</summary>
    /// <remarks>Preserves the order notes were emitted for order-sensitive assertions.</remarks>
    public IReadOnlyList<ExtractionNote> Notes => _notes;

    /// <summary>Gets the reported environment facts, in call order.</summary>
    /// <remarks>Preserves insertion order so provenance assertions read chronologically.</remarks>
    public IReadOnlyList<EnvironmentFact> EnvironmentFacts => _environmentFacts;

    /// <summary>Gets the reported content features, in call order.</summary>
    /// <remarks>
    ///     Recorded verbatim, including any zero count: unlike the real sink, which drops zeros so the
    ///     summary never prints a line of zeroes, this records exactly what the backend reported so a
    ///     test can assert the backend's own behavior.
    /// </remarks>
    public IReadOnlyList<ContentFeature> ContentFeatures => _contentFeatures;

    /// <summary>Gets the ordered names of every sink call, across all methods.</summary>
    /// <remarks>
    ///     Lets a test assert the exact sequence of interactions — for example that content was
    ///     written before a part was added — without inspecting each typed list.
    /// </remarks>
    public IReadOnlyList<string> Calls => _calls;

    /// <inheritdoc/>
    public async ValueTask<string> AddImageAsync(Stream content, ImageHint hint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(hint);

        var bytes = await ReadAllAsync(content, cancellationToken).ConfigureAwait(false);
        _images.Add(new RecordedImage(bytes, hint));
        _calls.Add(nameof(AddImageAsync));

        // Return a synthetic path shaped like the real sink's 1-based ordinal naming
        return "images/" + _images.Count.ToString("D4", CultureInfo.InvariantCulture) + "-image.png";
    }

    /// <inheritdoc/>
    public async ValueTask<string> AddPageAsync(int pageNumber, Stream pngContent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pngContent);

        var bytes = await ReadAllAsync(pngContent, cancellationToken).ConfigureAwait(false);
        _pages.Add(new RecordedPage(pageNumber, bytes));
        _calls.Add(nameof(AddPageAsync));

        // Return a synthetic path keyed on the document page number, matching the real naming
        return "pages/page" + pageNumber.ToString("D4", CultureInfo.InvariantCulture) + ".png";
    }

    /// <inheritdoc/>
    public ValueTask WriteContentAsync(string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        _contentWrites.Add(markdown);
        _calls.Add(nameof(WriteContentAsync));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string> AddContentPartAsync(ContentPart part, string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(markdown);

        _parts.Add(new RecordedPart(part, markdown));
        _calls.Add(nameof(AddContentPartAsync));

        // Return a synthetic path shaped like the real sink's dense 1-based part ordinal
        var kind = part.Kind.ToString().ToLowerInvariant();
        return ValueTask.FromResult(
            "parts/" + _parts.Count.ToString("D4", CultureInfo.InvariantCulture) + "-" + kind + ".md");
    }

    /// <inheritdoc/>
    public void ReportDocumentInfo(DocumentInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _documentInfos.Add(info);
        _calls.Add(nameof(ReportDocumentInfo));
    }

    /// <inheritdoc/>
    public void ReportDocumentMetadata(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _documentMetadata.Add(metadata);
        _calls.Add(nameof(ReportDocumentMetadata));
    }

    /// <inheritdoc/>
    public void ReportNote(ExtractionNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        _notes.Add(note);
        _calls.Add(nameof(ReportNote));
    }

    /// <inheritdoc/>
    public void ReportEnvironmentFact(EnvironmentFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        _environmentFacts.Add(fact);
        _calls.Add(nameof(ReportEnvironmentFact));
    }

    /// <inheritdoc/>
    public void ReportContentFeature(ContentFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        _contentFeatures.Add(feature);
        _calls.Add(nameof(ReportContentFeature));
    }

    /// <summary>
    ///     Reads a stream fully into a byte array in memory.
    /// </summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The bytes read from the stream.</returns>
    /// <remarks>Copies into memory so the recorder never touches the filesystem.</remarks>
    private static async ValueTask<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}

/// <summary>
///     A recorded call to <see cref="RecordingSink.AddImageAsync"/>.
/// </summary>
/// <param name="Content">The image bytes read from the caller's stream.</param>
/// <param name="Hint">The hint the caller supplied for the image.</param>
/// <remarks>Immutable snapshot of one image emission for assertion.</remarks>
public sealed record RecordedImage(byte[] Content, ImageHint Hint);

/// <summary>
///     A recorded call to <see cref="RecordingSink.AddPageAsync"/>.
/// </summary>
/// <param name="PageNumber">The 1-based document page number the caller supplied.</param>
/// <param name="Content">The PNG bytes read from the caller's stream.</param>
/// <remarks>Immutable snapshot of one page emission for assertion.</remarks>
public sealed record RecordedPage(int PageNumber, byte[] Content);

/// <summary>
///     A recorded call to <see cref="RecordingSink.AddContentPartAsync"/>.
/// </summary>
/// <param name="Part">The part descriptor the caller supplied.</param>
/// <param name="Markdown">The markdown body of the part.</param>
/// <remarks>Immutable snapshot of one part emission for assertion.</remarks>
public sealed record RecordedPart(ContentPart Part, string Markdown);
