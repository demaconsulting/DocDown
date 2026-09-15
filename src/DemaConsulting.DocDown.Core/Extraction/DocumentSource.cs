namespace DocDown.Core;

/// <summary>
///     A read source for a document, backed either by a file path or by a caller-owned stream.
/// </summary>
/// <remarks>
///     <para>
///         The engine opens the source; extractors receive this object and call
///         <see cref="OpenRead"/> to obtain a fresh readable stream. Abstracting over file and
///         stream sources lets the same extraction pipeline serve both on-disk documents and
///         in-memory content without special cases.
///     </para>
///     <para>
///         Construction never performs I/O that can fail: <see cref="FromFile"/> records size
///         best-effort and returns <see langword="null"/> for a missing file rather than
///         throwing, deferring any read error to <see cref="OpenRead"/> where the engine can
///         convert it into a structured failure.
///     </para>
///     <para>
///         For a stream source the underlying stream is <em>caller-owned</em>: this class never
///         disposes it. Concurrent extractions must not share one stream-backed instance because
///         <see cref="OpenRead"/> rewinds the shared stream.
///     </para>
/// </remarks>
public sealed class DocumentSource
{
    /// <summary>
    ///     The backing file path for a file source, or <see langword="null"/> for a stream source.
    /// </summary>
    /// <remarks>Stored so <see cref="OpenRead"/> can open a fresh file stream on demand.</remarks>
    private readonly string? _path;

    /// <summary>
    ///     The caller-owned backing stream for a stream source, or <see langword="null"/> for a file source.
    /// </summary>
    /// <remarks>
    ///     Held by reference (never copied or owned) so the caller retains responsibility for its
    ///     lifetime; <see cref="OpenRead"/> rewinds and wraps it without taking ownership.
    /// </remarks>
    private readonly Stream? _stream;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DocumentSource"/> class.
    /// </summary>
    /// <param name="path">The backing file path, or <see langword="null"/> for a stream source.</param>
    /// <param name="stream">The backing stream, or <see langword="null"/> for a file source.</param>
    /// <param name="fileName">The logical file name of the document.</param>
    /// <param name="sizeBytes">The known size in bytes, or <see langword="null"/> when unknown.</param>
    /// <remarks>
    ///     Private so instances are only created through the validated <see cref="FromFile"/> and
    ///     <see cref="FromStream"/> factories, guaranteeing exactly one of path/stream is set.
    /// </remarks>
    private DocumentSource(string? path, Stream? stream, string fileName, long? sizeBytes)
    {
        _path = path;
        _stream = stream;
        FileName = fileName;
        Path = path;
        SizeBytes = sizeBytes;
    }

    /// <summary>
    ///     Gets the logical file name of the document (without directory).
    /// </summary>
    /// <remarks>Used for format detection by extension and for reporting the source in output.</remarks>
    public string FileName { get; }

    /// <summary>
    ///     Gets the backing file path for a file source, or <see langword="null"/> for a stream source.
    /// </summary>
    /// <remarks>Exposed so the engine and manifest can record provenance for on-disk documents.</remarks>
    public string? Path { get; }

    /// <summary>
    ///     Gets the size of the document in bytes, or <see langword="null"/> when it is unknown.
    /// </summary>
    /// <remarks>
    ///     <see langword="null"/> for a missing file or a non-seekable stream, so consumers must
    ///     treat size as best-effort rather than guaranteed.
    /// </remarks>
    public long? SizeBytes { get; }

    /// <summary>
    ///     Creates a document source backed by a file path.
    /// </summary>
    /// <param name="path">The path to the document file. Must not be null or empty.</param>
    /// <returns>A <see cref="DocumentSource"/> for the given file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <remarks>
    ///     Records the file size only if the file currently exists; a missing file yields a
    ///     <see langword="null"/> size and no exception, deferring the read error to
    ///     <see cref="OpenRead"/>. This keeps construction total and side-effect-light.
    /// </remarks>
    public static DocumentSource FromFile(string path)
    {
        // A source must have an identity; refuse an empty path up front
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Record size best-effort; a missing file is not an error until an actual read is attempted
        var fileInfo = new FileInfo(path);
        var size = fileInfo.Exists ? fileInfo.Length : (long?)null;

        return new DocumentSource(path, null, System.IO.Path.GetFileName(path), size);
    }

    /// <summary>
    ///     Creates a document source backed by a caller-owned stream.
    /// </summary>
    /// <param name="content">The seekable stream containing the document. Must not be null.</param>
    /// <param name="fileName">The logical file name to associate with the content. Must not be null or empty.</param>
    /// <returns>A <see cref="DocumentSource"/> for the given stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null or empty.</exception>
    /// <remarks>
    ///     The stream remains owned by the caller and is never disposed by this class. Its size is
    ///     recorded only when the stream is seekable, because a non-seekable stream cannot report
    ///     a reliable length.
    /// </remarks>
    public static DocumentSource FromStream(Stream content, string fileName)
    {
        // Validate inputs so a stream source always has content and an identity
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        // Only a seekable stream can report a trustworthy length
        var size = content.CanSeek ? content.Length : (long?)null;

        return new DocumentSource(null, content, fileName, size);
    }

