namespace DocDown.PowerPoint.Com;

/// <summary>
///     The narrow seam through which the COM backend reaches Microsoft PowerPoint to render slides,
///     isolating the single untestable boundary from the rest of the system.
/// </summary>
/// <remarks>
///     <para>
///         Everything the COM backend does apart from talking to PowerPoint — content delegation to
///         the managed backend, availability handling, the gap and diagnostic policy, per-slide fault
///         isolation, and outcome mapping — is exercised cross-platform in CI by injecting a stub
///         implementation of this interface. The real automation adapter that implements it is
///         Windows-only and its behavior in a deployed environment is proven by release-time
///         self-tests rather than by CI.
///     </para>
///     <para>
///         An implementation opens a deck read-only, exports each slide to a PNG, and never prompts.
///         Instances own their PowerPoint session and release it on
///         <see cref="System.IDisposable.Dispose"/>.
///     </para>
/// </remarks>
internal interface IPowerPointAutomation : IDisposable
{
    /// <summary>
    ///     Renders every slide of a deck to a PNG image at the given resolution.
    /// </summary>
    /// <param name="path">The absolute path of the deck to render. Must not be null or empty.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    /// <returns>
    ///     One entry per slide in presentation order, each carrying either the slide's PNG bytes or a
    ///     per-slide failure reason so a single unrenderable slide degrades the run rather than
    ///     aborting it.
    /// </returns>
    /// <remarks>
    ///     Read-only; a conforming implementation never prompts and never writes to the deck. The
    ///     whole render runs in one session so no COM object outlives the call.
    /// </remarks>
    IReadOnlyList<PowerPointRenderedSlide> Render(string path, int dpi);
}

/// <summary>
///     One rendered slide: its 1-based number and either its PNG bytes or the reason it could not be
///     rendered.
/// </summary>
/// <param name="SlideNumber">The slide's 1-based number in presentation order.</param>
/// <param name="Png">The slide's PNG bytes, or <see langword="null"/> when the slide failed to render.</param>
/// <param name="FailureReason">
///     The reason the slide could not be rendered, or <see langword="null"/> when it rendered
///     successfully. Exactly one of <see cref="Png"/> and <see cref="FailureReason"/> is non-null.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointRenderedSlide(int SlideNumber, byte[]? Png, string? FailureReason);
