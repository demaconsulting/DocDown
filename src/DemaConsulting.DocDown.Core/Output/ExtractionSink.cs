using System.Globalization;
using System.Security.Cryptography;

namespace DocDown.Core;

/// <summary>
///     The sole write path for an extraction: the concrete sink that allocates every output path,
///     writes image and page bytes, buffers content and parts, and records the honesty stream
///     (document info, diagnostics, gaps, environment facts, and found counts).
/// </summary>
/// <remarks>
///     <para>
///         Extractors interact with this class only through <see cref="IExtractionSink"/>: they hand
///         over bytes and metadata and receive back the relative, forward-slash path they must use in
///         markdown links, so they never construct a path or see a <see cref="System.IO"/> output
///         API. All naming, containment, deduplication, and ordinal allocation happen here, behind
///         the <see cref="ScratchFolder"/> gate, which is the load-bearing path-safety invariant of
///         the library.
///     </para>
///     <para>
///         Images are deduplicated by the SHA-256 of their written bytes: a repeat writes nothing,
///         increments the existing image's reference count, and returns the existing path. When
///         <see cref="ExtractionOptions.IncludeEmbeddedImages"/> is <see langword="false"/>, images
///         are suppressed — nothing is written, a single <c>DD0201</c> diagnostic is recorded, and
///         <see cref="string.Empty"/> is returned so a well-behaved extractor emits no link. Content
///         parts are buffered rather than written immediately because
///         <see cref="ContentSplitMode.Single"/> concatenates them into <c>content.md</c> instead of
///         emitting separate files; <see cref="ContentWriter"/> makes the final layout decision.
///     </para>
///     <para>
///         This class performs filesystem I/O (writing image and page files) and mutates internal
///         collections as it records reports. It is <strong>not thread-safe</strong>: it is driven by
///         a single extractor on a single logical flow, matching the <see cref="IExtractionSink"/>
///         contract, and must not be shared across threads without external synchronization.
///     </para>
/// </remarks>
public sealed class ExtractionSink : IExtractionSink
{
    /// <summary>The scratch folder every allocated path is contained within and written into.</summary>
    /// <remarks>Held so the sink can validate and materialize paths through the single safety gate.</remarks>
    private readonly ScratchFolder _folder;

    /// <summary>The cloned, effective options governing suppression and other write decisions.</summary>
    /// <remarks>A private snapshot, so reading it here cannot be perturbed by later caller mutation.</remarks>
    private readonly ExtractionOptions _options;

    /// <summary>The recorded images in allocation order.</summary>
    /// <remarks>A list (not a set) so enumeration order is deterministic and matches the manifest.</remarks>
    private readonly List<RecordedImage> _images = [];

    /// <summary>Maps an image content digest to its recorded entry for SHA-256 deduplication.</summary>
    /// <remarks>Lets a repeated image resolve to the existing path in one lookup without a rescan.</remarks>
    private readonly Dictionary<string, RecordedImage> _imageByDigest = new(StringComparer.Ordinal);

    /// <summary>The recorded rendered pages, indexed by page number to prevent duplicates.</summary>
    /// <remarks>Kept in insertion order; the companion index deduplicates a re-rendered page.</remarks>
    private readonly List<RecordedPage> _pages = [];

    /// <summary>Maps a document page number to its position in <see cref="_pages"/>.</summary>
    /// <remarks>Allows an idempotent replace when the same page is rendered more than once.</remarks>
    private readonly Dictionary<int, int> _pageIndex = [];

    /// <summary>The buffered content parts in call order.</summary>
    /// <remarks>Buffered so <see cref="ContentWriter"/> can decide between separate files and concatenation.</remarks>
    private readonly List<RecordedPart> _parts = [];

    /// <summary>The relative paths already allocated, used to detect and resolve collisions.</summary>
    /// <remarks>The ordinal prefix makes collisions impossible today; this guards a future change.</remarks>
    private readonly HashSet<string> _allocatedPaths = new(StringComparer.Ordinal);

    /// <summary>The buffered single-flow markdown content.</summary>
    /// <remarks>Accumulated across calls so no content is silently dropped if written in pieces.</remarks>
    private readonly System.Text.StringBuilder _content = new();

    /// <summary>The recorded diagnostics in emission order.</summary>
    /// <remarks>Ordered so the auditable stream reads chronologically in the result and manifest.</remarks>
    private readonly List<ExtractionDiagnostic> _diagnostics = [];

    /// <summary>The recorded gaps in emission order, with Core-assigned identifiers.</summary>
    /// <remarks>A list preserves the dense <c>GAP-1..N</c> ordering the identifiers depend on.</remarks>
    private readonly List<ExtractionGap> _gaps = [];