    /// <summary>
    ///     Opens a fresh readable stream over the document.
    /// </summary>
    /// <returns>
    ///     A readable stream positioned at the start of the document. For a file source this is a
    ///     new <see cref="FileStream"/> the caller must dispose; for a stream source this is a
    ///     non-owning wrapper that rewinds the underlying stream and does not dispose it.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when a file source's file does not exist.</exception>
    /// <exception cref="IOException">Thrown when a file source cannot be opened for reading.</exception>
    /// <remarks>
    ///     The read error for a missing or unreadable file surfaces here (not at construction) so
    ///     the engine can catch it and report an unreadable-source failure. For a stream source the
    ///     underlying stream is rewound to position zero so each open reads from the beginning.
    /// </remarks>
    public Stream OpenRead()
    {
        // File source: hand back an independent, disposable stream the caller fully owns
        if (_path is not null)
        {
            return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // Stream source: rewind the caller-owned stream and wrap it so disposal does not close it
        var stream = _stream!;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return new NonClosingStreamWrapper(stream);
    }

    /// <summary>
    ///     A <see cref="Stream"/> decorator that delegates every operation to an inner stream but
    ///     never disposes it.
    /// </summary>
    /// <remarks>
    ///     Used for stream sources so an extractor can dispose the stream it received from
    ///     <see cref="OpenRead"/> without closing the caller-owned underlying stream, preserving
    ///     the caller-ownership contract. Not thread-safe; it shares the inner stream's position.
    /// </remarks>
    private sealed class NonClosingStreamWrapper : Stream
    {
        /// <summary>
        ///     The wrapped, caller-owned stream that this decorator must not dispose.
        /// </summary>
        /// <remarks>All operations delegate to this instance so behavior matches the inner stream exactly.</remarks>
        private readonly Stream _inner;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NonClosingStreamWrapper"/> class.
        /// </summary>
        /// <param name="inner">The stream to wrap without owning. Must not be null.</param>
        /// <remarks>Stores the inner stream by reference; ownership stays with the original caller.</remarks>
        public NonClosingStreamWrapper(Stream inner) => _inner = inner;

        /// <summary>Gets a value indicating whether the inner stream supports reading.</summary>
        /// <remarks>Delegates so the wrapper's capabilities match the inner stream.</remarks>
        public override bool CanRead => _inner.CanRead;

        /// <summary>Gets a value indicating whether the inner stream supports seeking.</summary>
        /// <remarks>Delegates so the wrapper's capabilities match the inner stream.</remarks>
        public override bool CanSeek => _inner.CanSeek;

        /// <summary>Gets a value indicating whether the inner stream supports writing.</summary>
        /// <remarks>Delegates so the wrapper's capabilities match the inner stream.</remarks>
        public override bool CanWrite => _inner.CanWrite;

        /// <summary>Gets the length of the inner stream in bytes.</summary>
        /// <remarks>Delegates to the inner stream so length reflects the real content.</remarks>
        public override long Length => _inner.Length;

        /// <summary>Gets or sets the position within the inner stream.</summary>
        /// <remarks>Delegates so seeking through the wrapper moves the shared inner position.</remarks>
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        /// <summary>Flushes any buffered data to the inner stream.</summary>
        /// <remarks>Delegates so buffered writes are honored by the inner stream.</remarks>
        public override void Flush() => _inner.Flush();

        /// <summary>Reads a sequence of bytes from the inner stream.</summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="offset">The zero-based offset in <paramref name="buffer"/> at which to begin storing data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes read into the buffer.</returns>
        /// <remarks>Delegates directly to the inner stream.</remarks>
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        /// <summary>Sets the position within the inner stream.</summary>
        /// <param name="offset">The byte offset relative to <paramref name="origin"/>.</param>
        /// <param name="origin">The reference point from which to seek.</param>
        /// <returns>The new position within the inner stream.</returns>
        /// <remarks>Delegates directly to the inner stream.</remarks>
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        /// <summary>Sets the length of the inner stream.</summary>
        /// <param name="value">The desired length in bytes.</param>
        /// <remarks>Delegates directly to the inner stream.</remarks>
        public override void SetLength(long value) => _inner.SetLength(value);

        /// <summary>Writes a sequence of bytes to the inner stream.</summary>
        /// <param name="buffer">The buffer containing data to write.</param>
        /// <param name="offset">The zero-based offset in <paramref name="buffer"/> at which to begin copying.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <remarks>Delegates directly to the inner stream.</remarks>
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        /// <summary>
        ///     Releases the decorator's resources without disposing the inner stream.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Stream.Dispose()"/>.</param>
        /// <remarks>
        ///     Deliberately does not forward disposal to the inner stream: the entire purpose of
        ///     this wrapper is to keep the caller-owned stream open when the extractor disposes the
        ///     stream returned by <see cref="OpenRead"/>.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            // Intentionally do NOT dispose the inner stream; it is owned by the caller
            base.Dispose(disposing);
        }
    }
}

