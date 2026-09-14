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