    /// <summary>The recorded environment facts in emission order.</summary>
    /// <remarks>Never re-sorted, so environment provenance reads in the order it was contributed.</remarks>
    private readonly List<EnvironmentFact> _environmentFacts = [];

    /// <summary>The accumulated content-feature counts, keyed by label, in first-reported order.</summary>
    /// <remarks>
    ///     A list rather than a dictionary so first-reported order — the order the backend judged most
    ///     informative — survives into the summary, while repeated reports of the same label
    ///     accumulate into one entry instead of printing twice.
    /// </remarks>
    private readonly List<ContentFeature> _contentFeatures = [];

    /// <summary>The reported found counts by content kind.</summary>
    /// <remarks>Supplies the ledger denominators (for example "3 of 4") the completeness story needs.</remarks>
    private readonly Dictionary<GapKind, int> _foundCounts = [];

    /// <summary>The document metadata most recently reported, or <see langword="null"/> when none.</summary>
    /// <remarks>Last write wins, matching the interface contract that later calls supersede earlier ones.</remarks>
    private DocumentInfo? _documentInfo;

    /// <summary>The self-reported document metadata most recently reported, or <see langword="null"/> when none.</summary>
    /// <remarks>
    ///     Held separately from <see cref="_documentInfo"/> so authored, provenance-tagged claims for
    ///     <c>metadata.json</c> never blend with the orientation metadata the manifest and headings
    ///     use. Last write wins.
    /// </remarks>
    private DocumentMetadata? _documentMetadata;

    /// <summary>The next 1-based image ordinal to allocate.</summary>
    /// <remarks>Monotonic so image file names sort in allocation order.</remarks>
    private int _nextImageOrdinal = 1;

    /// <summary>The next 1-based part ordinal to allocate.</summary>
    /// <remarks>Core-allocated so part numbering is dense and independent of extractor hints.</remarks>
    private int _nextPartOrdinal = 1;

    /// <summary>The next 1-based gap sequence number to allocate.</summary>
    /// <remarks>Drives the dense <c>GAP-n</c> identifiers, overwriting any caller-supplied value.</remarks>
    private int _nextGapNumber = 1;

