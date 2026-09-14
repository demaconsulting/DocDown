using DocDown.Visio.Com;

namespace DemaConsulting.DocDown.Office.Tests.Visio.TestData;

/// <summary>
///     A cross-platform stub of the Visio automation seam that replays a supplied set of rendered
///     pages, so the whole COM extraction path can be exercised in CI without Microsoft Office.
/// </summary>
/// <remarks>Records the path and DPI it was asked to render, and its own disposal, for assertion.</remarks>
internal sealed class StubVisioAutomation : IVisioAutomation
{
    /// <summary>The rendered pages every render replays.</summary>
    private readonly IReadOnlyList<VisioRenderedPage> _pages;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StubVisioAutomation"/> class.
    /// </summary>
    /// <param name="pages">The rendered pages to replay.</param>
    public StubVisioAutomation(IReadOnlyList<VisioRenderedPage> pages) => _pages = pages;

    /// <summary>Gets the path the last render received.</summary>
    public string? LastPath { get; private set; }

    /// <summary>Gets the DPI the last render received.</summary>
    public int LastDpi { get; private set; }

    /// <summary>Gets a value indicating whether this automation has been disposed.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<VisioRenderedPage> Render(string path, int dpi)
    {
        LastPath = path;
        LastDpi = dpi;
        return _pages;
    }

    /// <inheritdoc />
    public void Dispose() => Disposed = true;

    /// <summary>
    ///     Builds a rendered page carrying a minimal valid PNG.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <returns>The rendered page.</returns>
    public static VisioRenderedPage Rendered(int pageNumber) => new(pageNumber, MinimalPng(), null);

    /// <summary>
    ///     Builds a rendered page that failed to render.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The rendered page.</returns>
    public static VisioRenderedPage Failed(int pageNumber, string reason) => new(pageNumber, null, reason);

    /// <summary>
    ///     Builds the bytes of a minimal 1x1 PNG.
    /// </summary>
    /// <returns>The PNG bytes.</returns>
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
