namespace DocDown.Visio.Com;

/// <summary>
///     The narrow seam through which the COM backend reaches Microsoft Visio to render pages,
///     isolating the single untestable boundary from the rest of the system.
/// </summary>
/// <remarks>
///     <para>
///         Everything the COM backend does apart from talking to Visio — content and topology
///         delegation to the managed backend, availability handling, the gap and diagnostic policy,
///         per-page fault isolation, and outcome mapping — is exercised cross-platform in CI by
///         injecting a stub implementation of this interface. The real automation adapter that
///         implements it is Windows-only and its behavior in a deployed environment is proven by
///         release-time self-tests rather than by CI.
///     </para>
///     <para>
///         An implementation opens a drawing read-only, exports each foreground page to a PNG, and
///         never prompts. Instances own their Visio session and release it on
///         <see cref="System.IDisposable.Dispose"/>.
///     </para>
/// </remarks>
internal interface IVisioAutomation : IDisposable
{
    /// <summary>
    ///     Renders every foreground page of a drawing to a PNG image at the given resolution.
    /// </summary>
    /// <param name="path">The absolute path of the drawing to render. Must not be null or empty.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    /// <returns>
    ///     One entry per foreground page in document order, each carrying either the page's PNG bytes
    ///     or a per-page failure reason so a single unrenderable page degrades the run rather than
    ///     aborting it.
    /// </returns>
    /// <remarks>
    ///     Read-only; a conforming implementation never prompts and never writes to the drawing. The
    ///     whole render runs in one session so no COM object outlives the call. The export crops to
    ///     the drawing extent, so pixel dimensions are read back from the written image rather than
    ///     derived from the page size.
    /// </remarks>
    IReadOnlyList<VisioRenderedPage> Render(string path, int dpi);
}

/// <summary>
///     One rendered page: its 1-based number and either its PNG bytes or the reason it could not be
///     rendered.
/// </summary>
/// <param name="PageNumber">The page's 1-based number among the drawing's foreground pages.</param>
/// <param name="Png">The page's PNG bytes, or <see langword="null"/> when the page failed to render.</param>
/// <param name="FailureReason">
///     The reason the page could not be rendered, or <see langword="null"/> when it rendered
///     successfully. Exactly one of <see cref="Png"/> and <see cref="FailureReason"/> is non-null.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record VisioRenderedPage(int PageNumber, byte[]? Png, string? FailureReason);
