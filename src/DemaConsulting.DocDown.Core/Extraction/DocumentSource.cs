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
