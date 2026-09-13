using DocDown.PowerPoint.Com;

namespace DemaConsulting.DocDown.PowerPoint.Tests.TestData;

/// <summary>
///     A cross-platform stub of the PowerPoint automation seam that replays a supplied set of
///     rendered slides, so the whole COM extraction path can be exercised in CI without Microsoft
///     Office.
/// </summary>
/// <remarks>Records the path and DPI it was asked to render, and its own disposal, for assertion.</remarks>
internal sealed class StubPowerPointAutomation : IPowerPointAutomation
{
    /// <summary>The rendered slides every render replays.</summary>
    private readonly IReadOnlyList<PowerPointRenderedSlide> _slides;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StubPowerPointAutomation"/> class.
    /// </summary>
    /// <param name="slides">The rendered slides to replay.</param>
    public StubPowerPointAutomation(IReadOnlyList<PowerPointRenderedSlide> slides) => _slides = slides;

    /// <summary>Gets the path the last render received.</summary>
    public string? LastPath { get; private set; }

    /// <summary>Gets the DPI the last render received.</summary>
    public int LastDpi { get; private set; }

    /// <summary>Gets a value indicating whether this automation has been disposed.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<PowerPointRenderedSlide> Render(string path, int dpi)
    {
        LastPath = path;
        LastDpi = dpi;
        return _slides;
    }

    /// <inheritdoc />
    public void Dispose() => Disposed = true;

    /// <summary>
    ///     Builds a rendered slide carrying a minimal valid PNG.
    /// </summary>
    /// <param name="slideNumber">The 1-based slide number.</param>
    /// <returns>The rendered slide.</returns>
    public static PowerPointRenderedSlide Rendered(int slideNumber) =>
        new(slideNumber, MinimalPng(), null);

    /// <summary>
    ///     Builds a rendered slide that failed to render.
    /// </summary>
    /// <param name="slideNumber">The 1-based slide number.</param>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The rendered slide.</returns>
    public static PowerPointRenderedSlide Failed(int slideNumber, string reason) =>
        new(slideNumber, null, reason);

    /// <summary>
    ///     Builds the bytes of a minimal 1x1 PNG.
    /// </summary>
    /// <returns>The PNG bytes.</returns>
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
