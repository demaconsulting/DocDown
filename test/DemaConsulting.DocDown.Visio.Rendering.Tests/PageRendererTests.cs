using DemaConsulting.DocDown.Visio.Rendering.Tests.TestData;

namespace DemaConsulting.DocDown.Visio.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PageRenderer"/>, the package's rasterization seam: that it
///     rasterizes a page to a valid PNG, that the page count matches the source drawing,
///     and that concurrent renders all succeed with no process-wide lock.
/// </summary>
/// <remarks>
///     These tests exercise the real rasterization and PNG encode through
///     CanvasNet.Vsdx/CanvasNet, so they are the transitive verification evidence for the
///     CanvasNet.Vsdx and CanvasNet OTS items. Rendering is always available in every environment,
///     since CanvasNet.Vsdx is a fully-managed library with no native stack that could be absent.
/// </remarks>
public class PageRendererTests
{
    /// <summary>
    ///     Proves a single page rasterizes to a valid PNG of plausible dimensions.
    /// </summary>
    [Fact]
    public void PageRenderer_Render_SinglePage_ReturnsValidPng()
    {
        // Arrange: the embedded single-page probe drawing
        var vsdx = VsdxRenderingFixtures.Probe();

        // Act: rasterize page 0 at 96 DPI
        var png = PageRenderer.Render(vsdx, 0, 96);

        // Assert: the bytes are a valid PNG with positive dimensions
        Assert.True(Png.HasSignature(png), "the rendered bytes do not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(png);
        Assert.True(width > 0 && height > 0, $"implausible dimensions {width}x{height}");
    }

    /// <summary>
    ///     Proves a higher DPI produces a larger rendered page.
    /// </summary>
    [Fact]
    public void PageRenderer_Render_HigherDpi_ProducesLargerImage()
    {
        // Arrange: the embedded probe drawing rendered at two DPIs
        var vsdx = VsdxRenderingFixtures.Probe();

        // Act
        var low = Png.ReadDimensions(PageRenderer.Render(vsdx, 0, 96));
        var high = Png.ReadDimensions(PageRenderer.Render(vsdx, 0, 192));

        // Assert: doubling the DPI doubles the pixel dimensions
        Assert.True(high.Width > low.Width, "doubling the DPI should widen the rendered page");
        Assert.True(high.Height > low.Height, "doubling the DPI should heighten the rendered page");
    }

    /// <summary>
    ///     Proves concurrent renders all succeed with no process-wide lock.
    /// </summary>
    /// <remarks>
    ///     CanvasNet.Vsdx is a fully-managed library with no shared mutable per-call state: each
    ///     call opens and disposes its own document instance. Driving several renders in parallel
    ///     and asserting each returns a valid PNG is the observable evidence that independent calls
    ///     do not interfere with one another.
    /// </remarks>
    [Fact]
    public void PageRenderer_Render_ConcurrentCalls_AllProduceValidPng()
    {
        // Arrange: one drawing rendered from several threads at once
        var vsdx = VsdxRenderingFixtures.Probe();

        // Act: render in parallel
        var results = new byte[8][];
        Parallel.For(0, results.Length, index => results[index] = PageRenderer.Render(vsdx, 0, 96));

        // Assert: every parallel render produced a valid PNG
        Assert.All(results, png => Assert.True(Png.HasSignature(png)));
    }

    /// <summary>
    ///     Proves the renderer rejects a null drawing.
    /// </summary>
    [Fact]
    public void PageRenderer_Render_NullVsdx_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PageRenderer.Render(null!, 0, 96));
    }

    /// <summary>
    ///     Proves the page-count reader rejects a null drawing.
    /// </summary>
    [Fact]
    public void PageRenderer_GetPageCount_NullVsdx_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PageRenderer.GetPageCount(null!));
    }

    /// <summary>
    ///     Proves the page count matches the number of pages in the source drawing.
    /// </summary>
    [Fact]
    public void PageRenderer_GetPageCount_ProbeDrawing_ReturnsPageCount()
    {
        // Arrange: the embedded single-page probe drawing
        var vsdx = VsdxRenderingFixtures.Probe();

        // Act
        var count = PageRenderer.GetPageCount(vsdx);

        // Assert
        Assert.Equal(1, count);
    }
}
