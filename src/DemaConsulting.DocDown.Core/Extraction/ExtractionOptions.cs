namespace DocDown.Core;

/// <summary>
///     The caller-configurable options that control an extraction.
/// </summary>
/// <remarks>
///     <para>
///         This type is a mutable options bag with public setters for ergonomic configuration.
///         It is deliberately <strong>not thread-safe</strong>: callers may mutate an instance
///         freely, but must not share one instance across threads while it is being read.
///     </para>
///     <para>
///         To make that mutability safe in practice, <c>DocDownEngine.ExtractAsync</c> takes a
///         defensive copy via <see cref="Clone"/> on entry, before any other work. As a result,
///         mutating an options instance after the call has begun cannot affect an in-flight or
///         subsequent extraction — each extraction operates on its own private snapshot.
///     </para>
/// </remarks>
public sealed class ExtractionOptions
{
    /// <summary>
    ///     Creates an options instance carrying the documented defaults.
    /// </summary>
    /// <remarks>
    ///     The defaults are the safe, complete extraction: embedded images are included, page
    ///     rendering is off, and no page range limits the run. Set only the properties you want to
    ///     change. Each extraction takes a private copy via <see cref="Clone"/>, so one instance may
    ///     be configured and reused.
    /// </remarks>
    public ExtractionOptions()
    {
    }

    /// <summary>
    ///     Gets or sets a value indicating whether document pages should be rendered to images.
    /// </summary>
    /// <remarks>
    ///     Requesting rendered pages makes selection prefer an available backend that can render
    ///     pages; when none is available for a paginated format the run still produces its layout and
    ///     records a plain note that pages were not rendered.
    /// </remarks>
    public bool RenderPages { get; set; }

    /// <summary>
    ///     Gets or sets an optional inclusive page range to limit extraction to, or
    ///     <see langword="null"/> to process the whole document.
    /// </summary>
    /// <remarks>
    ///     Lets callers extract a subset of a large document. Interpreted by backends that
    ///     support page selection; ignored by those that do not.
    /// </remarks>
    public PageRange? Pages { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether embedded images should be extracted.
    ///     Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    ///     Setting this to <see langword="false"/> deliberately suppresses embedded images: nothing is
    ///     written to <c>images/</c> and a plain note records that images were not extracted, so an
    ///     absent image folder is never ambiguous.
    /// </remarks>
    public bool IncludeEmbeddedImages { get; set; } = true;

    /// <summary>
    ///     Gets or sets an optional maximum image dimension in pixels, or <see langword="null"/>
    ///     for no limit.
    /// </summary>
    /// <remarks>
    ///     Provided so callers can bound the size of extracted raster assets. Interpreted by
    ///     backends that can downscale; a backend that cannot honor it reports a gap.
    /// </remarks>
    public int? MaxImageDimensionPx { get; set; }

    /// <summary>
    ///     Gets or sets an optional maximum image size in bytes, or <see langword="null"/> for no
    ///     limit.
    /// </summary>
    /// <remarks>
    ///     Provided so callers can cap the on-disk cost of extracted images. Interpreted by
    ///     backends that can enforce it.
    /// </remarks>
    public long? MaxImageBytes { get; set; }

    /// <summary>
    ///     Gets or sets the DPI used when rendering pages to images. Defaults to <c>150</c>.
    /// </summary>
    /// <remarks>
    ///     Balances output fidelity against file size; only meaningful when
    ///     <see cref="RenderPages"/> is enabled and a rendering backend is available.
    /// </remarks>
    public int PageRenderDpi { get; set; } = 150;

    /// <summary>
    ///     Gets or sets the scratch-folder preparation policy.
    ///     Defaults to <see cref="ScratchFolderMode.CleanIfDocDownFolder"/>.
    /// </summary>
    /// <remarks>
    ///     Governs whether Core may delete existing folder contents, so an extraction never
    ///     clobbers unrelated files unless the caller explicitly permits it.
    /// </remarks>
    public ScratchFolderMode ScratchFolder { get; set; } = ScratchFolderMode.CleanIfDocDownFolder;

    /// <summary>
    ///     Creates a shallow member-wise copy of these options.
    /// </summary>
    /// <returns>A new <see cref="ExtractionOptions"/> instance with the same values.</returns>
    /// <remarks>
    ///     Used by the builder and engine to snapshot caller options so later mutation cannot
    ///     affect an extraction already in progress (see the type-level thread-safety note). A
    ///     shallow copy suffices because every property is a value type or an immutable value.
    /// </remarks>
    public ExtractionOptions Clone() => new()
    {
        RenderPages = RenderPages,
        Pages = Pages,
        IncludeEmbeddedImages = IncludeEmbeddedImages,
        MaxImageDimensionPx = MaxImageDimensionPx,
        MaxImageBytes = MaxImageBytes,
        PageRenderDpi = PageRenderDpi,
        ScratchFolder = ScratchFolder
    };
}

/// <summary>
///     Controls how the scratch output folder is prepared before an extraction writes into it.
/// </summary>
/// <remarks>
///     The mode is a safety and hygiene control: it decides whether Core may delete existing
///     content or must refuse a folder that holds files it did not itself write, so an extraction
///     never clobbers unrelated files by accident.
/// </remarks>
public enum ScratchFolderMode
{
    /// <summary>
    ///     Delete only the files a <c>manifest.json</c> written for this very folder accounts for,
    ///     refusing the whole operation when the folder holds anything that manifest does not list;
    ///     use <see cref="Overwrite"/> to replace a folder's contents deliberately.
    /// </summary>
    CleanIfDocDownFolder,

    /// <summary>
    ///     Delete any existing contents unconditionally before writing.
    /// </summary>
    Overwrite
}

/// <summary>
///     An inclusive range of 1-based document page numbers.
/// </summary>
/// <param name="First">The first page in the range (inclusive).</param>
/// <param name="Last">The last page in the range (inclusive).</param>
/// <remarks>
///     Modeled as a <see langword="readonly"/> <see langword="record struct"/> so a page range
///     is a cheap, allocation-free value. Callers are responsible for supplying
///     <see cref="First"/> &lt;= <see cref="Last"/>; the members here do not throw on an inverted
///     range but instead treat it as empty (<see cref="Count"/> is <c>0</c> and
///     <see cref="Contains"/> is <see langword="false"/>) so a malformed range degrades safely
///     rather than producing negative counts. Instances are immutable and thread-safe.
/// </remarks>
public readonly record struct PageRange(int First, int Last)
{
    /// <summary>
    ///     Determines whether the given page number falls within this inclusive range.
    /// </summary>
    /// <param name="page">The 1-based page number to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="page"/> is between <see cref="First"/> and
    ///     <see cref="Last"/> inclusive; otherwise <see langword="false"/>. Always
    ///     <see langword="false"/> for an inverted range.
    /// </returns>
    /// <remarks>
    ///     Written to be safe for an inverted range so callers need not pre-validate. Pure and
    ///     thread-safe.
    /// </remarks>
    public bool Contains(int page) => page >= First && page <= Last;

    /// <summary>
    ///     Gets the number of pages in the inclusive range.
    /// </summary>
    /// <remarks>
    ///     Clamped to a minimum of zero so an inverted range (<see cref="Last"/> &lt;
    ///     <see cref="First"/>) yields <c>0</c> rather than a negative count. Pure and
    ///     thread-safe.
    /// </remarks>
    public int Count => Last < First ? 0 : Last - First + 1;
}