    /// <summary>Whether image writing has been suppressed and the suppression already recorded.</summary>
    /// <remarks>Ensures the <c>DD0201</c> suppression diagnostic is emitted exactly once.</remarks>
    private bool _imagesSuppressed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtractionSink"/> class.
    /// </summary>
    /// <param name="folder">The prepared scratch folder to write into. Must not be null.</param>
    /// <param name="options">The cloned effective options for this extraction. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="folder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Both collaborators are captured by reference; the options are expected to be Core's private
    ///     clone so nothing the caller does afterwards can alter suppression or other decisions
    ///     mid-extraction.
    /// </remarks>
    public ExtractionSink(ScratchFolder folder, ExtractionOptions options)
    {
        // Reject null dependencies so no method has to defend against a missing folder or options
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(options);
        _folder = folder;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask<string> AddImageAsync(Stream content, ImageHint hint, CancellationToken cancellationToken)
    {
        // Validate inputs so a malformed call fails clearly rather than writing a corrupt artifact
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(hint);

        // Honor suppression: write nothing, record the reason once, and signal "no link" with an empty path
        if (!_options.IncludeEmbeddedImages)
        {
            if (!_imagesSuppressed)
            {
                _imagesSuppressed = true;
                RecordDiagnostic(DiagnosticCodes.EmbeddedImagesDisabled, DiagnosticSeverity.Info,
                    "Embedded-image extraction is disabled by caller options; no images were written.");
            }

            return string.Empty;
        }

        // Buffer the bytes so we can hash for deduplication and know the exact written size
        var bytes = await ReadAllBytesAsync(content, cancellationToken).ConfigureAwait(false);
        var digest = ComputeSha256(bytes);

        // A byte-identical image is stored once; a repeat just bumps the reference count and merges
        // its page associations into the surviving record so no referrer is lost to deduplication
        if (_imageByDigest.TryGetValue(digest, out var existing))
        {
            existing.References++;
            MergeReferrers(existing, hint);
            return existing.Path;
        }

        // Allocate a fresh ordinal and derive a safe file name from the (untrusted) hint
        var ordinal = _nextImageOrdinal++;
        var slug = ResolveSlug(hint.PreferredName, "image");
        var extension = ExtensionFor(hint.MediaType);
        var relativePath = AllocateRelativePath("images", $"{ordinal.ToString("D4", CultureInfo.InvariantCulture)}-{slug}", extension);

        // Route the write through the scratch-folder gate, creating the images/ folder on demand
        await WriteBytesAsync(relativePath, bytes, cancellationToken).ConfigureAwait(false);

        // Record provenance once; an extractor that transformed the bytes says so in its hint,
        // and an extractor that did not is recorded as a passthrough
        var record = new RecordedImage
        {
            Path = relativePath,
            MediaType = string.IsNullOrEmpty(hint.MediaType) ? "application/octet-stream" : hint.MediaType,
            WidthPx = hint.WidthPx,
            HeightPx = hint.HeightPx,
            SizeBytes = bytes.LongLength,
            Sha256 = digest,
            SourcePage = hint.SourcePage ?? (hint.SourcePages is { Count: > 0 } pages ? pages[0] : null),
            SourceRef = hint.SourceRef,
            Transform = hint.Transform ?? ImageTransform.Passthrough,
            Description = hint.Description,
            DescriptionSource = hint.DescriptionSource,
            SourcePages = hint.SourcePages is { Count: > 0 } ? [.. hint.SourcePages] : [],
            ReferencedByTemplate = hint.ReferencedByTemplate,
            References = 1
        };
        _images.Add(record);
        _imageByDigest[digest] = record;
        return relativePath;
    }

    /// <summary>
    ///     Merges a duplicate add's page associations into the surviving deduplicated image record.
    /// </summary>
    /// <param name="existing">The already-recorded image the duplicate resolves to.</param>
    /// <param name="hint">The duplicate add's hint, whose referrers are folded in.</param>
    /// <remarks>
    ///     Deduplication stores byte-identical bytes once; without this merge a second part that
    ///     references the same bytes from another page would lose its referrer. The union of the
    ///     source pages and the OR of the template flag keep the surviving record honest about every
    ///     place the image is used. The scalar source page stays the deterministic lowest referrer.
    ///     Side effect: mutates <paramref name="existing"/>.
    /// </remarks>
    private static void MergeReferrers(RecordedImage existing, ImageHint hint)
    {
        var pages = new SortedSet<int>(existing.SourcePages);
        if (existing.SourcePage is { } priorScalar)
        {
            pages.Add(priorScalar);
        }

        if (hint.SourcePages is { } hintPages)
        {
            foreach (var page in hintPages)
            {
                pages.Add(page);
            }
        }

        if (hint.SourcePage is { } scalar)
        {
            pages.Add(scalar);
        }

        existing.SourcePages = [.. pages];
        if (pages.Count > 0)
        {
            existing.SourcePage = pages.Min;
        }

        existing.ReferencedByTemplate |= hint.ReferencedByTemplate;
    }

    /// <inheritdoc />
    public async ValueTask<string> AddPageAsync(int pageNumber, Stream pngContent, CancellationToken cancellationToken)
    {
        // Validate inputs; a non-positive page number is a caller error, not extractable data
        ArgumentNullException.ThrowIfNull(pngContent);
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "The page number must be 1-based and positive.");
        }

        // Name the page file from the document page number so the file-to-page mapping is unambiguous
        var bytes = await ReadAllBytesAsync(pngContent, cancellationToken).ConfigureAwait(false);
        var digest = ComputeSha256(bytes);
        var relativePath = $"pages/page{pageNumber.ToString("D4", CultureInfo.InvariantCulture)}.png";

        // Materialize the bytes through the scratch-folder gate
        await WriteBytesAsync(relativePath, bytes, cancellationToken).ConfigureAwait(false);

        // Record the page, replacing any prior render of the same page so the manifest lists it once
        var record = new RecordedPage { Path = relativePath, PageNumber = pageNumber, SizeBytes = bytes.LongLength, Sha256 = digest };
        if (_pageIndex.TryGetValue(pageNumber, out var index))
        {
            _pages[index] = record;
        }
        else
        {
            _pageIndex[pageNumber] = _pages.Count;
            _allocatedPaths.Add(relativePath);
            _pages.Add(record);
        }