/// <summary>
///     Document-level metadata an extractor reports about the source document.
/// </summary>
/// <param name="Title">The document title, or <see langword="null"/> when unknown or unavailable.</param>
/// <param name="Author">The document author, or <see langword="null"/> when unknown or unavailable.</param>
/// <param name="PageCount">The number of pages, or <see langword="null"/> when not applicable or unknown.</param>
/// <param name="PartCount">
///     The number of logical parts (sheets, slides, sections), or <see langword="null"/> when
///     not applicable or unknown.
/// </param>
/// <remarks>
///     Every member is nullable because different formats expose different metadata and a value
///     that is genuinely unknown must be reported as such rather than guessed. Surfaced in the
///     manifest's <c>document</c> block. Instances are immutable and thread-safe.
/// </remarks>
public sealed record DocumentInfo(string? Title = null, string? Author = null,
    int? PageCount = null, int? PartCount = null);

/// <summary>
///     The origin of a self-reported metadata value, so an authored claim is never confused with a
///     backend-derived one.
/// </summary>
/// <remarks>
///     Provenance is the whole point of <c>metadata.json</c>: the artifact records <em>what the
///     document asserts about itself</em>, and a reader must be able to tell an OPC core property
///     apart from a PDF document-information entry apart from a value a backend merely inferred. The
///     set is closed and each member projects to a fixed camelCase string in the serialized form, so
///     the vocabulary stays stable across releases.
/// </remarks>
public enum MetadataProvenance
{
    /// <summary>
    ///     The value came from the Open Packaging Conventions core properties
    ///     (<c>docProps/core.xml</c>), shared by Word, Excel, PowerPoint, and Visio.
    /// </summary>
    OpcCoreProperties,

    /// <summary>The value came from a PDF's document information dictionary via PdfPig.</summary>
    PdfDocumentInformation
}

/// <summary>
///     One populated metadata field: its stable name, its verbatim value, and where the value came
///     from.
/// </summary>
/// <param name="Name">
///     The field's stable camelCase name (for example <c>creator</c>, <c>modified</c>), used as the
///     JSON key and as the identity a summary or test refers to.
/// </param>
/// <param name="Value">
///     The value exactly as read (dates already normalized to ISO-8601 UTC by the mapper), preserved
///     at full length. Never blank: a blank value is an absence, not a field.
/// </param>
/// <param name="Source">The provenance of the value, so authored and derived claims stay separable.</param>
/// <remarks>
///     Only genuinely present values become fields; the omit-empty rule is applied by the mapper
///     before a field is created, so every field in a <see cref="DocumentMetadata"/> is real.
///     Immutable and thread-safe.
/// </remarks>
public sealed record DocumentMetadataField(string Name, string Value, MetadataProvenance Source);

/// <summary>
///     What a document asserts about itself: the populated metadata fields plus the interesting
///     fields the property bag exposed but left blank.
/// </summary>
/// <param name="Fields">
///     The populated fields, in a stable order, each with its provenance. Only real values appear;
///     blank values are never emitted as fields (the omit-empty rule).
/// </param>
/// <param name="AbsentInteresting">
///     The names of interesting fields the source exposes but which were blank, recorded so the
///     artifact itself can state "the document supplied no value" where a reader would expect one
///     (for example <c>title</c>). Distinct from fields the source does not expose at all, which are
///     simply not mentioned.
/// </param>
/// <remarks>
///     <para>
///         This record is the backend-neutral carrier between a reader (which maps a property bag to
///         it) and <c>MetadataWriter</c> (which serializes it to <c>metadata.json</c>). Separating
///         it from <see cref="DocumentInfo"/> keeps authored, provenance-tagged claims apart from the
///         orientation and integrity metadata (title, counts) the manifest and headings use, which is
///         exactly the "do not blend authored and derived" rule the artifact exists to honor.
///     </para>
///     <para>
///         An instance whose <see cref="Fields"/> and <see cref="AbsentInteresting"/> are both empty
///         is a genuine "this backend read nothing" statement, which the writer renders as the sparse
///         form rather than an unexplained empty object. Immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record DocumentMetadata(
    IReadOnlyList<DocumentMetadataField> Fields,
    IReadOnlyList<string> AbsentInteresting);