        return relativePath;
    }

    /// <inheritdoc />
    public ValueTask WriteContentAsync(string markdown, CancellationToken cancellationToken)
    {
        // Accept content into the buffer; ContentWriter finalizes it later based on the split mode
        ArgumentNullException.ThrowIfNull(markdown);
        cancellationToken.ThrowIfCancellationRequested();
        _content.Append(markdown);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<string> AddContentPartAsync(ContentPart part, string markdown, CancellationToken cancellationToken)
    {
        // Validate inputs so a part always has a kind and body
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(markdown);
        cancellationToken.ThrowIfCancellationRequested();

        // Core allocates the real ordinal in call order, ignoring the extractor's advisory value
        var ordinal = _nextPartOrdinal++;
        var kind = part.Kind.ToString().ToLowerInvariant();
        var slug = Slug(part.Title);

        // Empty slug falls back to the kind alone, per the naming rules
        var baseName = slug.Length > 0
            ? $"{ordinal.ToString("D4", CultureInfo.InvariantCulture)}-{kind}-{slug}"
            : $"{ordinal.ToString("D4", CultureInfo.InvariantCulture)}-{kind}";
        var relativePath = AllocateRelativePath("parts", baseName, "md");

        // Validate the path now (so an unsafe title fails fast) but defer the write to ContentWriter
        _ = _folder.Combine(relativePath);

        _parts.Add(new RecordedPart
        {
            Path = relativePath,
            Kind = kind,
            Ordinal = ordinal,
            Title = part.Title,
            Markdown = markdown,
            CharacterCount = markdown.Length
        });
        return ValueTask.FromResult(relativePath);
    }

    /// <inheritdoc />
    public void ReportDocumentInfo(DocumentInfo info)
    {
        // Last write wins so an extractor can refine metadata as it learns more
        ArgumentNullException.ThrowIfNull(info);
        _documentInfo = info;
    }

    /// <inheritdoc />
    public void ReportDocumentMetadata(DocumentMetadata metadata)
    {
        // Last write wins, matching the interface contract; held apart from DocumentInfo so authored
        // claims and orientation metadata never blend
        ArgumentNullException.ThrowIfNull(metadata);
        _documentMetadata = metadata;
    }

    /// <inheritdoc />
    public void ReportDiagnostic(ExtractionDiagnostic diagnostic)
    {
        // Append to the ordered stream surfaced in the result and manifest
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    /// <inheritdoc />
    public void ReportGap(ExtractionGap gap)
    {
        // Every gap must carry a reason; substitute a Core-authored one and flag it rather than accept silence
        ArgumentNullException.ThrowIfNull(gap);
        var reason = gap.Reason;
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "the extractor reported this gap without a reason";
            RecordDiagnostic(DiagnosticCodes.UnexplainedAbsence, DiagnosticSeverity.Warning,
                $"A gap targeting '{gap.Target}' was reported without a reason; Core supplied one.");
        }

        // Overwrite any caller-supplied identifier so identifiers stay dense and ordered
        var id = $"GAP-{_nextGapNumber.ToString(CultureInfo.InvariantCulture)}";
        _nextGapNumber++;
        _gaps.Add(gap with { Id = id, Reason = reason });
    }

    /// <inheritdoc />
    public void ReportEnvironmentFact(EnvironmentFact fact)
    {
        // Append in insertion order; environment provenance is deliberately never re-sorted
        ArgumentNullException.ThrowIfNull(fact);
        _environmentFacts.Add(fact);
    }

    /// <inheritdoc />
    public void ReportContentFeature(ContentFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (string.IsNullOrWhiteSpace(feature.Label))
        {
            throw new ArgumentException("The feature label must not be blank.", nameof(feature));
        }

        if (feature.Count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(feature), feature.Count, "The feature count cannot be negative.");
        }

        // Accumulate under an existing label so a per-part backend can report each part separately.
        // The lookup happens before the zero-count rule because a backend that looked for a feature may
        // report it per part: an earlier part contributing a count must not be erased by a later zero.
        var index = _contentFeatures.FindIndex(
            entry => string.Equals(entry.Label, feature.Label, StringComparison.Ordinal));
        if (index >= 0)
        {
            var accumulated = _contentFeatures[index];
            _contentFeatures[index] = feature with
            {
                Count = accumulated.Count + feature.Count,
                LookedFor = accumulated.LookedFor || feature.LookedFor,
            };
            return;
        }

        // A zero count for a feature the backend did not declare it looked for is silently dropped:
        // a PDF has no worksheets, and a "0 worksheets" line costs tokens while telling a reader
        // nothing. A zero the backend did look for is kept, because "we looked; there are none" is a
        // fact about the document a consuming agent cannot recover any other way
        if (feature.Count == 0 && !feature.LookedFor)
        {
            return;
        }

        _contentFeatures.Add(feature);
    }

    /// <inheritdoc />
    public void ReportFound(GapKind kind, int foundCount)
    {
        // Record the denominator (last value wins) so partial success can be stated precisely
        if (foundCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(foundCount), foundCount, "The found count cannot be negative.");
        }

        _foundCounts[kind] = foundCount;
    }

    /// <summary>Gets the scratch folder this sink writes into.</summary>
    /// <remarks>Exposed to the writers so they resolve and create paths through the same safety gate.</remarks>
    internal ScratchFolder Folder => _folder;

    /// <summary>Gets the recorded images in allocation order.</summary>
    /// <remarks>Consumed by the writers to populate the manifest image list and summary details.</remarks>
    internal IReadOnlyList<RecordedImage> Images => _images;

    /// <summary>Gets the recorded rendered pages in insertion order.</summary>
    /// <remarks>Consumed by the writers to populate the manifest page list and completeness counts.</remarks>
    internal IReadOnlyList<RecordedPage> Pages => _pages;

    /// <summary>Gets the buffered content parts in call order.</summary>
    /// <remarks>Consumed by <see cref="ContentWriter"/>, which decides whether to emit or concatenate them.</remarks>
    internal IReadOnlyList<RecordedPart> Parts => _parts;

    /// <summary>Gets the buffered single-flow markdown content.</summary>
    /// <remarks>Consumed by <see cref="ContentWriter"/> when the document is emitted as one flow.</remarks>
    internal string BufferedContent => _content.ToString();

    /// <summary>Gets the most recently reported document metadata, or <see langword="null"/>.</summary>
    /// <remarks>Consumed by the writers for the manifest <c>document</c> block and the summary title.</remarks>
    internal DocumentInfo? DocumentInfo => _documentInfo;

    /// <summary>Gets the most recently reported self-reported document metadata, or <see langword="null"/>.</summary>
    /// <remarks>Consumed by <see cref="MetadataWriter"/> for <c>metadata.json</c> and by the summary's metadata block.</remarks>
    internal DocumentMetadata? DocumentMetadata => _documentMetadata;

    /// <summary>Gets the recorded diagnostics in emission order.</summary>
    /// <remarks>Consumed by the writers and merged with any reconciliation-synthesized diagnostics.</remarks>
    internal IReadOnlyList<ExtractionDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Gets the recorded gaps in emission order.</summary>
    /// <remarks>Consumed by reconciliation, which appends synthesized gaps for any unexplained absence.</remarks>
    internal IReadOnlyList<ExtractionGap> Gaps => _gaps;

    /// <summary>Gets the recorded environment facts in emission order.</summary>
    /// <remarks>Consumed by the writers for the manifest and summary environment blocks.</remarks>
    internal IReadOnlyList<EnvironmentFact> EnvironmentFacts => _environmentFacts;

    /// <summary>Gets the accumulated content features in first-reported order.</summary>
    /// <remarks>
    ///     Consumed by the summary's content outline and by the manifest's <c>contentFeatures</c>
    ///     block, so the two describe the same structure from the same source. A zero count survives
    ///     only for a feature the backend declared it looked for.
    /// </remarks>
    internal IReadOnlyList<ContentFeature> ContentFeatures => _contentFeatures;

    /// <summary>Gets the reported found counts by content kind.</summary>
    /// <remarks>Consumed by reconciliation to compute ledger denominators and partial-vs-present status.</remarks>
    internal IReadOnlyDictionary<GapKind, int> FoundCounts => _foundCounts;

    /// <summary>Gets a value indicating whether image writing was suppressed.</summary>
    /// <remarks>Consumed by reconciliation to mark the images ledger entry absent and require a gap.</remarks>
    internal bool ImagesSuppressed => _imagesSuppressed;

    /// <summary>
    ///     Records a diagnostic from a code, severity, and message.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="severity">The severity of the diagnostic.</param>
    /// <param name="message">The human-readable message.</param>
    /// <remarks>
    ///     A private convenience over <see cref="ReportDiagnostic(ExtractionDiagnostic)"/> so internal
    ///     emitters (suppression, reasonless gaps) construct diagnostics consistently.
    /// </remarks>
    private void RecordDiagnostic(string code, DiagnosticSeverity severity, string message) =>
        _diagnostics.Add(new ExtractionDiagnostic(code, severity, message));

    /// <summary>
    ///     Allocates a unique relative path within a folder, appending a numeric suffix on collision.
    /// </summary>
    /// <param name="folder">The relative folder (for example <c>images</c>).</param>
    /// <param name="baseName">The base file name including its ordinal prefix and slug.</param>
    /// <param name="extension">The file extension without a leading dot.</param>
    /// <returns>A unique relative, forward-slash path.</returns>
    /// <remarks>
    ///     The ordinal prefix makes collisions impossible in practice, but the suffix loop is retained
    ///     so a future naming change cannot silently overwrite an earlier file. Records the allocation
    ///     so subsequent calls see it.
    /// </remarks>
    private string AllocateRelativePath(string folder, string baseName, string extension)
    {
        // First candidate uses the base name as-is; the ordinal prefix normally guarantees uniqueness
        var candidate = $"{folder}/{baseName}.{extension}";
        var suffix = 2;
        while (!_allocatedPaths.Add(candidate))
        {
            // On the (currently impossible) collision, disambiguate with -2, -3, ... before the extension
            candidate = $"{folder}/{baseName}-{suffix.ToString(CultureInfo.InvariantCulture)}.{extension}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>
    ///     Writes bytes to a relative path, creating the parent folder through the scratch-folder gate.
    /// </summary>
    /// <param name="relativePath">The relative, forward-slash path to write.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <remarks>
    ///     Resolving through <see cref="ScratchFolder.Combine"/> applies containment and component
    ///     validation before any byte reaches disk. Performs filesystem I/O.
    /// </remarks>
    private async ValueTask WriteBytesAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken)
    {
        // Containment and component validation happen inside Combine before we touch the filesystem
        var absolute = _folder.Combine(relativePath);
        var directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(absolute, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Slugs a value and substitutes a fallback when the slug is empty.
    /// </summary>
    /// <param name="value">The value to slug, or <see langword="null"/>.</param>
    /// <param name="fallback">The substitute used when the slug is empty.</param>
    /// <returns>A non-empty slug.</returns>
    /// <remarks>Centralizes the "empty slug substitutes the kind" rule for image naming.</remarks>
    private static string ResolveSlug(string? value, string fallback)
    {
        // Substitute the content kind when the untrusted title reduces to nothing
        var slug = Slug(value);
        return slug.Length > 0 ? slug : fallback;
    }

    /// <summary>
    ///     Produces a slug from a value using the shared scratch-folder slug rules.
    /// </summary>
    /// <param name="value">The value to slug, or <see langword="null"/>.</param>
    /// <returns>The slug, which may be empty.</returns>
    /// <remarks>A thin alias over <see cref="ScratchFolder.Slugify"/> so naming lives in one place.</remarks>
    private static string Slug(string? value) => ScratchFolder.Slugify(value);

    /// <summary>
    ///     Maps an image media type to the file extension used for the written bytes.
    /// </summary>
    /// <param name="mediaType">The media type of the image bytes, or <see langword="null"/>.</param>
    /// <returns>The extension without a leading dot; <c>bin</c> for an unrecognized type.</returns>
    /// <remarks>
    ///     The extension follows the bytes actually written, never a requested format, so the file
    ///     name never misrepresents its content. Pure and side-effect free.
    /// </remarks>
    private static string ExtensionFor(string? mediaType) => (mediaType ?? string.Empty).ToLowerInvariant() switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/tiff" => "tif",
        "image/bmp" => "bmp",
        "image/jp2" => "jp2",
        "image/x-emf" => "emf",
        "image/emf" => "emf",
        "image/x-wmf" => "wmf",
        "image/wmf" => "wmf",
        "image/svg+xml" => "svg",
        _ => "bin"
    };

    /// <summary>
    ///     Reads a stream fully into a byte array.
    /// </summary>
    /// <param name="content">The stream to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The stream's bytes.</returns>
    /// <remarks>
    ///     Buffers the whole stream because the bytes are needed twice — once to hash for dedup and
    ///     once to write. The stream is not disposed; the caller owns it.
    /// </remarks>
    private static async ValueTask<byte[]> ReadAllBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        // Copy into memory so the same bytes can be hashed and written without re-reading
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-256 of the given bytes.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The 64-character lowercase hex digest.</returns>
    /// <remarks>
    ///     Lowercase hex is used consistently across images, pages, and the source hash so the
    ///     manifest and the contract verifier compare digests byte-for-byte. Pure and side-effect free.
    /// </remarks>
    private static string ComputeSha256(byte[] bytes)
    {
        // A stable lowercase hex digest lets dedup and verification compare hashes directly
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
///     One counted structural feature of the extracted content, such as the number of headings,
///     tables, or author-attributed comments a document carried.
/// </summary>
/// <param name="Label">
///     The plural, lower-case noun phrase naming the feature (for example <c>headings</c>,
///     <c>tables</c>, <c>comments</c>, <c>distinct comment authors</c>). Rendered verbatim after the
///     count, so it must read naturally in the phrase "<c>12 tables</c>". Must not be null or blank.
/// </param>
/// <param name="Count">The number of such features present. Must not be negative.</param>
/// <param name="SingularLabel">
///     The singular form to use when <paramref name="Count"/> is one, for a label that does not
///     pluralize by a trailing <c>s</c> (for example <c>cells carrying a formula</c>). Leave
///     <see langword="null"/> for a regular label; the singular is then the label with any trailing
///     <c>s</c> removed.
/// </param>
/// <param name="LookedFor">
///     <see langword="true"/> when the backend actively looked for this feature in <em>this</em>
///     document, so a count of zero is itself a finding and must be reported; <see langword="false"/>
///     (the default) for a feature that may simply not apply, whose zero is dropped as noise.
/// </param>
/// <remarks>
///     <para>
///         The summary's bare character count tells a reader how <em>much</em> text
///         <c>content.md</c> holds but nothing about what is <em>in</em> it. That omission has a
///         measured cost: a Word draft carrying dozens of author-attributed reviewer comments was
///         mined by an agent that never learned the comments existed, so it answered a question
///         about reviewer commentary from a different document by inference. Features close that gap
///         by letting the backend — which walked the real structure and therefore already knows the
///         counts — state what the content contains, with no one regex-scanning rendered markdown.
///     </para>
///     <para>
///         A feature the format cannot have is simply never reported, and a zero count for a feature
///         the backend did not declare it looked for is dropped by
///         <see cref="ExtractionSink.ReportContentFeature"/>, because a PDF's "0 worksheets" costs
///         tokens and tells a reader nothing. A feature the backend <em>did</em> look for is reported
///         even at zero, by setting <see cref="LookedFor"/>: "this deck has no speaker notes" is a
///         fact about the document that a consuming agent cannot otherwise tell apart from "speaker
///         notes are not something DocDown counts". The inventory states that fact plainly, with no
///         verdict attached — an absence the backend looked for is never a gap and never degrades the
///         run. Instances are immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record ContentFeature(
    string Label, int Count, string? SingularLabel = null, bool LookedFor = false)
{
    /// <summary>
    ///     Gets the label agreeing in number with <see cref="Count"/>.
    /// </summary>
    /// <remarks>
    ///     Number agreement is not a cosmetic detail here: "1 headings" in a file meant to be read
    ///     by a model reads as a defect and quietly undermines trust in every other count beside it.
    ///     The trailing-<c>s</c> rule covers every regular label, and
    ///     <see cref="SingularLabel"/> handles the rest explicitly rather than by guessing.
    /// </remarks>
    public string AgreeingLabel
    {
        get
        {
            // A plural count needs no adjustment; only a count of one does
            if (Count != 1)
            {
                return Label;
            }

            // An explicit singular wins; otherwise the regular trailing-s rule applies
            return SingularLabel ?? (Label.EndsWith('s') ? Label[..^1] : Label);
        }
    }
}

/// <summary>
///     The mutable internal record of one written image, including its reference count.
/// </summary>
/// <remarks>
///     A mutable class rather than a positional record because the reference count is incremented
///     in place when a byte-identical image is added again. Internal because only the sink and the
///     writers within the assembly consume it.
/// </remarks>
internal sealed class RecordedImage
{
    /// <summary>Gets the relative path to the written image.</summary>
    /// <remarks>Forward-slash separated and returned to the extractor for markdown links.</remarks>
    public required string Path { get; init; }

    /// <summary>Gets the media type of the written bytes.</summary>
    /// <remarks>Recorded in the manifest so a consumer knows the encoding without opening the file.</remarks>
    public required string MediaType { get; init; }

    /// <summary>Gets the image width in pixels, or <see langword="null"/> when unknown.</summary>
    /// <remarks>Sourced from the extractor's hint; reported as absent rather than guessed.</remarks>
    public int? WidthPx { get; init; }

    /// <summary>Gets the image height in pixels, or <see langword="null"/> when unknown.</summary>
    /// <remarks>Sourced from the extractor's hint; reported as absent rather than guessed.</remarks>
    public int? HeightPx { get; init; }

    /// <summary>Gets the size of the written bytes in bytes.</summary>
    /// <remarks>Measured from the buffered bytes so it matches the file exactly for verification.</remarks>
    public long SizeBytes { get; init; }

    /// <summary>Gets the lowercase hexadecimal SHA-256 digest of the written bytes.</summary>
    /// <remarks>The deduplication key and the integrity hash recorded in the manifest.</remarks>
    public required string Sha256 { get; init; }

    /// <summary>Gets or sets the 1-based source page, or <see langword="null"/> when not applicable.</summary>
    /// <remarks>
    ///     Provenance from the hint, used in the summary's "what was extracted" detail. A convenience
    ///     alias for the first (lowest) entry of <see cref="SourcePages"/>; mutable so a deduplication
    ///     merge can lower it to the smallest referrer across the merged parts.
    /// </remarks>
    public int? SourcePage { get; set; }

    /// <summary>Gets or sets every 1-based page, slide, or worksheet that references the image, sorted and distinct.</summary>
    /// <remarks>
    ///     The authoritative multi-referrer record: a part shown on several slides lists them all,
    ///     retiring the single arbitrary source page. Empty when no page-level referrer is derivable
    ///     (for example a template-only or orphan part). Mutable so a deduplication merge can fold in
    ///     a byte-identical part's referrers.
    /// </remarks>
    public IReadOnlyList<int> SourcePages { get; set; } = [];

    /// <summary>Gets or sets whether a template container (a layout or master) references the image.</summary>
    /// <remarks>
    ///     Distinguishes a template-borne asset (referenced via a slide layout, slide master, or Visio
    ///     master) from a true orphan, so <see cref="SourcePages"/> being empty is never mistaken for
    ///     "referenced by nothing" when a template in fact references it. Mutable so a deduplication
    ///     merge can OR in a byte-identical part's template flag.
    /// </remarks>
    public bool ReferencedByTemplate { get; set; }

    /// <summary>Gets a backend-specific source reference, or <see langword="null"/> when none.</summary>
    /// <remarks>Provenance from the hint, recorded verbatim in the manifest.</remarks>
    public string? SourceRef { get; init; }

    /// <summary>Gets the transform the extractor reported for the written image.</summary>
    /// <remarks>
    ///     Taken from the extractor's hint and defaulted to
    ///     <see cref="ImageTransform.Passthrough"/> when it reported none; recorded verbatim in the
    ///     manifest so provenance is stated rather than inferred. Deduplication keeps the first
    ///     record, and therefore its transform, because byte-identical images cannot honestly have
    ///     two different provenances.
    /// </remarks>
    public required ImageTransform Transform { get; init; }

    /// <summary>Gets a human-meaningful description of the image, or <see langword="null"/> when none.</summary>
    /// <remarks>
    ///     Sourced from the extractor's hint (for example authored alt text or a caption) and
    ///     reported as absent rather than guessed; recorded in the manifest as image metadata.
    ///     Deduplication keeps the first record's description, matching how its other provenance is kept.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>Gets the provenance of <see cref="Description"/> as a camelCase string, or <see langword="null"/> when none.</summary>
    /// <remarks>
    ///     States where the description came from (for example <c>description</c>, <c>caption</c>, or
    ///     <c>heading</c>) so a reader can weigh it; present exactly when <see cref="Description"/> is.
    /// </remarks>
    public string? DescriptionSource { get; init; }

    /// <summary>Gets or sets how many times the image is referenced.</summary>
    /// <remarks>Incremented on each byte-identical add so deduplication is visible in the manifest.</remarks>
    public int References { get; set; }
}

/// <summary>
///     The internal record of one written rendered page.
/// </summary>
/// <remarks>
///     Immutable init-only because a page's identity and bytes do not change after writing; a
///     re-render replaces the whole record. Internal to the assembly.
/// </remarks>
internal sealed class RecordedPage
{
    /// <summary>Gets the relative path to the written page image.</summary>
    /// <remarks>Named from the document page number so the mapping is unambiguous.</remarks>
    public required string Path { get; init; }

    /// <summary>Gets the 1-based document page number.</summary>
    /// <remarks>The page's identity; used to deduplicate and to order the manifest page list.</remarks>
    public int PageNumber { get; init; }

    /// <summary>Gets the size of the written page image in bytes.</summary>
    /// <remarks>Measured from the buffered bytes so it matches the file for verification.</remarks>
    public long SizeBytes { get; init; }

    /// <summary>Gets the lowercase hexadecimal SHA-256 digest of the page bytes.</summary>
    /// <remarks>The integrity hash the contract verifier recomputes from disk.</remarks>
    public required string Sha256 { get; init; }
}

/// <summary>
///     The internal record of one buffered content part.
/// </summary>
/// <remarks>
///     Buffers the markdown alongside the allocated path so <see cref="ContentWriter"/> can either
///     write the part file or concatenate it into <c>content.md</c>. Immutable init-only; internal
///     to the assembly.
/// </remarks>
internal sealed class RecordedPart
{
    /// <summary>Gets the relative path allocated for the part file.</summary>
    /// <remarks>Returned to the extractor and used when the layout emits separate part files.</remarks>
    public required string Path { get; init; }

    /// <summary>Gets the lowercase part kind (for example <c>sheet</c>).</summary>
    /// <remarks>Embedded in the file name and recorded in the manifest so a reader knows the part type.</remarks>
    public required string Kind { get; init; }

    /// <summary>Gets the Core-assigned dense 1-based ordinal.</summary>
    /// <remarks>Independent of any extractor hint so ordering is stable and gap-free.</remarks>
    public int Ordinal { get; init; }

    /// <summary>Gets the part title, or <see langword="null"/> when none.</summary>
    /// <remarks>Used for the index heading and the concatenated-mode section heading.</remarks>
    public string? Title { get; init; }

    /// <summary>Gets the part's markdown body.</summary>
    /// <remarks>Buffered so the final layout decision can defer writing until the split mode is known.</remarks>
    public required string Markdown { get; init; }

    /// <summary>Gets the character count of the part body.</summary>
    /// <remarks>Recorded for the manifest so part sizes are visible without opening each file.</remarks>
    public int CharacterCount { get; init; }
}
